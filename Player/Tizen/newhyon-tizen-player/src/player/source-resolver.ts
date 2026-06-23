const TIZEN_VIRTUAL_ROOT_PREFIXES = [
  'wgt-package/',
  'wgt-private/',
  'wgt-private-tmp/',
  'documents/',
  'downloads/',
  'images/',
  'music/',
  'videos/',
  'camera/',
  'removable_',
] as const;

interface FileSystemLike {
  toURI(path: string): string;
  pathExists?(path: string): boolean;
}

function hasUriScheme(sourceUrl: string): boolean {
  return /^[a-zA-Z][a-zA-Z\d+.-]*:/.test(sourceUrl);
}

function isRemoteUriProtocol(protocol: string): boolean {
  return protocol === 'http:' || protocol === 'https:';
}

function isSupportedVirtualPath(sourceUrl: string): boolean {
  return TIZEN_VIRTUAL_ROOT_PREFIXES.some((prefix) => sourceUrl.startsWith(prefix));
}

function normalizeVirtualPath(pathname: string): string {
  return pathname.replace(/^\/+/, '');
}

function convertFileUriToAbsolutePath(fileUri: string): string {
  const resolvedUrl = new URL(fileUri);
  if (resolvedUrl.protocol !== 'file:') {
    throw new Error(`Tizen filesystem returned a non-file URI: ${fileUri}`);
  }

  return decodeURIComponent(resolvedUrl.pathname);
}

function resolveBaseLocationHref(locationHref?: string): string {
  return locationHref || window.location.href || 'http://localhost/';
}

function resolvePackagedVirtualPath(sourceUrl: string, locationHref?: string): string {
  if (isSupportedVirtualPath(sourceUrl)) {
    return sourceUrl;
  }

  if (sourceUrl.startsWith('/')) {
    return `wgt-package/${normalizeVirtualPath(sourceUrl)}`;
  }

  if (!hasUriScheme(sourceUrl)) {
    return `wgt-package/${normalizeVirtualPath(sourceUrl)}`;
  }

  const resolvedUrl = new URL(sourceUrl, resolveBaseLocationHref(locationHref));
  return `wgt-package/${normalizeVirtualPath(decodeURIComponent(resolvedUrl.pathname))}`;
}

function resolveVirtualPathToAbsolutePath(sourceUrl: string, filesystem: FileSystemLike, locationHref?: string): string {
  const virtualPath = resolvePackagedVirtualPath(sourceUrl, locationHref);
  if (typeof filesystem.pathExists === 'function' && !filesystem.pathExists(virtualPath)) {
    throw new Error(`패키지 로컬 미디어 파일을 찾지 못했습니다: ${virtualPath}`);
  }

  return convertFileUriToAbsolutePath(filesystem.toURI(virtualPath));
}

export function resolveAvplaySourceUrl(sourceUrl: string, locationHref?: string): string {
  const trimmedSourceUrl = sourceUrl.trim();
  if (!trimmedSourceUrl) {
    throw new Error('AVPlay source URL이 비어 있습니다.');
  }

  if (hasUriScheme(trimmedSourceUrl)) {
    const resolvedUrl = new URL(trimmedSourceUrl, resolveBaseLocationHref(locationHref));
    if (resolvedUrl.protocol === 'file:') {
      return decodeURIComponent(resolvedUrl.pathname);
    }

    if (isRemoteUriProtocol(resolvedUrl.protocol)) {
      return resolvedUrl.toString();
    }

    throw new Error(`AVPlay가 지원하지 않는 소스 프로토콜입니다: ${resolvedUrl.protocol}`);
  }

  const filesystem = window.tizen?.filesystem;
  if (!filesystem) {
    throw new Error('패키지 로컬 미디어는 Tizen filesystem API로 해석해야 합니다.');
  }

  return resolveVirtualPathToAbsolutePath(trimmedSourceUrl, filesystem, locationHref);
}

export function resolveImageSourceUrl(sourceUrl: string): string {
  const trimmedSourceUrl = sourceUrl.trim();
  if (!trimmedSourceUrl) {
    throw new Error('이미지 source URL이 비어 있습니다.');
  }

  if (hasUriScheme(trimmedSourceUrl)) {
    return trimmedSourceUrl;
  }

  if (isSupportedVirtualPath(trimmedSourceUrl)) {
    const filesystem = window.tizen?.filesystem;
    if (!filesystem) {
      throw new Error('Tizen 가상 저장소 이미지는 filesystem API로 해석해야 합니다.');
    }

    return filesystem.toURI(trimmedSourceUrl);
  }

  return trimmedSourceUrl;
}
