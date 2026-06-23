import { beforeEach, describe, expect, it, vi } from 'vitest';
import { resolveRuntimeConfig } from '../src/app/runtime-config';
import { DEFAULT_PLAYER_SETTINGS, savePlayerSettings } from '../src/app/player-settings';

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

  it('Manifest URL 설정이 있으면 외부 매니페스트를 불러온다', async () => {
    savePlayerSettings({
      ...DEFAULT_PLAYER_SETTINGS,
      playerId: '',
      managerAddress: '',
      manifestUrl: 'https://example.com/manifest.json',
      preserveAspectRatio: false,
      switchOnContentEnd: false,
      hudInitiallyVisible: false,
    });
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        ok: true,
        json: async () => ({
          playlistName: 'remote',
          preserveAspectRatio: true,
          pages: [],
        }),
      })),
    );

    const config = await resolveRuntimeConfig({ search: '' });

    expect(fetch).toHaveBeenCalledWith('https://example.com/manifest.json', { cache: 'no-store' });
    expect(config.manifest.playlistName).toBe('remote');
    expect(config.manifest.preserveAspectRatio).toBe(false);
  });
});
