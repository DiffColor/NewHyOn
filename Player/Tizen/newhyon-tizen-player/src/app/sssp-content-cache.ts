import type { ContentsInfoClass, ElementInfoClass, PageInfoClass, PlayerManifest } from '../domain/models';

const DOWNLOAD_ROOT = 'downloads';
const TIZEN_IMAGE_REMOTE_DIR = 'Contents/tizen';
const FTP_DOWNLOADER_APP_ID = 'NewHyOnT01.NewHyOnFtpDownloader';
const FTP_DOWNLOAD_OPERATION = 'http://turtlelab.co.kr/appcontrol/newhyon/ftp-download';
const FTP_DOWNLOAD_TIMEOUT_MS = 60000;
const TIZEN_DOWNLOAD_TIMEOUT_MS = 60000;
const TIZEN_IMAGE_CACHE_STATE_KEY = 'newhyon-tizen-player.tizen-image-cache.v1';
const IMAGE_EXTENSION_SET = new Set(['.jpg', '.jpeg', '.bmp', '.png', '.gif', '.webp']);
const XXHASH64_HEX_PATTERN = /^[0-9a-f]{16}$/i;
const PARTIAL_HASH_BLOCK_SIZE = 1024;
const UINT64_MASK = 0xffffffffffffffffn;
const XXH64_PRIME_1 = 11400714785074694791n;
const XXH64_PRIME_2 = 14029467366897019727n;
const XXH64_PRIME_3 = 1609587929392839161n;
const XXH64_PRIME_4 = 9650029242287828579n;
const XXH64_PRIME_5 = 2870177450012600261n;

export interface ContentCacheProgress {
  readonly completed: number;
  readonly total: number;
  readonly fileName: string;
}

export interface ContentCacheOptions {
  readonly remoteBaseUrl?: string;
  readonly ftp?: FtpContentSource;
  readonly cacheNamespace?: string;
  readonly onProgress?: (progress: ContentCacheProgress) => void;
}

interface DownloadTarget {
  readonly sourceUrl: string;
  readonly ftpSource?: FtpDownloadSource;
  readonly fileName: string;
  readonly virtualPath: string;
  readonly expectedHash: string | null;
}

interface DownloadOptions {
  readonly skipCache?: boolean;
}

interface PreferredTizenImageCacheEntry {
  readonly size: number;
}

interface LocalHashState {
  readonly status: 'not-local' | 'valid' | 'invalid';
  readonly path?: string;
  readonly expectedHash?: string;
  readonly actualHash?: string | null;
}

interface LocalHashTarget {
  readonly path: string;
  readonly expectedHash: string;
}

type PreferredTizenImageCacheState = Record<string, PreferredTizenImageCacheEntry>;

export interface FtpContentSource {
  readonly host: string;
  readonly port: number;
  readonly basePath: string;
  readonly userName: string;
  readonly password: string;
}

interface FtpDownloadSource extends FtpContentSource {
  readonly remotePath: string;
}

export async function cacheRemoteManifestContent(
  manifest: PlayerManifest,
  options: ContentCacheOptions = {},
): Promise<PlayerManifest> {
  const remoteContents = collectRemoteContents(manifest.pages, options);
  let completed = 0;
  const pages: PageInfoClass[] = [];

  for (const page of manifest.pages) {
    const elements: ElementInfoClass[] = [];
    for (const element of page.PIC_Elements ?? []) {
      const contents: ContentsInfoClass[] = [];
      for (const content of element.EIF_ContentsInfoClassList ?? []) {
        const localHashTarget = resolveLocalHashTarget(content);
        const localHashState = localHashTarget
          ? await resolveLocalHashState(localHashTarget)
          : { status: 'not-local' } satisfies LocalHashState;
        if (localHashState.status === 'valid') {
          contents.push(content);
          continue;
        }

        const source = localHashState.status === 'invalid'
          ? resolveLocalHashRepairDownloadSource(content, options)
          : resolveDownloadSource(content, options);
        if (!source) {
          if (localHashState.status === 'invalid') {
            throw new Error(
              `저장 콘텐츠 해시 불일치: ${content.CIF_FileName} expected=${localHashState.expectedHash} actual=${localHashState.actualHash ?? '-'} path=${localHashState.path ?? '-'}`,
            );
          }
          contents.push(content);
          continue;
        }

        const target = await downloadContentToTizenStorage(source, content, options);
        completed += 1;
        options.onProgress?.({
          completed,
          total: remoteContents.length,
          fileName: content.CIF_FileName,
        });

        contents.push({
          ...content,
          CIF_FileFullPath: target.downloadedPath,
          CIF_RelativePath: target.downloadedPath,
          CIF_FileExist: true,
        });
      }

      elements.push({
        ...element,
        EIF_ContentsInfoClassList: contents,
      });
    }

    pages.push({
      ...page,
      PIC_Elements: elements,
    });
  }

  return {
    ...manifest,
    pages,
  };
}

export function countRemoteManifestContent(manifest: PlayerManifest): number {
  return collectRemoteContents(manifest.pages, {}).length;
}

export function countRemoteManifestContentForOptions(
  manifest: PlayerManifest,
  options: ContentCacheOptions = {},
): number {
  return collectRemoteContents(manifest.pages, options).length;
}

function collectRemoteContents(pages: readonly PageInfoClass[], options: ContentCacheOptions): ContentsInfoClass[] {
  return pages.flatMap((page) => page.PIC_Elements ?? [])
    .flatMap((element) => element.EIF_ContentsInfoClassList ?? [])
    .filter((content) => Boolean(
      resolveDownloadSource(content, options)
      || resolveLocalHashRepairDownloadSource(content, options),
    ));
}

function readContentSourceUrl(content: ContentsInfoClass, options: ContentCacheOptions): string {
  const fileFullPath = content.CIF_FileFullPath?.trim() ?? '';
  const relativePath = content.CIF_RelativePath?.trim() ?? '';
  if (isDownloadableRemoteUrl(fileFullPath)) {
    return fileFullPath;
  }
  if (options.ftp && relativePath) {
    return relativePath;
  }
  if (options.ftp && isWindowsAbsolutePath(fileFullPath)) {
    return content.CIF_FileName.trim();
  }

  return fileFullPath || relativePath || content.CIF_FileName.trim();
}

function resolveDownloadSource(content: ContentsInfoClass, options: ContentCacheOptions): string | FtpDownloadSource {
  const sourceUrl = readContentSourceUrl(content, options);
  if (isDownloadableRemoteUrl(sourceUrl)) {
    if (isFtpUrl(sourceUrl)) {
      return parseFtpUrl(sourceUrl);
    }

    return sourceUrl;
  }

  if (hasUriScheme(sourceUrl) || isTizenVirtualPath(sourceUrl)) {
    return '';
  }

  if (options.ftp) {
    return {
      ...options.ftp,
      remotePath: buildRemotePath(options.ftp.basePath, sourceUrl),
    };
  }

  if (!options.remoteBaseUrl?.trim()) {
    return '';
  }

  return buildRemoteUrl(options.remoteBaseUrl, sourceUrl);
}

function resolveLocalHashRepairDownloadSource(content: ContentsInfoClass, options: ContentCacheOptions): string | FtpDownloadSource {
  if (isImageContent(content) || !normalizeExpectedXxHash64(content.CIF_FileHash) || !resolveLocalReadablePath(content)) {
    return '';
  }

  const remoteFileName = sanitizeRemoteFileName(content.CIF_FileName)
    || sanitizeRemoteFileName(content.CIF_RelativePath ?? '')
    || sanitizeRemoteFileName(content.CIF_FileFullPath ?? '');
  if (!remoteFileName) {
    return '';
  }
  const remotePath = buildRemotePath('Contents', remoteFileName);

  if (options.ftp) {
    return {
      ...options.ftp,
      remotePath: buildRemotePath(options.ftp.basePath, remotePath),
    };
  }

  if (options.remoteBaseUrl?.trim()) {
    return buildRemoteUrl(options.remoteBaseUrl, remotePath);
  }

  return '';
}

function buildRemoteUrl(remoteBaseUrl: string, relativePath: string): string {
  const rawBaseUrl = ensureTrailingSlash(remoteBaseUrl.trim());
  const baseUrl = new URL(rawBaseUrl);
  const normalizedRoot = normalizeRemotePath(decodeURIComponent(baseUrl.pathname), '/');
  baseUrl.pathname = buildRemotePath(normalizedRoot, relativePath);
  return baseUrl.toString();
}

function normalizeRemotePath(path: string, defaultPath: string): string {
  const trimmed = path.replace(/\\/g, '/').trim();
  if (!trimmed) {
    return defaultPath;
  }

  const prefixed = trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
  const normalized = prefixed.replace(/\/+$/, '');
  return normalized || '/';
}

function isDownloadableRemoteUrl(value: string): boolean {
  if (!value.trim()) {
    return false;
  }

  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:' || url.protocol === 'ftp:';
  } catch {
    return false;
  }
}

function buildDownloadTarget(
  source: string | FtpDownloadSource,
  content: ContentsInfoClass,
  cacheNamespace: string | undefined,
): DownloadTarget {
  const sourceKeyInput = typeof source === 'string' ? source : [
    source.host,
    source.port,
    source.basePath,
    source.remotePath,
    content.CIF_FileHash ?? '',
    content.CIF_FileSize ?? '',
  ].join('|');
  const sourcePathName = typeof source === 'string'
    ? decodeURIComponent(new URL(source).pathname.split('/').pop() ?? '')
    : source.remotePath.split('/').pop() ?? '';
  const extension = readExtension(content.CIF_FileName) || readExtension(sourcePathName);
  const baseName = sanitizeFileName(
    content.CIF_StrGUID?.trim()
    || content.CIF_FileHash?.trim()
    || removeExtension(content.CIF_FileName.trim())
    || removeExtension(sourcePathName)
    || 'content',
  );
  const sourceKey = stableHash(sourceKeyInput);
  const namespacePrefix = buildCacheNamespacePrefix(cacheNamespace);
  const fileName = `${namespacePrefix}${baseName}-${sourceKey}${extension}`;

  return {
    sourceUrl: typeof source === 'string' ? source : '',
    ftpSource: typeof source === 'string' ? undefined : source,
    fileName,
    virtualPath: `${DOWNLOAD_ROOT}/${fileName}`,
    expectedHash: normalizeExpectedXxHash64(content.CIF_FileHash),
  };
}

async function downloadContentToTizenStorage(
  source: string | FtpDownloadSource,
  content: ContentsInfoClass,
  options: ContentCacheOptions,
): Promise<{ readonly downloadedPath: string; readonly target: DownloadTarget }> {
  const target = buildDownloadTarget(source, content, options.cacheNamespace);
  const preferredSource = buildPreferredTizenImageSource(source, content);
  if (preferredSource) {
    const preferredTarget = {
      ...withDownloadSource(target, preferredSource),
      expectedHash: null,
    };
    try {
      const remoteSize = await queryRemoteContentSize(preferredTarget);
      const preferredCachedPath = await resolvePreferredTizenImageCachedPath(target, remoteSize);
      if (preferredCachedPath) {
        return {
          downloadedPath: preferredCachedPath,
          target,
        };
      }

      const downloadedPath = await downloadToTizenStorage(preferredTarget, { skipCache: true });
      markPreferredTizenImageCached(target, remoteSize);
      return {
        downloadedPath,
        target,
      };
    } catch {
      // 리사이즈 이미지가 없으면 원본 캐시/다운로드 경로로 진행한다.
    }
  }

  return {
    downloadedPath: await downloadToTizenStorage(target),
    target,
  };
}

function withDownloadSource(target: DownloadTarget, source: string | FtpDownloadSource): DownloadTarget {
  return {
    ...target,
    sourceUrl: typeof source === 'string' ? source : '',
    ftpSource: typeof source === 'string' ? undefined : source,
  };
}

function normalizeExpectedXxHash64(value: string | undefined): string | null {
  const trimmed = value?.trim() ?? '';
  return XXHASH64_HEX_PATTERN.test(trimmed) ? trimmed.toUpperCase() : null;
}

function buildPreferredTizenImageSource(
  source: string | FtpDownloadSource,
  content: ContentsInfoClass,
): string | FtpDownloadSource | null {
  if (!isImageContent(content)) {
    return null;
  }

  if (typeof source === 'string') {
    return buildPreferredTizenImageUrl(source, content);
  }

  const remotePath = buildPreferredTizenImageRemotePath(source.remotePath, content);
  if (!remotePath || remotePath === source.remotePath) {
    return null;
  }

  return {
    ...source,
    remotePath,
  };
}

function buildPreferredTizenImageUrl(sourceUrl: string, content: ContentsInfoClass): string | null {
  let url: URL;
  try {
    url = new URL(sourceUrl);
  } catch {
    return null;
  }

  const remotePath = buildPreferredTizenImageRemotePath(decodeURIComponent(url.pathname), content);
  if (!remotePath || remotePath === decodeURIComponent(url.pathname)) {
    return null;
  }

  url.pathname = remotePath;
  return url.toString();
}

function buildPreferredTizenImageRemotePath(remotePath: string, content: ContentsInfoClass): string | null {
  const fileName = sanitizeRemoteFileName(content.CIF_FileName);
  if (!fileName || !IMAGE_EXTENSION_SET.has(readExtension(fileName))) {
    return null;
  }

  const normalized = normalizeRemotePath(remotePath, '/');
  const lower = normalized.toLowerCase();
  if (lower.includes('/contents/tizen/')) {
    return null;
  }

  const tizenRelativePath = `${TIZEN_IMAGE_REMOTE_DIR}/${fileName}`;
  const contentsIndex = lower.lastIndexOf('/contents/');
  if (contentsIndex >= 0) {
    const root = normalized.slice(0, contentsIndex).replace(/\/+$/, '');
    return `${root}/${tizenRelativePath}`;
  }

  return normalizeRemotePath(tizenRelativePath, '/');
}

function sanitizeRemoteFileName(fileName: string): string {
  return fileName.replace(/\\/g, '/').split('/').pop()?.trim() ?? '';
}

function isImageContent(content: ContentsInfoClass): boolean {
  const declaredType = content.CIF_ContentType?.trim().toLowerCase() ?? '';
  if (declaredType === 'image') {
    return true;
  }
  if (declaredType === 'video') {
    return false;
  }

  return IMAGE_EXTENSION_SET.has(readExtension(
    content.CIF_FileName
    || content.CIF_RelativePath
    || content.CIF_FileFullPath
    || '',
  ));
}

async function resolvePreferredTizenImageCachedPath(target: DownloadTarget, remoteSize: number): Promise<string | null> {
  const pathExists = window.tizen?.filesystem?.pathExists;
  if (!pathExists?.(target.virtualPath)) {
    return null;
  }

  const localSize = await queryLocalVirtualPathSize(target.virtualPath);
  if (localSize !== null) {
    return localSize === remoteSize ? target.virtualPath : null;
  }

  return isPreferredTizenImageCached(target, remoteSize) ? target.virtualPath : null;
}

function isPreferredTizenImageCached(target: DownloadTarget, remoteSize: number): boolean {
  return readPreferredTizenImageCacheState()[target.virtualPath]?.size === remoteSize;
}

function markPreferredTizenImageCached(target: DownloadTarget, remoteSize: number): void {
  const state = readPreferredTizenImageCacheState();
  state[target.virtualPath] = { size: remoteSize };
  try {
    window.localStorage.setItem(TIZEN_IMAGE_CACHE_STATE_KEY, JSON.stringify(state));
  } catch {
    // 캐시 상태 저장 실패는 재생 실패 사유가 아니다. 다음 업데이트 때 리사이즈 후보를 다시 확인한다.
  }
}

function queryLocalVirtualPathSize(virtualPath: string): Promise<number | null> {
  const filesystem = window.tizen?.filesystem;
  if (!filesystem?.resolve) {
    return Promise.resolve(null);
  }

  return new Promise((resolve) => {
    filesystem.resolve?.(
      virtualPath,
      (file) => {
        const size = Number(file.fileSize);
        resolve(Number.isFinite(size) && size >= 0 ? Math.round(size) : null);
      },
      () => resolve(null),
      'r',
    );
  });
}

function readPreferredTizenImageCacheState(): PreferredTizenImageCacheState {
  let parsed: unknown;
  try {
    parsed = JSON.parse(window.localStorage.getItem(TIZEN_IMAGE_CACHE_STATE_KEY) ?? '{}');
  } catch {
    return {};
  }

  if (!parsed || typeof parsed !== 'object') {
    return {};
  }

  return Object.fromEntries(
    Object.entries(parsed)
      .map(([key, value]) => {
        if (value === true) {
          return [key, { size: -1 }] as const;
        }
        if (!value || typeof value !== 'object') {
          return null;
        }
        const size = Number((value as Record<string, unknown>).size);
        return Number.isFinite(size) && size >= 0
          ? [key, { size: Math.round(size) }] as const
          : null;
      })
      .filter((entry): entry is readonly [string, PreferredTizenImageCacheEntry] => entry !== null),
  );
}

async function queryRemoteContentSize(target: DownloadTarget): Promise<number> {
  if (target.ftpSource) {
    return queryFtpContentSize(target);
  }

  const response = await fetch(target.sourceUrl, {
    method: 'HEAD',
    cache: 'no-store',
  });
  if (!response.ok) {
    throw new Error(`원격 콘텐츠 크기 확인 실패: ${response.status} ${response.statusText}`);
  }

  const contentLength = Number(response.headers.get('content-length') ?? '');
  if (!Number.isFinite(contentLength) || contentLength <= 0) {
    throw new Error('원격 콘텐츠 크기 확인 실패: content-length 없음');
  }

  return Math.round(contentLength);
}

function queryFtpContentSize(target: DownloadTarget): Promise<number> {
  const ftpSource = target.ftpSource;
  if (!ftpSource) {
    throw new Error('FTP 크기 확인 요청 정보가 없습니다.');
  }

  return launchFtpDownloaderService(target, 'stat').then((reply) => {
    if (reply.status !== 'ok') {
      throw new Error(
        `FTP 콘텐츠 크기 확인 실패: ${target.fileName} (${reply.error || 'unknown'}; path=${sanitizeFtpPath(ftpSource)})`,
      );
    }

    const size = Number(reply.size ?? '');
    if (!Number.isFinite(size) || size <= 0) {
      throw new Error(
        `FTP 콘텐츠 크기 확인 실패: ${target.fileName} (invalid size; path=${sanitizeFtpPath(ftpSource)})`,
      );
    }

    return Math.round(size);
  });
}

function buildCacheNamespacePrefix(cacheNamespace: string | undefined): string {
  const trimmed = cacheNamespace?.trim() ?? '';
  if (!trimmed) {
    return '';
  }

  const readable = sanitizeFileName(trimmed).slice(0, 24);
  return `${readable}-${stableHash(trimmed)}-`;
}

function readExtension(fileName: string): string {
  const match = fileName.match(/(\.[a-z0-9]{1,12})$/i);
  return match?.[1]?.toLowerCase() ?? '';
}

function removeExtension(fileName: string): string {
  return fileName.replace(/\.[a-z0-9]{1,12}$/i, '');
}

function sanitizeFileName(value: string): string {
  const sanitized = value
    .normalize('NFKD')
    .replace(/[^a-zA-Z0-9._-]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 80);

  return sanitized || 'content';
}

function stableHash(value: string): string {
  let hash = 5381;
  for (let index = 0; index < value.length; index += 1) {
    hash = ((hash << 5) + hash) ^ value.charCodeAt(index);
  }

  return (hash >>> 0).toString(16).padStart(8, '0');
}

async function downloadToTizenStorage(target: DownloadTarget, options: DownloadOptions = {}): Promise<string> {
  const tizen = window.tizen;
  if (options.skipCache !== true) {
    const cachedVirtualPath = await resolveCachedVirtualPath(target);
    if (cachedVirtualPath) {
      return cachedVirtualPath;
    }
  }

  if (target.ftpSource) {
    const downloadedPath = await downloadFtpToTizenStorage(target);
    if (target.expectedHash) {
      await verifyDownloadedHash(target, downloadedPath);
    }
    return downloadedPath;
  }

  const download = tizen?.download;
  const DownloadRequest = tizen?.DownloadRequest;
  if (!download?.start || !DownloadRequest) {
    throw new Error('Tizen Download API를 사용할 수 없습니다.');
  }

  const downloadedPath = await new Promise<string>((resolve, reject) => {
    const request = new DownloadRequest(target.sourceUrl, DOWNLOAD_ROOT, target.fileName, 'ALL');
    let settled = false;
    let downloadId = 0;
    const settle = (callback: () => void) => {
      if (settled) {
        return;
      }

      settled = true;
      window.clearTimeout(timeoutId);
      callback();
    };
    const timeoutId = window.setTimeout(() => {
      if (downloadId > 0) {
        download.cancel?.(downloadId);
      }
      settle(() => reject(new Error(
        `콘텐츠 다운로드 시간 초과: ${target.fileName} (${TIZEN_DOWNLOAD_TIMEOUT_MS}ms; url=${sanitizeDownloadUrl(target.sourceUrl)})`,
      )));
    }, TIZEN_DOWNLOAD_TIMEOUT_MS);

    downloadId = download.start(request, {
      oncompleted: (_downloadId, path) => {
        settle(() => resolve(normalizeCompletedPath(path, target)));
      },
      onfailed: (_downloadId, error) => {
        settle(() => reject(new Error(
          `콘텐츠 다운로드 실패: ${target.fileName} (${formatDownloadError(error)}; url=${sanitizeDownloadUrl(target.sourceUrl)})`,
        )));
      },
    });
  });
  if (target.expectedHash) {
    await verifyDownloadedHash(target, downloadedPath);
  }
  return downloadedPath;
}

async function resolveCachedVirtualPath(target: DownloadTarget): Promise<string | null> {
  const pathExists = window.tizen?.filesystem?.pathExists;
  if (!pathExists) {
    return null;
  }

  if (!pathExists(target.virtualPath)) {
    return null;
  }

  if (!target.expectedHash) {
    return target.virtualPath;
  }

  const localHash = await computeLocalPartialXxHash64(target.virtualPath);
  return localHash === target.expectedHash ? target.virtualPath : null;
}

async function verifyDownloadedHash(target: DownloadTarget, downloadedPath: string): Promise<void> {
  if (!target.expectedHash) {
    return;
  }

  const localHash = await computeLocalPartialXxHash64(downloadedPath);
  if (localHash !== target.expectedHash) {
    throw new Error(`콘텐츠 해시 검증 실패: ${target.fileName} expected=${target.expectedHash} actual=${localHash ?? '-'}`);
  }
}

async function resolveLocalHashState(target: LocalHashTarget): Promise<LocalHashState> {
  const actualHash = await computeLocalPartialXxHash64(target.path);
  return actualHash === target.expectedHash
    ? { status: 'valid', path: target.path, expectedHash: target.expectedHash, actualHash }
    : { status: 'invalid', path: target.path, expectedHash: target.expectedHash, actualHash };
}

function resolveLocalHashTarget(content: ContentsInfoClass): LocalHashTarget | null {
  if (isImageContent(content)) {
    return null;
  }

  const expectedHash = normalizeExpectedXxHash64(content.CIF_FileHash);
  const localPath = resolveLocalReadablePath(content);
  if (!expectedHash || !localPath) {
    return null;
  }

  return {
    path: localPath,
    expectedHash,
  };
}

function resolveLocalReadablePath(content: ContentsInfoClass): string | null {
  return resolveReadableTizenPath(content.CIF_FileFullPath)
    ?? resolveReadableTizenPath(content.CIF_RelativePath);
}

function resolveReadableTizenPath(value: string | undefined): string | null {
  const trimmed = value?.trim() ?? '';
  if (!trimmed) {
    return null;
  }

  if (isTizenVirtualPath(trimmed) || trimmed.startsWith('file://')) {
    return trimmed;
  }

  return null;
}

function computeLocalPartialXxHash64(virtualPath: string): Promise<string | null> {
  const filesystem = window.tizen?.filesystem;
  if (!filesystem?.resolve) {
    return Promise.resolve(null);
  }

  return new Promise((resolve) => {
    filesystem.resolve?.(
      virtualPath,
      (file) => {
        file.openStream(
          'r',
          (stream) => {
            try {
              const fileSize = Math.max(0, Math.round(Number(file.fileSize ?? 0)));
              if (fileSize <= 0 || typeof stream.readBytes !== 'function') {
                resolve(null);
                return;
              }

              const hashInput = readPartialHashInput(stream, fileSize);
              resolve(xxh64Hex(hashInput));
            } catch {
              resolve(null);
            } finally {
              stream.close();
            }
          },
          () => resolve(null),
        );
      },
      () => resolve(null),
      'r',
    );
  });
}

function readPartialHashInput(stream: TizenFileStream, fileSize: number): Uint8Array {
  if (fileSize < PARTIAL_HASH_BLOCK_SIZE * 3) {
    stream.position = 0;
    return Uint8Array.from(stream.readBytes?.(fileSize) ?? []);
  }

  const block1 = readStreamBytesAt(stream, 0, PARTIAL_HASH_BLOCK_SIZE);
  const block2 = readStreamBytesAt(stream, Math.floor(fileSize / 2), PARTIAL_HASH_BLOCK_SIZE);
  const block3 = readStreamBytesAt(stream, Math.max(0, fileSize - PARTIAL_HASH_BLOCK_SIZE), PARTIAL_HASH_BLOCK_SIZE);
  const sizeBytes = new Uint8Array(8);
  new DataView(sizeBytes.buffer).setBigUint64(0, BigInt(fileSize), false);
  const input = new Uint8Array(block1.length + block2.length + block3.length + sizeBytes.length);
  input.set(block1, 0);
  input.set(block2, block1.length);
  input.set(block3, block1.length + block2.length);
  input.set(sizeBytes, block1.length + block2.length + block3.length);
  return input;
}

function readStreamBytesAt(stream: TizenFileStream, position: number, byteCount: number): Uint8Array {
  stream.position = position;
  return Uint8Array.from(stream.readBytes?.(byteCount) ?? []);
}

function xxh64Hex(input: Uint8Array): string {
  let offset = 0;
  let hash: bigint;
  if (input.length >= 32) {
    let v1 = toUint64(XXH64_PRIME_1 + XXH64_PRIME_2);
    let v2 = XXH64_PRIME_2;
    let v3 = 0n;
    let v4 = toUint64(-XXH64_PRIME_1);
    const limit = input.length - 32;

    while (offset <= limit) {
      v1 = xxh64Round(v1, readUint64LE(input, offset));
      offset += 8;
      v2 = xxh64Round(v2, readUint64LE(input, offset));
      offset += 8;
      v3 = xxh64Round(v3, readUint64LE(input, offset));
      offset += 8;
      v4 = xxh64Round(v4, readUint64LE(input, offset));
      offset += 8;
    }

    hash = toUint64(
      rotateLeft64(v1, 1)
      + rotateLeft64(v2, 7)
      + rotateLeft64(v3, 12)
      + rotateLeft64(v4, 18),
    );
    hash = xxh64MergeRound(hash, v1);
    hash = xxh64MergeRound(hash, v2);
    hash = xxh64MergeRound(hash, v3);
    hash = xxh64MergeRound(hash, v4);
  } else {
    hash = XXH64_PRIME_5;
  }

  hash = toUint64(hash + BigInt(input.length));
  while (offset + 8 <= input.length) {
    const lane = xxh64Round(0n, readUint64LE(input, offset));
    hash = toUint64(rotateLeft64(hash ^ lane, 27) * XXH64_PRIME_1 + XXH64_PRIME_4);
    offset += 8;
  }

  if (offset + 4 <= input.length) {
    hash = toUint64(hash ^ (BigInt(readUint32LE(input, offset)) * XXH64_PRIME_1));
    hash = toUint64(rotateLeft64(hash, 23) * XXH64_PRIME_2 + XXH64_PRIME_3);
    offset += 4;
  }

  while (offset < input.length) {
    hash = toUint64(hash ^ (BigInt(input[offset] ?? 0) * XXH64_PRIME_5));
    hash = toUint64(rotateLeft64(hash, 11) * XXH64_PRIME_1);
    offset += 1;
  }

  hash = xxh64Avalanche(hash);
  return hash.toString(16).padStart(16, '0').toUpperCase();
}

function xxh64Round(accumulator: bigint, input: bigint): bigint {
  return toUint64(rotateLeft64(toUint64(accumulator + input * XXH64_PRIME_2), 31) * XXH64_PRIME_1);
}

function xxh64MergeRound(accumulator: bigint, value: bigint): bigint {
  let merged = toUint64(accumulator ^ xxh64Round(0n, value));
  merged = toUint64(merged * XXH64_PRIME_1 + XXH64_PRIME_4);
  return merged;
}

function xxh64Avalanche(value: bigint): bigint {
  let hash = value;
  hash = toUint64((hash ^ (hash >> 33n)) * XXH64_PRIME_2);
  hash = toUint64((hash ^ (hash >> 29n)) * XXH64_PRIME_3);
  return toUint64(hash ^ (hash >> 32n));
}

function rotateLeft64(value: bigint, bits: number): bigint {
  const normalized = toUint64(value);
  return toUint64((normalized << BigInt(bits)) | (normalized >> BigInt(64 - bits)));
}

function readUint64LE(input: Uint8Array, offset: number): bigint {
  let value = 0n;
  for (let index = 7; index >= 0; index -= 1) {
    value = (value << 8n) | BigInt(input[offset + index] ?? 0);
  }
  return value;
}

function readUint32LE(input: Uint8Array, offset: number): number {
  return (
    (input[offset] ?? 0)
    | ((input[offset + 1] ?? 0) << 8)
    | ((input[offset + 2] ?? 0) << 16)
    | ((input[offset + 3] ?? 0) << 24)
  ) >>> 0;
}

function toUint64(value: bigint): bigint {
  return value & UINT64_MASK;
}

function downloadFtpToTizenStorage(target: DownloadTarget): Promise<string> {
  const ftpSource = target.ftpSource;
  if (!ftpSource) {
    throw new Error('FTP 다운로드 요청 정보가 없습니다.');
  }

  return launchFtpDownloaderService(target, 'download').then((reply) => {
    if (reply.status === 'ok' && reply.path) {
      return reply.path;
    }

    throw new Error(
      `FTP 콘텐츠 다운로드 실패: ${target.fileName} (${reply.error || 'unknown'}; path=${sanitizeFtpPath(ftpSource)})`,
    );
  });
}

function launchFtpDownloaderService(
  target: DownloadTarget,
  action: 'download' | 'stat',
): Promise<Record<string, string>> {
  const ftpSource = target.ftpSource;
  if (!ftpSource) {
    throw new Error('FTP 다운로드 요청 정보가 없습니다.');
  }
  const tizen = window.tizen;
  const application = tizen?.application;
  const ApplicationControl = tizen?.ApplicationControl;
  const ApplicationControlData = tizen?.ApplicationControlData;
  if (!application?.launchAppControl || !ApplicationControl || !ApplicationControlData) {
    throw new Error('NewHyOn FTP Downloader Service를 실행할 수 없습니다.');
  }
  const launchAppControl = application.launchAppControl.bind(application);

  return new Promise((resolve, reject) => {
    let settled = false;
    const settle = (callback: () => void) => {
      if (settled) {
        return;
      }

      settled = true;
      window.clearTimeout(timeoutId);
      callback();
    };
    const timeoutId = window.setTimeout(() => {
      settle(() => reject(new Error(
        `FTP 콘텐츠 다운로드 시간 초과: ${target.fileName} (${FTP_DOWNLOAD_TIMEOUT_MS}ms; path=${sanitizeFtpPath(ftpSource)})`,
      )));
    }, FTP_DOWNLOAD_TIMEOUT_MS);
    const appControl = new ApplicationControl(
      FTP_DOWNLOAD_OPERATION,
      null,
      'application/octet-stream',
      null,
      [
        new ApplicationControlData('host', [ftpSource.host]),
        new ApplicationControlData('port', [String(ftpSource.port)]),
        new ApplicationControlData('userName', [ftpSource.userName]),
        new ApplicationControlData('password', [ftpSource.password]),
        new ApplicationControlData('remotePath', [ftpSource.remotePath]),
        new ApplicationControlData('fileName', [target.fileName]),
        new ApplicationControlData('action', [action]),
      ],
      'SINGLE',
    );

    launchAppControl(
      appControl,
      FTP_DOWNLOADER_APP_ID,
      () => undefined,
      (error) => settle(() => reject(new Error(
        `FTP 다운로드 서비스 실행 실패: ${formatDownloadError(error)}; path=${sanitizeFtpPath(ftpSource)}`,
      ))),
      {
        onsuccess: (data) => {
          settle(() => resolve(readApplicationControlData(data)));
        },
        onfailure: () => settle(() => reject(new Error(
          `FTP 콘텐츠 다운로드 실패: ${target.fileName} (service failure; path=${sanitizeFtpPath(ftpSource)})`,
        ))),
      },
    );
  });
}

function isWindowsAbsolutePath(value: string): boolean {
  return /^[a-zA-Z]:[\\/]/.test(value.trim()) || /^\\\\/.test(value.trim());
}

function hasUriScheme(value: string): boolean {
  return /^[a-zA-Z][a-zA-Z\d+.-]*:/.test(value.trim());
}

function isTizenVirtualPath(value: string): boolean {
  return /^(downloads|documents|wgt-package|wgt-private|wgt-private-tmp)\//.test(value.trim());
}

function ensureTrailingSlash(value: string): string {
  return value.endsWith('/') ? value : `${value}/`;
}

function parseFtpUrl(sourceUrl: string): FtpDownloadSource {
  const url = new URL(sourceUrl);
  return {
    host: url.hostname,
    port: url.port ? Number.parseInt(url.port, 10) : 21,
    basePath: '/',
    userName: decodeURIComponent(url.username),
    password: decodeURIComponent(url.password),
    remotePath: normalizeRemotePath(decodeURIComponent(url.pathname), '/'),
  };
}

function buildRemotePath(rootPath: string, relativePath: string): string {
  const normalizedRoot = normalizeRemotePath(rootPath, '/');
  const normalizedRelative = normalizeRemotePath(relativePath, '/');
  if (normalizedRelative === '/') {
    return normalizedRoot;
  }
  if (
    normalizedRelative === normalizedRoot
    || normalizedRelative.toLowerCase().startsWith(`${normalizedRoot.toLowerCase()}/`)
  ) {
    return normalizedRelative;
  }

  return `${normalizedRoot.replace(/\/+$/, '')}/${normalizedRelative.replace(/^\/+/, '')}`;
}

function isFtpUrl(value: string): boolean {
  try {
    return new URL(value).protocol === 'ftp:';
  } catch {
    return false;
  }
}

function readApplicationControlData(data?: TizenApplicationControlData[] | null): Record<string, string> {
  const result: Record<string, string> = {};
  for (const item of data ?? []) {
    result[item.key] = item.value[0] ?? '';
  }

  return result;
}

function normalizeCompletedPath(path: string, target: DownloadTarget): string {
  const trimmed = path.trim();
  if (trimmed.startsWith(`${DOWNLOAD_ROOT}/`)) {
    return trimmed;
  }

  return target.virtualPath;
}

function formatDownloadError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  if (error && typeof error === 'object') {
    const values = error as Record<string, unknown>;
    return String(values.message ?? values.name ?? values.code ?? 'unknown');
  }

  return String(error || 'unknown');
}

function sanitizeDownloadUrl(value: string): string {
  const match = value.match(/^([a-zA-Z][a-zA-Z\d+.-]*:\/\/)([^/?#@]*@)?([^/?#]*)(.*)$/);
  return match ? `${match[1]}${match[3]}${match[4]}` : 'invalid-url';
}

function sanitizeFtpPath(source: FtpDownloadSource): string {
  return `${source.host}:${source.port}${source.remotePath}`;
}
