import type { ContentsInfoClass, ElementInfoClass, PageInfoClass, PlayerManifest } from '../domain/models';

const DOWNLOAD_ROOT = 'downloads';
const TIZEN_IMAGE_REMOTE_DIR = 'Contents/tizen';
const FTP_DOWNLOADER_APP_ID = 'NewHyOnFtpD01.Downloader';
const FTP_DOWNLOAD_OPERATION = 'http://turtlelab.co.kr/appcontrol/newhyon/ftp-download';
const FTP_DOWNLOAD_TIMEOUT_MS = 60000;
const TIZEN_DOWNLOAD_TIMEOUT_MS = 60000;
const TIZEN_IMAGE_CACHE_STATE_KEY = 'newhyon-tizen-player.tizen-image-cache.v1';
const IMAGE_EXTENSION_SET = new Set(['.jpg', '.jpeg', '.bmp', '.png', '.gif', '.webp']);

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
}

interface DownloadOptions {
  readonly skipCache?: boolean;
}

interface PreferredTizenImageCacheEntry {
  readonly size: number;
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
        const source = resolveDownloadSource(content, options);
        if (!source) {
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
    .filter((content) => Boolean(resolveDownloadSource(content, options)));
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
    const preferredTarget = withDownloadSource(target, preferredSource);
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
  const cachedVirtualPath = resolveCachedVirtualPath(target);
  if (!cachedVirtualPath) {
    return null;
  }

  const localSize = await queryLocalVirtualPathSize(cachedVirtualPath);
  if (localSize !== null) {
    return localSize === remoteSize ? cachedVirtualPath : null;
  }

  return isPreferredTizenImageCached(target, remoteSize) ? cachedVirtualPath : null;
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

function downloadToTizenStorage(target: DownloadTarget, options: DownloadOptions = {}): Promise<string> {
  const tizen = window.tizen;
  if (options.skipCache !== true) {
    const cachedVirtualPath = resolveCachedVirtualPath(target);
    if (cachedVirtualPath) {
      return Promise.resolve(cachedVirtualPath);
    }
  }

  if (target.ftpSource) {
    return downloadFtpToTizenStorage(target);
  }

  const download = tizen?.download;
  const DownloadRequest = tizen?.DownloadRequest;
  if (!download?.start || !DownloadRequest) {
    throw new Error('Tizen Download API를 사용할 수 없습니다.');
  }

  return new Promise((resolve, reject) => {
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
}

function resolveCachedVirtualPath(target: DownloadTarget): string | null {
  const pathExists = window.tizen?.filesystem?.pathExists;
  if (!pathExists) {
    return null;
  }

  if (pathExists(target.virtualPath)) {
    return target.virtualPath;
  }

  return null;
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
