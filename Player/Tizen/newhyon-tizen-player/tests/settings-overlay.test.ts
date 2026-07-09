import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SettingsOverlay } from '../src/app/settings-overlay';
import { DEFAULT_PLAYER_SETTINGS, loadPlayerSettings, savePlayerSettings } from '../src/app/player-settings';
import { loadWeeklySchedule } from '../src/app/weekly-schedule';
import { loadRemoteManifest, saveRemoteManifest } from '../src/app/update-payload';

function keyEvent(key: string): KeyboardEvent {
  return new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
}

function activeSettingName(): string | undefined {
  const active = document.querySelector<HTMLElement>('[data-setting-active="true"]');
  return active?.dataset.settingName ?? active?.dataset.settingAction;
}

function activeSettingControl(): HTMLElement | null {
  return document.querySelector<HTMLElement>('[data-setting-active="true"]');
}

describe('SettingsOverlay', () => {
  beforeEach(() => {
    document.body.textContent = '';
    window.localStorage.clear();
  });

  it('리모컨 방향키로 이동하고 Enter로 설정을 저장한다', () => {
    const onApply = vi.fn();
    const overlay = new SettingsOverlay({ onApply });

    overlay.open();
    expect(activeSettingName()).toBe('playerId');
    expect(document.activeElement).not.toBe(activeSettingControl());

    const playerIdInput = document.querySelector<HTMLInputElement>('[data-setting-name="playerId"]');
    if (!playerIdInput) {
      throw new Error('기기 이름 입력 필드를 찾지 못했습니다.');
    }
    playerIdInput.value = 'player-01';

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('managerAddress');
    const managerInput = document.querySelector<HTMLInputElement>('[data-setting-name="managerAddress"]');
    if (!managerInput) {
      throw new Error('데이터서버 입력 필드를 찾지 못했습니다.');
    }
    managerInput.value = '10.0.0.10';

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('remoteStreamingGatewayUrl');
    const remoteStreamingInput = document.querySelector<HTMLInputElement>('[data-setting-name="remoteStreamingGatewayUrl"]');
    if (!remoteStreamingInput) {
      throw new Error('원격화면 서버 입력 필드를 찾지 못했습니다.');
    }
    remoteStreamingInput.value = 'https://newhyon-web.local';

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('preserveAspectRatio');
    overlay.handleKeyDown(keyEvent('Enter'));

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('switchOnContentEnd');
    overlay.handleKeyDown(keyEvent('Enter'));

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('defaultVolume');
    const volumeSlider = document.querySelector<HTMLInputElement>('[data-setting-name="defaultVolume"]');
    expect(volumeSlider?.classList.contains('settings-control--active')).toBe(true);
    expect(volumeSlider?.classList.contains('settings-volume-slider--selected')).toBe(false);
    overlay.handleKeyDown(keyEvent('ArrowLeft'));
    expect(volumeSlider?.value).toBe('100');
    expect(activeSettingName()).toBe('switchOnContentEnd');

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('defaultVolume');
    overlay.handleKeyDown(keyEvent('Enter'));
    expect(volumeSlider?.classList.contains('settings-volume-slider--selected')).toBe(true);
    expect(document.querySelector('.remote-keypad')).toBeNull();
    overlay.handleKeyDown(keyEvent('ArrowLeft'));
    expect(volumeSlider?.value).toBe('99');
    overlay.handleKeyDown(keyEvent('ArrowRight'));
    expect(volumeSlider?.value).toBe('100');
    overlay.handleKeyDown(keyEvent('Enter'));
    expect(volumeSlider?.classList.contains('settings-volume-slider--selected')).toBe(false);
    expect(document.querySelector('.remote-keypad')).toBeNull();
    overlay.handleKeyDown(keyEvent('Enter'));
    overlay.handleKeyDown(keyEvent('ArrowLeft'));
    expect(volumeSlider?.value).toBe('99');

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('test-volume');

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('apply');
    overlay.handleKeyDown(keyEvent('Enter'));

    expect(document.querySelector('.settings-save-status')?.textContent).toBe('설정이 저장되었습니다.');
    expect(onApply).toHaveBeenCalledWith({
      ...DEFAULT_PLAYER_SETTINGS,
      playerId: 'player-01',
      managerAddress: '10.0.0.10',
      remoteStreamingGatewayUrl: 'https://newhyon-web.local',
      preserveAspectRatio: true,
      switchOnContentEnd: true,
      defaultVolume: 99,
    });
    expect(loadPlayerSettings()).toEqual({
      ...DEFAULT_PLAYER_SETTINGS,
      playerId: 'player-01',
      managerAddress: '10.0.0.10',
      remoteStreamingGatewayUrl: 'https://newhyon-web.local',
      preserveAspectRatio: true,
      switchOnContentEnd: true,
      defaultVolume: 99,
    });
  });

  it('기본 볼륨 슬라이더를 저장된 설정값으로 표시하고 테스트 재생을 요청한다', () => {
    savePlayerSettings({
      ...DEFAULT_PLAYER_SETTINGS,
      defaultVolume: 37,
    });
    const onVolumePreview = vi.fn();
    const onPlayVolumeTest = vi.fn();
    const overlay = new SettingsOverlay({
      onApply: vi.fn(),
      onVolumePreview,
      onPlayVolumeTest,
    });

    overlay.open();
    const slider = document.querySelector<HTMLInputElement>('[data-setting-name="defaultVolume"]');
    expect(slider?.value).toBe('37');

    slider!.value = '62';
    slider!.dispatchEvent(new Event('input', { bubbles: true }));
    expect(slider?.value).toBe('62');
    expect(document.querySelector('[data-volume-value="true"]')?.textContent).toBe('62');

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    overlay.handleKeyDown(keyEvent('ArrowDown'));
    overlay.handleKeyDown(keyEvent('ArrowDown'));
    overlay.handleKeyDown(keyEvent('ArrowDown'));
    overlay.handleKeyDown(keyEvent('ArrowDown'));
    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('test-volume');
    overlay.handleKeyDown(keyEvent('Enter'));

    expect(onVolumePreview).toHaveBeenLastCalledWith(62);
    expect(onPlayVolumeTest).toHaveBeenCalledWith(62);
  });

  it('Back 계열 키로 설정창을 닫는다', () => {
    const onClose = vi.fn();
    const overlay = new SettingsOverlay({ onApply: vi.fn(), onClose });

    overlay.open();
    expect(overlay.isOpen).toBe(true);

    expect(overlay.handleKeyDown(keyEvent('GoBack'))).toBe(true);

    expect(overlay.isOpen).toBe(false);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('입력 필드를 리모컨 키패드만으로 수정한다', () => {
    const overlay = new SettingsOverlay({ onApply: vi.fn() });

    overlay.open();
    expect(activeSettingName()).toBe('playerId');
    expect(document.querySelector('.remote-keypad')).toBeNull();
    expect(document.activeElement).not.toBe(activeSettingControl());

    const playerIdInput = document.querySelector<HTMLInputElement>('[data-setting-name="playerId"]');
    if (!playerIdInput) {
      throw new Error('기기 이름 입력 필드를 찾지 못했습니다.');
    }
    expect(playerIdInput.readOnly).toBe(true);
    expect(playerIdInput.inputMode).toBe('none');
    expect(playerIdInput.tabIndex).toBe(-1);

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('managerAddress');
    expect(document.querySelector('.remote-keypad')).toBeNull();
    const managerInput = document.querySelector<HTMLInputElement>('[data-setting-name="managerAddress"]');
    expect(document.activeElement).not.toBe(managerInput);
    expect(managerInput?.readOnly).toBe(true);

    overlay.handleKeyDown(keyEvent('ArrowDown'));
    expect(activeSettingName()).toBe('remoteStreamingGatewayUrl');
    const remoteStreamingInput = document.querySelector<HTMLInputElement>('[data-setting-name="remoteStreamingGatewayUrl"]');
    expect(document.activeElement).not.toBe(remoteStreamingInput);
    expect(remoteStreamingInput?.readOnly).toBe(true);

    overlay.handleKeyDown(keyEvent('ArrowUp'));
    expect(activeSettingName()).toBe('managerAddress');
    overlay.handleKeyDown(keyEvent('ArrowUp'));
    expect(activeSettingName()).toBe('playerId');

    overlay.handleKeyDown(keyEvent('Enter'));
    expect(document.querySelector('.remote-keypad')).not.toBeNull();

    overlay.handleKeyDown(keyEvent('Enter'));
    overlay.handleKeyDown(keyEvent('ArrowRight'));
    overlay.handleKeyDown(keyEvent('Enter'));

    expect(document.querySelector<HTMLInputElement>('[data-setting-name="playerId"]')?.value).toBe('tizen12');

    overlay.handleKeyDown(keyEvent('Back'));
    expect(document.querySelector('.remote-keypad')).toBeNull();
    expect(activeSettingName()).toBe('playerId');
    expect(document.activeElement).not.toBe(playerIdInput);
  });

  it('인증 상태를 표시하고 주간 스케줄을 수정해 저장한다', () => {
    const overlay = new SettingsOverlay({
      onApply: vi.fn(),
      getAuthStatusText: () => '인증 상태 : authenticated (ONLINE server-valid)',
    });

    overlay.open();

    expect(document.querySelector('.settings-status')?.textContent).toBe('인증 상태 : authenticated (ONLINE server-valid)');
    const firstDay = document.querySelector<HTMLButtonElement>('.weekly-settings__day');
    const mondayDay = document.querySelector<HTMLButtonElement>('[data-schedule-day="MON"][data-schedule-field="isOnAir"]');
    const startHour = document.querySelector<HTMLInputElement>('[data-schedule-day="MON"][data-schedule-field="startHour"]');
    const startMinute = document.querySelector<HTMLInputElement>('[data-schedule-day="MON"][data-schedule-field="startMinute"]');
    const endHour = document.querySelector<HTMLInputElement>('[data-schedule-day="MON"][data-schedule-field="endHour"]');
    const endMinute = document.querySelector<HTMLInputElement>('[data-schedule-day="MON"][data-schedule-field="endMinute"]');
    if (!firstDay || !mondayDay || !startHour || !startMinute || !endHour || !endMinute) {
      throw new Error('월요일 스케줄 컨트롤을 찾지 못했습니다.');
    }

    expect(firstDay.textContent).toBe('월요일');
    expect(mondayDay.className).toBe('weekly-settings__day');
    expect(mondayDay.getAttribute('aria-pressed')).toBe('true');
    mondayDay.focus();
    overlay.handleKeyDown(keyEvent('Enter'));
    expect(mondayDay.textContent).toBe('월요일');
    expect(mondayDay.getAttribute('aria-pressed')).toBe('false');
    expect(mondayDay.getAttribute('aria-label')).toBe('월요일 방송 꺼짐');
    startHour.value = '9';
    startMinute.value = '15';
    endHour.value = '18';
    endMinute.value = '30';

    document.querySelector<HTMLButtonElement>('[data-setting-action="apply"]')?.focus();
    overlay.handleKeyDown(keyEvent('Enter'));

    expect(loadWeeklySchedule()[0]).toMatchObject({
      dayCode: 'MON',
      isOnAir: false,
      startHour: 9,
      startMinute: 15,
      endHour: 18,
      endMinute: 30,
    });
  });

  it('초기화는 저장된 콘텐츠 매니페스트를 지우고 인증 데이터는 건드리지 않는다', () => {
    const overlay = new SettingsOverlay({ onApply: vi.fn() });
    saveRemoteManifest({
      playlistName: 'stale-list',
      preserveAspectRatio: false,
      pages: [],
    });
    window.localStorage.setItem('newhyon-tizen-player.licensehub.v1', 'license-token');

    overlay.open();
    document.querySelector<HTMLButtonElement>('[data-setting-action="reset"]')?.focus();
    overlay.handleKeyDown(keyEvent('Enter'));

    expect(loadRemoteManifest()).toBeNull();
    expect(window.localStorage.getItem('newhyon-tizen-player.licensehub.v1')).toBe('license-token');
  });
});
