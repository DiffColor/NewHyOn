import {
  clearPlayerSettings,
  DEFAULT_PLAYER_SETTINGS,
  loadPlayerSettings,
  normalizeVolume,
  savePlayerSettings,
  type PlayerSettings,
} from './player-settings';
import {
  getDefaultWeeklySchedule,
  loadWeeklySchedule,
  saveWeeklySchedule,
  type WeeklyDayCode,
  type WeeklyScheduleRow,
} from './weekly-schedule';
import { clearRemoteManifest } from './update-payload';

type SettingControl = HTMLInputElement | HTMLButtonElement;
type KeypadAction = 'backspace' | 'clear' | 'space' | 'done' | 'cancel';
type WeeklyScheduleField = 'startHour' | 'startMinute' | 'endHour' | 'endMinute';
type SettingsCloseReason = 'button' | 'return-key' | 'auth' | 'programmatic';

interface KeypadKey {
  readonly label: string;
  readonly value?: string;
  readonly action?: KeypadAction;
}

interface SettingsOverlayOptions {
  readonly onApply: (settings: PlayerSettings) => void;
  readonly onClose?: (reason: SettingsCloseReason) => void;
  readonly onAuthenticate?: () => void;
  readonly getCurrentVolume?: () => number | null;
  readonly onVolumePreview?: (volume: number) => void;
  readonly onPlayVolumeTest?: (volume: number) => void;
  readonly getAuthStatusText?: () => string;
}

const KEYPAD_COLUMNS = 10;
const REMOTE_KEYPAD_KEYS: KeypadKey[] = [
  ...'1234567890'.split('').map((value) => ({ label: value, value })),
  ...'qwertyuiop'.split('').map((value) => ({ label: value, value })),
  ...'asdfghjkl.'.split('').map((value) => ({ label: value, value })),
  ...'zxcvbnm-_/'.split('').map((value) => ({ label: value, value })),
  { label: ':', value: ':' },
  { label: '?', value: '?' },
  { label: '&', value: '&' },
  { label: '=', value: '=' },
  { label: '%', value: '%' },
  { label: '#', value: '#' },
  { label: '@', value: '@' },
  { label: '지움', action: 'backspace' },
  { label: '전체', action: 'clear' },
  { label: '공백', action: 'space' },
  { label: '완료', action: 'done' },
  { label: '취소', action: 'cancel' },
];

function createRow(label: string, control: HTMLElement): HTMLLabelElement {
  const row = document.createElement('label');
  row.className = 'settings-row';
  const text = document.createElement('span');
  text.className = 'settings-row__label';
  text.textContent = label;
  row.append(text, control);
  return row;
}

function createInput(name: keyof PlayerSettings, value: string, placeholder: string): HTMLInputElement {
  const input = document.createElement('input');
  input.className = 'settings-input';
  input.dataset.settingControl = 'true';
  input.dataset.settingName = name;
  input.tabIndex = -1;
  input.type = 'text';
  input.inputMode = 'none';
  input.readOnly = true;
  input.value = value;
  input.placeholder = placeholder;
  input.autocomplete = 'off';
  input.spellcheck = false;
  return input;
}

function createToggle(name: keyof PlayerSettings, active: boolean): HTMLButtonElement {
  const button = document.createElement('button');
  button.className = 'settings-toggle';
  button.dataset.settingControl = 'true';
  button.dataset.settingName = name;
  button.type = 'button';
  button.setAttribute('aria-pressed', String(active));
  button.textContent = active ? 'ON' : 'OFF';
  return button;
}

function createPlaybackOptionsRow(aspectToggle: HTMLButtonElement, switchOnEndToggle: HTMLButtonElement): HTMLElement {
  const row = document.createElement('div');
  row.className = 'settings-row settings-row--inline';

  const label = document.createElement('span');
  label.className = 'settings-row__label';
  label.textContent = '재생 옵션';

  const controls = document.createElement('div');
  controls.className = 'settings-inline-controls';

  const aspect = document.createElement('label');
  aspect.className = 'settings-inline-toggle';
  const aspectLabel = document.createElement('span');
  aspectLabel.textContent = '화면 비율 유지';
  aspect.append(aspectLabel, aspectToggle);

  const switchOnEnd = document.createElement('label');
  switchOnEnd.className = 'settings-inline-toggle';
  const switchOnEndLabel = document.createElement('span');
  switchOnEndLabel.textContent = '콘텐츠 종료 시 전환';
  switchOnEnd.append(switchOnEndLabel, switchOnEndToggle);

  controls.append(aspect, switchOnEnd);
  row.append(label, controls);
  return row;
}

function createVolumeSlider(volume: number): HTMLElement {
  const row = document.createElement('div');
  row.className = 'settings-row settings-row--volume';

  const label = document.createElement('label');
  label.className = 'settings-row__label';
  label.htmlFor = 'settings-default-volume';
  label.textContent = '기본 볼륨';

  const controls = document.createElement('div');
  controls.className = 'settings-volume-control';

  const slider = document.createElement('input');
  slider.id = 'settings-default-volume';
  slider.className = 'settings-volume-slider';
  slider.dataset.settingControl = 'true';
  slider.dataset.settingName = 'defaultVolume';
  slider.tabIndex = -1;
  slider.type = 'range';
  slider.min = '0';
  slider.max = '100';
  slider.step = '1';
  slider.value = String(normalizeVolume(volume));
  slider.setAttribute('aria-label', '기본 볼륨');

  const value = document.createElement('span');
  value.className = 'settings-volume-value';
  value.dataset.volumeValue = 'true';
  value.textContent = slider.value;

  controls.append(slider, value, createAction('test-volume', '볼륨 테스트'));
  row.append(label, controls);
  return row;
}

function createAction(action: string, text: string): HTMLButtonElement {
  const button = document.createElement('button');
  button.className = 'settings-action';
  button.dataset.settingControl = 'true';
  button.dataset.settingAction = action;
  button.type = 'button';
  button.textContent = text;
  return button;
}

function createScheduleInput(dayCode: WeeklyDayCode, field: WeeklyScheduleField, value: number): HTMLInputElement {
  const input = document.createElement('input');
  input.className = 'settings-input settings-input--time';
  input.dataset.settingControl = 'true';
  input.dataset.scheduleDay = dayCode;
  input.dataset.scheduleField = field;
  input.tabIndex = -1;
  input.type = 'text';
  input.inputMode = 'none';
  input.readOnly = true;
  input.value = String(value).padStart(2, '0');
  input.autocomplete = 'off';
  input.spellcheck = false;
  return input;
}

function createScheduleDayButton(row: WeeklyScheduleRow): HTMLButtonElement {
  const button = document.createElement('button');
  button.className = 'weekly-settings__day';
  button.dataset.settingControl = 'true';
  button.dataset.scheduleDay = row.dayCode;
  button.dataset.scheduleField = 'isOnAir';
  button.type = 'button';
  button.setAttribute('aria-pressed', String(row.isOnAir));
  button.setAttribute('aria-label', `${row.dayLabel} 방송 ${row.isOnAir ? '켜짐' : '꺼짐'}`);
  button.textContent = row.dayLabel;
  return button;
}

export class SettingsOverlay {
  private readonly root = document.createElement('aside');
  private readonly controls: SettingControl[] = [];
  private readonly keypadButtons: HTMLButtonElement[] = [];
  private keypadRoot: HTMLElement | null = null;
  private keypadPreview: HTMLElement | null = null;
  private keypadInput: HTMLInputElement | null = null;
  private saveStatus: HTMLElement | null = null;
  private selectedControlIndex = 0;
  private openState = false;
  private tvVolumeListenerBound = false;

  constructor(private readonly options: SettingsOverlayOptions) {
    this.root.className = 'settings-overlay settings-overlay--hidden';
    this.root.setAttribute('aria-hidden', 'true');
    document.body.appendChild(this.root);
    this.render(loadPlayerSettings());
  }

  get isOpen(): boolean {
    return this.openState;
  }

  open(): void {
    this.render(loadPlayerSettings());
    this.root.classList.remove('settings-overlay--hidden');
    this.root.setAttribute('aria-hidden', 'false');
    this.openState = true;
    this.bindTvVolumeSync();
    this.focusControl(0);
  }

  close(reason: SettingsCloseReason = 'programmatic'): void {
    this.unbindTvVolumeSync();
    this.closeKeypad();
    this.root.classList.add('settings-overlay--hidden');
    this.root.setAttribute('aria-hidden', 'true');
    this.openState = false;
    this.options.onClose?.(reason);
  }

  handleKeyDown(event: KeyboardEvent): boolean {
    if (!this.openState) {
      return false;
    }

    this.consumeKeyEvent(event);
    if (this.keypadRoot) {
      return this.handleKeypadKeyDown(event);
    }

    if (
      event.key === 'Back'
      || event.key === 'BrowserBack'
      || event.key === 'Escape'
      || event.key === 'Return'
      || event.key === 'GoBack'
      || event.key === 'Exit'
      || event.keyCode === 10009
      || event.keyCode === 10182
    ) {
      event.preventDefault();
      this.close('return-key');
      return true;
    }

    if (event.key === 'ArrowDown' || event.key === 'ArrowRight') {
      const active = this.selectedControl();
      if (active instanceof HTMLInputElement && active.type === 'range' && event.key === 'ArrowRight') {
        event.preventDefault();
        this.adjustVolumeSlider(active, 1);
        return true;
      }
      event.preventDefault();
      this.focusRelative(1);
      return true;
    }

    if (event.key === 'ArrowUp' || event.key === 'ArrowLeft') {
      const active = this.selectedControl();
      if (active instanceof HTMLInputElement && active.type === 'range' && event.key === 'ArrowLeft') {
        event.preventDefault();
        this.adjustVolumeSlider(active, -1);
        return true;
      }
      event.preventDefault();
      this.focusRelative(-1);
      return true;
    }

    if (event.key === 'Enter') {
      const active = this.selectedControl();
      if (active instanceof HTMLInputElement && active.dataset.settingControl === 'true') {
        event.preventDefault();
        this.openKeypad(active);
        return true;
      }

      if (active instanceof HTMLButtonElement && active.dataset.settingControl === 'true') {
        event.preventDefault();
        this.activateButton(active);
        return true;
      }
    }

    return false;
  }

  private consumeKeyEvent(event: KeyboardEvent): void {
    event.preventDefault();
    event.stopPropagation();
    event.stopImmediatePropagation();
  }

  private render(settings: PlayerSettings): void {
    this.root.textContent = '';
    this.controls.splice(0);
    this.selectedControlIndex = 0;

    const panel = document.createElement('section');
    panel.className = 'settings-panel';

    const title = document.createElement('h2');
    title.className = 'settings-title';
    title.textContent = '기기 설정';

    const playerIdInput = createInput('playerId', settings.playerId, 'PLAYER-01');
    const managerInput = createInput('managerAddress', settings.managerAddress, '10.0.0.10 또는 10.0.0.10:8181');
    const aspectToggle = createToggle('preserveAspectRatio', settings.preserveAspectRatio);
    const switchOnEndToggle = createToggle('switchOnContentEnd', settings.switchOnContentEnd);
    const currentVolume = this.options.getCurrentVolume?.();
    const volumeRow = createVolumeSlider(currentVolume ?? settings.defaultVolume);
    const authStatus = document.createElement('div');
    authStatus.className = 'settings-status';
    authStatus.textContent = this.options.getAuthStatusText?.() ?? '인증 상태 : 미확인';

    const saveStatus = document.createElement('div');
    saveStatus.className = 'settings-save-status';
    saveStatus.setAttribute('role', 'status');
    saveStatus.setAttribute('aria-live', 'polite');
    this.saveStatus = saveStatus;

    const actionBar = document.createElement('div');
    actionBar.className = 'settings-actions';
    actionBar.append(createAction('apply', '적용'), createAction('reset', '초기화'), createAction('close', '닫기'));
    if (this.options.onAuthenticate) {
      actionBar.append(createAction('auth', '인증'));
    }

    panel.append(
      title,
      authStatus,
      saveStatus,
      createRow('기기 이름', playerIdInput),
      createRow('데이터서버', managerInput),
      createPlaybackOptionsRow(aspectToggle, switchOnEndToggle),
      volumeRow,
      actionBar,
      this.createWeeklySchedulePanel(loadWeeklySchedule()),
    );
    this.root.appendChild(panel);

    this.controls.push(
      ...Array.from(this.root.querySelectorAll<SettingControl>('[data-setting-control="true"]')),
    );
    this.controls.forEach((control, index) => {
      if (control instanceof HTMLButtonElement) {
        control.addEventListener('focus', () => {
          this.markSelectedControl(index);
        });
      }
      if (control instanceof HTMLInputElement && control.type === 'range') {
        control.addEventListener('input', () => {
          this.applyVolumeSlider(control);
        });
      }
    });
  }

  private focusControl(index: number): void {
    const normalizedIndex = Math.min(Math.max(0, index), this.controls.length - 1);
    const control = this.controls[normalizedIndex];
    if (!control) {
      return;
    }

    this.markSelectedControl(normalizedIndex);
  }

  private markSelectedControl(index: number): void {
    const normalizedIndex = Math.min(Math.max(0, index), this.controls.length - 1);
    const control = this.controls[normalizedIndex];
    if (!control) {
      return;
    }

    this.controls.forEach((candidate) => {
      candidate.classList.remove('settings-control--active');
      delete candidate.dataset.settingActive;
    });

    this.selectedControlIndex = normalizedIndex;
    control.classList.add('settings-control--active');
    control.dataset.settingActive = 'true';
  }

  private focusRelative(offset: number): void {
    const nextIndex = (this.selectedControlIndex + offset + this.controls.length) % this.controls.length;
    this.focusControl(nextIndex);
  }

  private adjustVolumeSlider(slider: HTMLInputElement, delta: number): void {
    slider.value = String(normalizeVolume(Number.parseInt(slider.value, 10) + delta));
    this.applyVolumeSlider(slider);
  }

  private applyVolumeSlider(slider: HTMLInputElement): void {
    const volume = normalizeVolume(slider.value);
    this.updateVolumeSlider(volume);
    this.options.onVolumePreview?.(volume);
  }

  private updateVolumeSlider(volume: number): void {
    const normalizedVolume = normalizeVolume(volume);
    const slider = this.root.querySelector<HTMLInputElement>('[data-setting-name="defaultVolume"]');
    if (slider) {
      slider.value = String(normalizedVolume);
    }
    const value = this.root.querySelector<HTMLElement>('[data-volume-value="true"]');
    if (value) {
      value.textContent = String(normalizedVolume);
    }
  }

  private bindTvVolumeSync(): void {
    const audioControl = window.tizen?.tvaudiocontrol;
    if (this.tvVolumeListenerBound || typeof audioControl?.setVolumeChangeListener !== 'function') {
      return;
    }

    audioControl.setVolumeChangeListener((volume) => {
      if (this.openState) {
        this.updateVolumeSlider(volume);
      }
    });
    this.tvVolumeListenerBound = true;
  }

  private unbindTvVolumeSync(): void {
    if (!this.tvVolumeListenerBound) {
      return;
    }

    window.tizen?.tvaudiocontrol?.unsetVolumeChangeListener?.();
    this.tvVolumeListenerBound = false;
  }

  private currentVolumeValue(): number {
    const slider = this.root.querySelector<HTMLInputElement>('[data-setting-name="defaultVolume"]');
    return normalizeVolume(slider?.value);
  }

  private selectedControl(): SettingControl | null {
    return this.controls[this.selectedControlIndex] ?? null;
  }

  private openKeypad(input: HTMLInputElement): void {
    this.closeKeypad();
    this.keypadInput = input;

    const keypad = document.createElement('section');
    keypad.className = 'remote-keypad';

    const title = document.createElement('h3');
    title.className = 'remote-keypad__title';
    title.textContent = `${this.labelForInput(input)} 입력`;

    const preview = document.createElement('div');
    preview.className = 'remote-keypad__preview';
    preview.textContent = input.value || input.placeholder;
    this.keypadPreview = preview;

    const grid = document.createElement('div');
    grid.className = 'remote-keypad__grid';

    this.keypadButtons.splice(0);
    REMOTE_KEYPAD_KEYS.forEach((key) => {
      const button = document.createElement('button');
      button.className = 'remote-keypad__key';
      button.type = 'button';
      button.textContent = key.label;
      if (key.value !== undefined) {
        button.dataset.keypadValue = key.value;
      }
      if (key.action) {
        button.dataset.keypadAction = key.action;
      }
      grid.appendChild(button);
      this.keypadButtons.push(button);
    });

    keypad.append(title, preview, grid);
    this.root.appendChild(keypad);
    this.keypadRoot = keypad;
    this.focusKeypadButton(0);
  }

  private labelForInput(input: HTMLInputElement): string {
    const row = input.closest('.settings-row');
    return row?.querySelector('.settings-row__label')?.textContent ?? '값';
  }

  private closeKeypad(): void {
    this.keypadRoot?.remove();
    this.keypadRoot = null;
    this.keypadPreview = null;
    const input = this.keypadInput;
    this.keypadInput = null;
    this.keypadButtons.splice(0);
    if (input) {
      const inputIndex = this.controls.findIndex((control) => control === input);
      this.focusControl(inputIndex < 0 ? this.selectedControlIndex : inputIndex);
    }
  }

  private handleKeypadKeyDown(event: KeyboardEvent): boolean {
    if (
      event.key === 'Back'
      || event.key === 'BrowserBack'
      || event.key === 'Escape'
      || event.key === 'Return'
      || event.key === 'GoBack'
      || event.key === 'Exit'
      || event.keyCode === 10009
      || event.keyCode === 10182
    ) {
      event.preventDefault();
      this.closeKeypad();
      return true;
    }

    if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.focusRelativeKeypadButton(1);
      return true;
    }

    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.focusRelativeKeypadButton(-1);
      return true;
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.focusRelativeKeypadButton(KEYPAD_COLUMNS);
      return true;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.focusRelativeKeypadButton(-KEYPAD_COLUMNS);
      return true;
    }

    if (event.key === 'Enter') {
      const active = document.activeElement;
      if (active instanceof HTMLButtonElement && active.classList.contains('remote-keypad__key')) {
        event.preventDefault();
        this.activateKeypadButton(active);
        return true;
      }
    }

    return true;
  }

  private focusKeypadButton(index: number): void {
    const normalizedIndex = Math.min(Math.max(0, index), this.keypadButtons.length - 1);
    this.keypadButtons[normalizedIndex]?.focus();
  }

  private focusRelativeKeypadButton(offset: number): void {
    const activeIndex = this.keypadButtons.findIndex((button) => button === document.activeElement);
    const nextIndex = activeIndex < 0 ? 0 : Math.min(Math.max(0, activeIndex + offset), this.keypadButtons.length - 1);
    this.focusKeypadButton(nextIndex);
  }

  private activateKeypadButton(button: HTMLButtonElement): void {
    const input = this.keypadInput;
    if (!input) {
      return;
    }

    const value = button.dataset.keypadValue;
    if (value !== undefined) {
      input.value += value;
      this.updateKeypadPreview();
      return;
    }

    const action = button.dataset.keypadAction as KeypadAction | undefined;
    if (action === 'backspace') {
      input.value = input.value.slice(0, -1);
      this.updateKeypadPreview();
      return;
    }

    if (action === 'clear') {
      input.value = '';
      this.updateKeypadPreview();
      return;
    }

    if (action === 'space') {
      input.value += ' ';
      this.updateKeypadPreview();
      return;
    }

    if (action === 'done' || action === 'cancel') {
      this.closeKeypad();
    }
  }

  private updateKeypadPreview(): void {
    if (!this.keypadPreview || !this.keypadInput) {
      return;
    }

    this.keypadPreview.textContent = this.keypadInput.value || this.keypadInput.placeholder;
  }

  private activateButton(button: HTMLButtonElement): void {
    const settingName = button.dataset.settingName;
    if (
      settingName === 'preserveAspectRatio'
      || settingName === 'switchOnContentEnd'
    ) {
      const nextValue = button.getAttribute('aria-pressed') !== 'true';
      button.setAttribute('aria-pressed', String(nextValue));
      button.textContent = nextValue ? 'ON' : 'OFF';
      return;
    }

    if (button.dataset.scheduleField === 'isOnAir') {
      const nextValue = button.getAttribute('aria-pressed') !== 'true';
      button.setAttribute('aria-pressed', String(nextValue));
      button.setAttribute('aria-label', `${button.textContent ?? '요일'} 방송 ${nextValue ? '켜짐' : '꺼짐'}`);
      return;
    }

    const action = button.dataset.settingAction;
    if (action === 'apply') {
      const settings = this.collectSettings();
      savePlayerSettings(settings);
      saveWeeklySchedule(this.collectWeeklySchedule());
      this.showSaveStatus('설정이 저장되었습니다.');
      this.options.onApply(settings);
      return;
    }

    if (action === 'test-volume') {
      const volume = this.currentVolumeValue();
      this.options.onVolumePreview?.(volume);
      this.options.onPlayVolumeTest?.(volume);
      return;
    }

    if (action === 'reset') {
      clearPlayerSettings();
      clearRemoteManifest();
      saveWeeklySchedule(getDefaultWeeklySchedule());
      this.render(DEFAULT_PLAYER_SETTINGS);
      this.showSaveStatus('설정이 초기화되었습니다.');
      this.focusControl(0);
      return;
    }

    if (action === 'close') {
      this.close('button');
      return;
    }

    if (action === 'auth') {
      this.close('auth');
      this.options.onAuthenticate?.();
    }
  }

  private showSaveStatus(message: string): void {
    if (!this.saveStatus) {
      return;
    }

    this.saveStatus.textContent = message;
    this.saveStatus.classList.add('settings-save-status--visible');
  }

  private collectSettings(): PlayerSettings {
    const current = loadPlayerSettings();
    const playerId = this.root.querySelector<HTMLInputElement>('[data-setting-name="playerId"]')?.value.trim() ?? '';
    const managerAddress = this.root.querySelector<HTMLInputElement>('[data-setting-name="managerAddress"]')?.value.trim() ?? '';
    const preserveAspectRatio =
      this.root.querySelector<HTMLButtonElement>('[data-setting-name="preserveAspectRatio"]')?.getAttribute('aria-pressed') === 'true';
    const switchOnContentEnd =
      this.root.querySelector<HTMLButtonElement>('[data-setting-name="switchOnContentEnd"]')?.getAttribute('aria-pressed') === 'true';
    const defaultVolume = this.currentVolumeValue();

    return {
      ...current,
      playerId,
      managerAddress,
      preserveAspectRatio,
      switchOnContentEnd,
      defaultVolume,
    };
  }

  private createWeeklySchedulePanel(rows: readonly WeeklyScheduleRow[]): HTMLElement {
    const section = document.createElement('section');
    section.className = 'weekly-settings';

    const title = document.createElement('h3');
    title.className = 'weekly-settings__title';
    title.textContent = '주간 스케줄';

    const grid = document.createElement('div');
    grid.className = 'weekly-settings__grid';
    ['요일', '시작', '종료'].forEach((label) => {
      const header = document.createElement('span');
      header.className = 'weekly-settings__header';
      header.textContent = label;
      grid.appendChild(header);
    });

    rows.forEach((row) => {
      const start = document.createElement('div');
      start.className = 'weekly-settings__time';
      start.append(
        createScheduleInput(row.dayCode, 'startHour', row.startHour),
        createScheduleInput(row.dayCode, 'startMinute', row.startMinute),
      );

      const end = document.createElement('div');
      end.className = 'weekly-settings__time';
      end.append(
        createScheduleInput(row.dayCode, 'endHour', row.endHour),
        createScheduleInput(row.dayCode, 'endMinute', row.endMinute),
      );

      grid.append(createScheduleDayButton(row), start, end);
    });

    section.append(title, grid);
    return section;
  }

  private collectWeeklySchedule(): WeeklyScheduleRow[] {
    return getDefaultWeeklySchedule().map((defaultRow) => {
      const isOnAir =
        this.root.querySelector<HTMLButtonElement>(
          `[data-schedule-day="${defaultRow.dayCode}"][data-schedule-field="isOnAir"]`,
        )?.getAttribute('aria-pressed') === 'true';

      return {
        ...defaultRow,
        isOnAir,
        startHour: this.readScheduleTime(defaultRow.dayCode, 'startHour', 23),
        startMinute: this.readScheduleTime(defaultRow.dayCode, 'startMinute', 59),
        endHour: this.readScheduleTime(defaultRow.dayCode, 'endHour', 23),
        endMinute: this.readScheduleTime(defaultRow.dayCode, 'endMinute', 59),
      };
    });
  }

  private readScheduleTime(dayCode: WeeklyDayCode, field: WeeklyScheduleField, max: number): number {
    const raw = this.root.querySelector<HTMLInputElement>(
      `[data-schedule-day="${dayCode}"][data-schedule-field="${field}"]`,
    )?.value ?? '';
    const parsed = Number.parseInt(raw, 10);
    if (!Number.isFinite(parsed)) {
      return 0;
    }

    return Math.min(Math.max(0, Math.round(parsed)), max);
  }
}
