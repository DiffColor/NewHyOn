import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DEFAULT_MANIFEST } from '../src/app/default-manifest';
import { resolveRuntimeConfig } from '../src/app/runtime-config';
import { DEFAULT_PLAYER_SETTINGS, savePlayerSettings } from '../src/app/player-settings';
import { saveRemoteManifest } from '../src/app/update-payload';

describe('resolveRuntimeConfig', () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.restoreAllMocks();
    window.NEWHYON_PLAYER_MANIFEST = undefined;
  });

  it('저장된 설정을 런타임 설정에 반영한다', async () => {
    savePlayerSettings({
      ...DEFAULT_PLAYER_SETTINGS,
      playerId: 'player-01',
      managerAddress: 'http://manager.local',
      manifestUrl: '',
      preserveAspectRatio: true,
      switchOnContentEnd: true,
      hudInitiallyVisible: true,
    });

    const config = await resolveRuntimeConfig({ search: '' });

    expect(config.settings.playerId).toBe('player-01');
    expect(config.manifest.preserveAspectRatio).toBe(true);
    expect(config.settings.switchOnContentEnd).toBe(true);
    expect(config.hudInitiallyVisible).toBe(true);
  });

  it('Manifest URL 설정이 있어도 저장된 매니페스트로 즉시 기동한다', async () => {
    savePlayerSettings({
      ...DEFAULT_PLAYER_SETTINGS,
      playerId: '',
      managerAddress: '',
      manifestUrl: 'https://example.com/manifest.json',
      preserveAspectRatio: false,
      switchOnContentEnd: false,
      hudInitiallyVisible: false,
    });
    const fetch = vi.fn(() => new Promise<Response>(() => undefined));
    vi.stubGlobal('fetch', fetch);

    const config = await resolveRuntimeConfig({ search: '' });

    expect(fetch).not.toHaveBeenCalled();
    expect(config.manifest.playlistName).toBe(DEFAULT_MANIFEST.playlistName);
    expect(config.manifest.preserveAspectRatio).toBe(false);
  });

  it('네트워크 상태와 무관하게 저장된 매니페스트로 기동한다', async () => {
    savePlayerSettings({
      ...DEFAULT_PLAYER_SETTINGS,
      manifestUrl: 'https://example.com/manifest.json',
      preserveAspectRatio: false,
    });
    saveRemoteManifest({
      playlistName: 'cached-local',
      preserveAspectRatio: false,
      pages: [],
    });
    const fetch = vi.fn(() => new Promise<Response>(() => undefined));
    vi.stubGlobal('fetch', fetch);

    const config = await resolveRuntimeConfig({ search: '' });

    expect(fetch).not.toHaveBeenCalled();
    expect(config.manifest.playlistName).toBe('cached-local');
  });
});
