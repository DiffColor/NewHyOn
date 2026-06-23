import type { PageInfoClass, PlayerManifest } from '../domain/models';

const REMOTE_MANIFEST_STORAGE_KEY = 'newhyon-tizen-player:remote-manifest';

export interface UpdatePayload {
  readonly PageList?: {
    readonly PLI_PageListName?: string;
  };
  readonly Pages?: PageInfoClass[];
  readonly Contract?: {
    readonly PlaylistName?: string;
  };
  readonly Schedule?: unknown;
  readonly ContentPeriodUpdateGuids?: string[];
}

interface UsableUpdatePayload extends UpdatePayload {
  readonly PageList: {
    readonly PLI_PageListName?: string;
  };
  readonly Pages: PageInfoClass[];
}

export function decodeUpdatePayload(payloadBase64: string): UpdatePayload | null {
  const raw = payloadBase64.trim();
  if (!raw) {
    return null;
  }

  const decoded = decodeBase64ToUtf8(raw);
  return parsePayload(decoded) ?? parsePayload(raw);
}

export function hasUsableUpdatePayload(payload: UpdatePayload | null): payload is UsableUpdatePayload {
  return Boolean(payload?.PageList && Array.isArray(payload.Pages));
}

export function buildManifestFromUpdatePayload(payload: UpdatePayload, preserveAspectRatio: boolean): PlayerManifest {
  if (!hasUsableUpdatePayload(payload)) {
    throw new Error('업데이트 payload에 PageList/Pages가 없습니다.');
  }

  const pageList = payload.PageList;
  const pages = payload.Pages;
  const playlistName = pageList.PLI_PageListName?.trim()
    || payload.Contract?.PlaylistName?.trim()
    || 'remote-playlist';

  return {
    playlistName,
    preserveAspectRatio,
    pages,
  };
}

export function saveRemoteManifest(manifest: PlayerManifest): void {
  window.localStorage.setItem(REMOTE_MANIFEST_STORAGE_KEY, JSON.stringify(manifest));
}

export function clearRemoteManifest(): void {
  window.localStorage.removeItem(REMOTE_MANIFEST_STORAGE_KEY);
}

export function loadRemoteManifest(): PlayerManifest | null {
  const raw = window.localStorage.getItem(REMOTE_MANIFEST_STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    if (isManifest(parsed)) {
      return parsed;
    }
  } catch {
  }

  window.localStorage.removeItem(REMOTE_MANIFEST_STORAGE_KEY);
  return null;
}

function decodeBase64ToUtf8(value: string): string {
  try {
    const binary = window.atob(value);
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index += 1) {
      bytes[index] = binary.charCodeAt(index);
    }
    return new TextDecoder().decode(bytes);
  } catch {
    return '';
  }
}

function parsePayload(value: string): UpdatePayload | null {
  if (!value.trim()) {
    return null;
  }

  try {
    const parsed = JSON.parse(value) as unknown;
    return parsed && typeof parsed === 'object' ? parsed as UpdatePayload : null;
  } catch {
    return null;
  }
}

function isManifest(value: unknown): value is PlayerManifest {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as PlayerManifest;
  return typeof candidate.playlistName === 'string'
    && typeof candidate.preserveAspectRatio === 'boolean'
    && Array.isArray(candidate.pages);
}
