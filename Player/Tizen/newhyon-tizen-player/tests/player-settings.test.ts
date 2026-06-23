import { beforeEach, describe, expect, it } from 'vitest';
import {
  clearPlayerSettings,
  DEFAULT_PLAYER_SETTINGS,
  loadPlayerSettings,
  savePlayerSettings,
  type PlayerSettings,
} from '../src/app/player-settings';

describe('player settings', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('저장된 설정을 정리해서 다시 읽는다', () => {
    const settings: PlayerSettings = {
      ...DEFAULT_PLAYER_SETTINGS,
      playerId: ' player-01 ',
      managerAddress: ' http://manager.local ',
      dataServerAddress: ' 10.0.0.10 ',
      messageServerAddress: ' 10.0.0.11 ',
      signalrPort: 5001,
      signalrHubPath: 'Data',
      ftpPort: 10022,
      ftpPasvMinPort: 24000,
      ftpPasvMaxPort: 24240,
      ftpRootPath: ' /NewHyOnEnt ',
      manifestUrl: ' https://example.com/manifest.json ',
      preserveAspectRatio: true,
      switchOnContentEnd: true,
      hudInitiallyVisible: true,
    };

    savePlayerSettings(settings);

    expect(loadPlayerSettings()).toEqual({
      playerId: 'player-01',
      managerAddress: 'http://manager.local',
      dataServerAddress: '10.0.0.10',
      messageServerAddress: '10.0.0.11',
      signalrPort: 5001,
      signalrHubPath: '/Data',
      ftpPort: 10022,
      ftpPasvMinPort: 24000,
      ftpPasvMaxPort: 24240,
      ftpRootPath: '/NewHyOnEnt',
      manifestUrl: 'https://example.com/manifest.json',
      preserveAspectRatio: true,
      switchOnContentEnd: true,
      hudInitiallyVisible: true,
    });
  });

  it('깨진 저장값은 제거하고 기본 설정으로 복구한다', () => {
    window.localStorage.setItem('newhyon-tizen-player.settings.v1', '{broken');

    expect(loadPlayerSettings()).toEqual(DEFAULT_PLAYER_SETTINGS);
    expect(window.localStorage.getItem('newhyon-tizen-player.settings.v1')).toBeNull();
  });

  it('설정을 초기화한다', () => {
    savePlayerSettings({
      ...DEFAULT_PLAYER_SETTINGS,
      playerId: 'player-01',
      managerAddress: 'http://manager.local',
      manifestUrl: '',
      preserveAspectRatio: true,
      switchOnContentEnd: true,
      hudInitiallyVisible: true,
    });

    clearPlayerSettings();

    expect(loadPlayerSettings()).toEqual(DEFAULT_PLAYER_SETTINGS);
  });
});
