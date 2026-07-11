import { DEFAULT_MANIFEST } from './default-manifest';
import { loadPlayerSettings, type PlayerSettings } from './player-settings';
import { loadRemoteManifest } from './update-payload';
import type { PlayerManifest } from '../domain/models';

export interface RuntimeConfig {
  readonly manifest: PlayerManifest;
  readonly settings: PlayerSettings;
  readonly hudInitiallyVisible: boolean;
}

function parseBoolean(value: string | null, defaultValue: boolean): boolean {
  if (value === '1' || value === 'true') {
    return true;
  }

  if (value === '0' || value === 'false') {
    return false;
  }

  return defaultValue;
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

async function loadManifestFromUrl(url: string): Promise<PlayerManifest> {
  const response = await fetch(url, { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`매니페스트를 가져오지 못했습니다: ${response.status} ${response.statusText}`);
  }

  const json = (await response.json()) as unknown;
  if (!isManifest(json)) {
    throw new Error('매니페스트 형식이 PlayerManifest와 일치하지 않습니다.');
  }

  return json;
}

export async function resolveRuntimeConfig(location: Pick<Location, 'search'> = window.location): Promise<RuntimeConfig> {
  const params = new URLSearchParams(location.search);
  const settings = loadPlayerSettings();
  const injectedManifest = window.NEWHYON_PLAYER_MANIFEST;
  const remoteManifest = loadRemoteManifest();
  let manifest = DEFAULT_MANIFEST;

  if (isManifest(remoteManifest)) {
    manifest = remoteManifest;
  }

  if (isManifest(injectedManifest)) {
    manifest = injectedManifest;
  }

  const manifestUrl = params.get('manifest')?.trim() || settings.manifestUrl;
  if (manifestUrl) {
    try {
      manifest = await loadManifestFromUrl(manifestUrl);
    } catch {
      // USB 설치 뒤 네트워크가 없어도 이전에 저장한 콘텐츠 계획으로 기동한다.
    }
  }

  return {
    manifest: {
      ...manifest,
      preserveAspectRatio: settings.preserveAspectRatio,
    },
    settings,
    hudInitiallyVisible: parseBoolean(params.get('hud'), settings.hudInitiallyVisible),
  };
}
