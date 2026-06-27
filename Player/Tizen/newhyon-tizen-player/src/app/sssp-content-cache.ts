import type { ContentsInfoClass, ElementInfoClass, PageInfoClass, PlayerManifest } from '../domain/models';

const DOWNLOAD_ROOT = 'downloads';
const FTP_DOWNLOADER_APP_ID = 'NewHyOnFtpD01.Downloader';
const FTP_DOWNLOAD_OPERATION = 'http://turtlelab.co.kr/appcontrol/newhyon/ftp-download';
const IMAGE_OPTIMIZATION_QUALITY = 0.86;

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
  readonly optimizedImageVirtualPath?: string;
  readonly imageOptimization?: ImageOptimizationTarget;
}

interface ImageOptimizationTarget {
  readonly width: number;
  readonly height: number;
  readonly mimeType: 'image/jpeg' | 'image/png';
  readonly extension: '.jpg' | '.png';
}

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

        const imageOptimization = resolveImageOptimizationTarget(content, page, element);
        const target = buildDownloadTarget(source, content, options.cacheNamespace, imageOptimization);
        const downloadedPath = await downloadToTizenStorage(target);
        const virtualPath = await optimizeDownloadedImageForDisplay(downloadedPath, target);
        completed += 1;
        options.onProgress?.({
          completed,
          total: remoteContents.length,
          fileName: content.CIF_FileName,
        });

        contents.push({
          ...content,
          CIF_FileFullPath: virtualPath,
          CIF_RelativePath: virtualPath,
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
  imageOptimization: ImageOptimizationTarget | null = null,
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
  const optimizedImageVirtualPath = imageOptimization
    ? `${DOWNLOAD_ROOT}/${namespacePrefix}${baseName}-${sourceKey}-display-${imageOptimization.width}x${imageOptimization.height}${imageOptimization.extension}`
    : undefined;

  return {
    sourceUrl: typeof source === 'string' ? source : '',
    ftpSource: typeof source === 'string' ? undefined : source,
    fileName,
    virtualPath: `${DOWNLOAD_ROOT}/${fileName}`,
    optimizedImageVirtualPath,
    imageOptimization: imageOptimization ?? undefined,
  };
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

function downloadToTizenStorage(target: DownloadTarget): Promise<string> {
  const tizen = window.tizen;
  const cachedVirtualPath = resolveCachedVirtualPath(target);
  if (cachedVirtualPath) {
    return Promise.resolve(cachedVirtualPath);
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
    download.start(request, {
      oncompleted: (_downloadId, path) => {
        resolve(normalizeCompletedPath(path, target));
      },
      onfailed: (_downloadId, error) => {
        reject(new Error(
          `콘텐츠 다운로드 실패: ${target.fileName} (${formatDownloadError(error)}; url=${sanitizeDownloadUrl(target.sourceUrl)})`,
        ));
      },
    });
  });
}

function resolveCachedVirtualPath(target: DownloadTarget): string | null {
  const pathExists = window.tizen?.filesystem?.pathExists;
  if (!pathExists) {
    return null;
  }

  if (target.optimizedImageVirtualPath && pathExists(target.optimizedImageVirtualPath)) {
    return target.optimizedImageVirtualPath;
  }

  if (pathExists(target.virtualPath)) {
    return target.virtualPath;
  }

  return null;
}

async function optimizeDownloadedImageForDisplay(downloadedPath: string, target: DownloadTarget): Promise<string> {
  const optimization = target.imageOptimization;
  if (!optimization || downloadedPath === target.optimizedImageVirtualPath) {
    return downloadedPath;
  }

  const optimizedPath = target.optimizedImageVirtualPath;
  const filesystem = window.tizen?.filesystem;
  if (!optimizedPath || !filesystem?.toURI || !filesystem.openFile) {
    return downloadedPath;
  }

  const sourceUri = filesystem.toURI(downloadedPath);
  const image = await loadImageElement(sourceUri);
  const outputSize = calculateOptimizedImageSize(
    image.naturalWidth || image.width,
    image.naturalHeight || image.height,
    optimization.width,
    optimization.height,
  );
  if (!outputSize) {
    return downloadedPath;
  }

  const canvas = document.createElement('canvas');
  canvas.width = outputSize.width;
  canvas.height = outputSize.height;
  const context = canvas.getContext('2d', { alpha: optimization.mimeType === 'image/png' });
  if (!context) {
    return downloadedPath;
  }

  context.drawImage(image, 0, 0, outputSize.width, outputSize.height);
  const blob = await canvasToBlob(canvas, optimization.mimeType, IMAGE_OPTIMIZATION_QUALITY);
  await writeBlobToTizenStorage(optimizedPath, blob);
  console.info(`[download] 이미지 최적화 완료: ${downloadedPath} -> ${optimizedPath} ${image.naturalWidth}x${image.naturalHeight} => ${outputSize.width}x${outputSize.height}`);
  return optimizedPath;
}

function resolveImageOptimizationTarget(
  content: ContentsInfoClass,
  page: PageInfoClass,
  element: ElementInfoClass,
): ImageOptimizationTarget | null {
  if (String(content.CIF_ContentType).toLowerCase() !== 'image') {
    return null;
  }

  const extension = readExtension(content.CIF_FileName);
  if (!isOptimizableImageExtension(extension)) {
    return null;
  }

  const screenWidth = Math.max(1, Math.round(window.screen?.width || window.innerWidth || page.PIC_CanvasWidth || 1920));
  const screenHeight = Math.max(1, Math.round(window.screen?.height || window.innerHeight || page.PIC_CanvasHeight || 1080));
  const canvasWidth = Math.max(1, page.PIC_CanvasWidth || screenWidth);
  const canvasHeight = Math.max(1, page.PIC_CanvasHeight || screenHeight);
  const targetWidth = Math.max(1, Math.ceil((Math.max(1, element.EIF_Width) / canvasWidth) * screenWidth));
  const targetHeight = Math.max(1, Math.ceil((Math.max(1, element.EIF_Height) / canvasHeight) * screenHeight));
  const png = extension === '.png';
  return {
    width: targetWidth,
    height: targetHeight,
    mimeType: png ? 'image/png' : 'image/jpeg',
    extension: png ? '.png' : '.jpg',
  };
}

function isOptimizableImageExtension(extension: string): boolean {
  return extension === '.jpg' || extension === '.jpeg' || extension === '.png' || extension === '.bmp';
}

function calculateOptimizedImageSize(
  naturalWidth: number,
  naturalHeight: number,
  targetWidth: number,
  targetHeight: number,
): { width: number; height: number } | null {
  if (!Number.isFinite(naturalWidth) || !Number.isFinite(naturalHeight) || naturalWidth <= 0 || naturalHeight <= 0) {
    return null;
  }

  if (naturalWidth <= targetWidth && naturalHeight <= targetHeight) {
    return null;
  }

  const scale = Math.min(targetWidth / naturalWidth, targetHeight / naturalHeight, 1);
  return {
    width: Math.max(1, Math.round(naturalWidth * scale)),
    height: Math.max(1, Math.round(naturalHeight * scale)),
  };
}

function loadImageElement(sourceUri: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const image = document.createElement('img');
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error(`이미지 최적화 로드 실패: ${sourceUri}`));
    image.src = sourceUri;
  });
}

function canvasToBlob(canvas: HTMLCanvasElement, mimeType: string, quality: number): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (!blob) {
        reject(new Error('이미지 최적화 blob 생성 실패'));
        return;
      }

      resolve(blob);
    }, mimeType, quality);
  });
}

function writeBlobToTizenStorage(virtualPath: string, blob: Blob): Promise<void> {
  const openFile = window.tizen?.filesystem?.openFile;
  if (!openFile) {
    throw new Error('Tizen FileSystem openFile API를 사용할 수 없습니다.');
  }

  return new Promise((resolve, reject) => {
    openFile(
      virtualPath,
      'w',
      (file) => {
        const close = () => {
          if (file.closeNonBlocking) {
            file.closeNonBlocking(resolve, reject);
            return;
          }

          file.close?.();
          resolve();
        };
        const fail = (error: unknown) => {
          file.close?.();
          reject(error);
        };

        if (file.writeBlobNonBlocking) {
          file.writeBlobNonBlocking(blob, close, fail);
          return;
        }

        file.writeBlob?.(blob);
        close();
      },
      reject,
      true,
    );
  });
}

function downloadFtpToTizenStorage(target: DownloadTarget): Promise<string> {
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
      ],
      'SINGLE',
    );

    launchAppControl(
      appControl,
      FTP_DOWNLOADER_APP_ID,
      () => undefined,
      (error) => reject(new Error(
        `FTP 다운로드 서비스 실행 실패: ${formatDownloadError(error)}; path=${sanitizeFtpPath(ftpSource)}`,
      )),
      {
        onsuccess: (data) => {
          const reply = readApplicationControlData(data);
          if (reply.status === 'ok' && reply.path) {
            resolve(reply.path);
            return;
          }

          reject(new Error(
            `FTP 콘텐츠 다운로드 실패: ${target.fileName} (${reply.error || 'unknown'}; path=${sanitizeFtpPath(ftpSource)})`,
          ));
        },
        onfailure: () => reject(new Error(
          `FTP 콘텐츠 다운로드 실패: ${target.fileName} (service failure; path=${sanitizeFtpPath(ftpSource)})`,
        )),
      },
    );
  });
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
