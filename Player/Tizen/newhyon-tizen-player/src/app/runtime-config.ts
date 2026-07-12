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

export function resolveRuntimeConfig(location: Pick<Location, 'search'> = window.location): RuntimeConfig {
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

  return {
    manifest: {
      ...manifest,
      preserveAspectRatio: settings.preserveAspectRatio,
    },
    settings,
    hudInitiallyVisible: parseBoolean(params.get('hud'), settings.hudInitiallyVisible),
  };
}
