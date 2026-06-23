import QRCode from 'qrcode';
import {
  LICENSEHUB_OFFLINE_CODE_CHARSET,
  LicenseHubAuthService,
  type LicenseAuthState,
  type OfflineAuthContext,
} from './licensehub-auth';

type AuthControl = HTMLButtonElement;
type AuthResolve = (state: LicenseAuthState) => void;

interface LicenseAuthOverlayOptions {
  readonly service: LicenseHubAuthService;
  readonly onStatus?: (status: string, detail: string) => void;
}

interface OtpKey {
  readonly label: string;
  readonly value?: string;
  readonly action?: 'backspace' | 'clear' | 'done';
}

const OTP_KEYS: OtpKey[] = [
  ...LICENSEHUB_OFFLINE_CODE_CHARSET.split('').map((value) => ({ label: value, value })),
  { label: '지움', action: 'backspace' },
  { label: '전체', action: 'clear' },
  { label: '완료', action: 'done' },
];

const OTP_COLUMNS = 8;

export class LicenseAuthOverlay {
  private readonly root = document.createElement('aside');
  private readonly controls: AuthControl[] = [];
  private readonly qrCanvas = document.createElement('canvas');
  private readonly title = document.createElement('h2');
  private readonly mode = document.createElement('div');
  private readonly status = document.createElement('p');
  private readonly qrPayload = document.createElement('textarea');
  private readonly otpPreview = document.createElement('div');
  private readonly otpPad = document.createElement('div');
  private readonly otpButtons: HTMLButtonElement[] = [];
  private onlineOtp = '';
  private offlineOtp = '';
  private offlineContext: OfflineAuthContext | null = null;
  private resolve: AuthResolve | null = null;
  private openState = false;

  constructor(private readonly options: LicenseAuthOverlayOptions) {
    this.root.className = 'license-auth-overlay license-auth-overlay--hidden';
    this.root.setAttribute('aria-hidden', 'true');
    document.body.appendChild(this.root);
    this.render();
  }

  get isOpen(): boolean {
    return this.openState;
  }

  open(initialMessage: string): Promise<LicenseAuthState> {
    this.root.classList.remove('license-auth-overlay--hidden');
    this.root.setAttribute('aria-hidden', 'false');
    this.openState = true;
    this.setStatus(initialMessage);
    void this.generateOnlineQr();
    this.focusControl(0);
    return new Promise((resolve) => {
      this.resolve = resolve;
    });
  }

  close(): void {
    this.root.classList.add('license-auth-overlay--hidden');
    this.root.setAttribute('aria-hidden', 'true');
    this.openState = false;
  }

  handleKeyDown(event: KeyboardEvent): boolean {
    if (!this.openState) {
      return false;
    }

    if (this.otpPad.classList.contains('license-auth-otp--visible')) {
      return this.handleOtpKeyDown(event);
    }

    if (isBackKey(event)) {
      event.preventDefault();
      this.setStatus('인증이 완료되기 전에는 인증창을 닫을 수 없습니다.', true);
      return true;
    }

    if (event.key === 'ArrowRight' || event.key === 'ArrowDown') {
      event.preventDefault();
      this.focusRelative(1);
      return true;
    }

    if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') {
      event.preventDefault();
      this.focusRelative(-1);
      return true;
    }

    if (event.key === 'Enter') {
      const active = document.activeElement;
      if (active instanceof HTMLButtonElement && active.dataset.authAction) {
        event.preventDefault();
        void this.activate(active.dataset.authAction);
        return true;
      }
    }

    return true;
  }

  private render(): void {
    this.root.textContent = '';
    const panel = document.createElement('section');
    panel.className = 'license-auth-panel';

    this.title.className = 'license-auth-title';
    this.title.textContent = 'LicenseHub 디바이스 인증';
    this.mode.className = 'license-auth-mode';
    this.status.className = 'license-auth-status';
    this.qrCanvas.className = 'license-auth-qr';
    this.qrPayload.className = 'license-auth-payload';
    this.qrPayload.readOnly = true;
    this.otpPreview.className = 'license-auth-otp-preview';
    this.otpPreview.textContent = 'OTP: -----';
    this.otpPad.className = 'license-auth-otp';
    this.renderOtpPad();

    const actions = document.createElement('div');
    actions.className = 'license-auth-actions';
    actions.append(
      this.createAction('online-qr', '온라인 QR'),
      this.createAction('online-otp', '온라인 OTP'),
      this.createAction('online-complete', '온라인 완료'),
      this.createAction('offline-start', '오프라인 QR'),
      this.createAction('offline-otp', '오프라인 OTP'),
      this.createAction('offline-proof', '증빙 QR'),
      this.createAction('offline-complete', '오프라인 완료'),
      this.createAction('revalidate', '재검증'),
    );

    panel.append(this.title, this.mode, this.status, this.qrCanvas, this.qrPayload, this.otpPreview, actions, this.otpPad);
    this.root.appendChild(panel);
    this.controls.splice(0, this.controls.length, ...Array.from(actions.querySelectorAll<HTMLButtonElement>('button')));
  }

  private renderOtpPad(): void {
    this.otpPad.textContent = '';
    this.otpButtons.splice(0);
    OTP_KEYS.forEach((key) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'license-auth-otp__key';
      button.textContent = key.label;
      if (key.value) {
        button.dataset.otpValue = key.value;
      }
      if (key.action) {
        button.dataset.otpAction = key.action;
      }
      this.otpPad.appendChild(button);
      this.otpButtons.push(button);
    });
  }

  private createAction(action: string, label: string): HTMLButtonElement {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'license-auth-action';
    button.dataset.authAction = action;
    button.textContent = label;
    return button;
  }

  private async activate(action: string): Promise<void> {
    if (action === 'online-qr') {
      await this.generateOnlineQr();
    } else if (action === 'online-otp') {
      this.openOtpPad('ONLINE');
    } else if (action === 'online-complete') {
      await this.completeOnline();
    } else if (action === 'offline-start') {
      await this.generateOfflineStart();
    } else if (action === 'offline-otp') {
      this.openOtpPad('OFFLINE');
    } else if (action === 'offline-proof') {
      await this.generateOfflineProof();
    } else if (action === 'offline-complete') {
      await this.completeOffline();
    } else if (action === 'revalidate') {
      await this.revalidate();
    }
  }

  private async generateOnlineQr(): Promise<void> {
    try {
      const result = await this.options.service.buildOnlineQr();
      this.mode.textContent = '온라인 인증';
      this.qrPayload.value = result.qrText;
      this.onlineOtp = '';
      this.updateOtpPreview('ONLINE');
      await this.renderQr(result.qrText);
      this.setStatus('온라인 QR을 모바일에서 스캔한 뒤 OTP 5자리를 입력해 주세요.');
    } catch (error) {
      this.setStatus(formatError(error), true);
    }
  }

  private async completeOnline(): Promise<void> {
    try {
      const result = await this.options.service.claimOnline(this.onlineOtp);
      if (!result.isValid) {
        this.setStatus(result.reason || '온라인 인증에 실패했습니다.', true);
        return;
      }

      this.finish(result);
    } catch (error) {
      this.setStatus(formatError(error), true);
    }
  }

  private async generateOfflineStart(): Promise<void> {
    try {
      const result = await this.options.service.buildOfflineStart();
      this.mode.textContent = '오프라인 인증 1단계';
      this.offlineContext = result.context;
      this.offlineOtp = '';
      this.qrPayload.value = result.qrText;
      this.updateOtpPreview('OFFLINE');
      await this.renderQr(result.qrText);
      this.setStatus('1단계 QR을 모바일에서 스캔한 뒤 OTP 입력 단계로 이동해 주세요.');
    } catch (error) {
      this.setStatus(formatError(error), true);
    }
  }

  private async generateOfflineProof(): Promise<void> {
    if (!this.offlineContext) {
      this.setStatus('먼저 오프라인 1단계 QR을 생성해 주세요.', true);
      return;
    }

    try {
      const result = await this.options.service.buildOfflineProof(this.offlineContext, this.offlineOtp);
      this.mode.textContent = '오프라인 인증 2단계';
      this.offlineContext = result.context;
      this.qrPayload.value = result.qrText;
      await this.renderQr(result.qrText);
      this.setStatus('2단계 증빙 QR을 모바일에서 인식한 뒤 오프라인 완료를 눌러 주세요.');
    } catch (error) {
      this.setStatus(formatError(error), true);
    }
  }

  private async completeOffline(): Promise<void> {
    if (!this.offlineContext || !this.qrPayload.value.startsWith('LH3-PROOF:')) {
      this.setStatus('먼저 오프라인 증빙 QR을 생성해 주세요.', true);
      return;
    }

    try {
      this.finish(await this.options.service.completeOffline(this.offlineContext));
    } catch (error) {
      this.setStatus(formatError(error), true);
    }
  }

  private async revalidate(): Promise<void> {
    const result = await this.options.service.validateStoredOrBootstrap();
    if (result.isValid) {
      this.finish(result);
      return;
    }

    this.setStatus(result.reason || '인증이 유효하지 않습니다.', true);
  }

  private finish(state: LicenseAuthState): void {
    this.setStatus('인증이 완료되었습니다.');
    this.options.onStatus?.(state.status, state.mode);
    this.resolve?.(state);
    this.resolve = null;
    this.close();
  }

  private async renderQr(text: string): Promise<void> {
    await QRCode.toCanvas(this.qrCanvas, text, {
      width: 300,
      margin: 1,
      color: {
        dark: '#000000',
        light: '#ffffff',
      },
      errorCorrectionLevel: 'M',
    });
  }

  private openOtpPad(mode: 'ONLINE' | 'OFFLINE'): void {
    this.otpPad.dataset.otpMode = mode;
    this.otpPad.classList.add('license-auth-otp--visible');
    this.updateOtpPreview(mode);
    this.otpButtons[0]?.focus();
  }

  private closeOtpPad(): void {
    this.otpPad.classList.remove('license-auth-otp--visible');
    this.focusControl(0);
  }

  private handleOtpKeyDown(event: KeyboardEvent): boolean {
    if (isBackKey(event)) {
      event.preventDefault();
      this.closeOtpPad();
      return true;
    }

    if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.focusRelativeOtp(1);
      return true;
    }

    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.focusRelativeOtp(-1);
      return true;
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.focusRelativeOtp(OTP_COLUMNS);
      return true;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.focusRelativeOtp(-OTP_COLUMNS);
      return true;
    }

    if (event.key === 'Enter') {
      const active = document.activeElement;
      if (active instanceof HTMLButtonElement) {
        event.preventDefault();
        this.activateOtp(active);
      }
      return true;
    }

    return true;
  }

  private activateOtp(button: HTMLButtonElement): void {
    const mode = this.otpPad.dataset.otpMode === 'OFFLINE' ? 'OFFLINE' : 'ONLINE';
    const current = mode === 'ONLINE' ? this.onlineOtp : this.offlineOtp;
    const value = button.dataset.otpValue;
    const action = button.dataset.otpAction;
    let next = current;
    if (value && current.length < 5) {
      next = current + value;
    } else if (action === 'backspace') {
      next = current.slice(0, -1);
    } else if (action === 'clear') {
      next = '';
    } else if (action === 'done') {
      this.closeOtpPad();
    }

    if (mode === 'ONLINE') {
      this.onlineOtp = next;
    } else {
      this.offlineOtp = next;
    }
    this.updateOtpPreview(mode);
  }

  private updateOtpPreview(mode: 'ONLINE' | 'OFFLINE'): void {
    const value = mode === 'ONLINE' ? this.onlineOtp : this.offlineOtp;
    this.otpPreview.textContent = `${mode} OTP: ${value.padEnd(5, '-')}`;
  }

  private setStatus(message: string, error = false): void {
    this.status.textContent = message;
    this.status.classList.toggle('license-auth-status--error', error);
    this.options.onStatus?.(error ? 'failed' : 'ready', message);
  }

  private focusControl(index: number): void {
    this.controls[Math.min(Math.max(0, index), this.controls.length - 1)]?.focus();
  }

  private focusRelative(offset: number): void {
    const index = this.controls.findIndex((control) => control === document.activeElement);
    this.focusControl(index < 0 ? 0 : (index + offset + this.controls.length) % this.controls.length);
  }

  private focusRelativeOtp(offset: number): void {
    const index = this.otpButtons.findIndex((button) => button === document.activeElement);
    const next = Math.min(Math.max(0, (index < 0 ? 0 : index) + offset), this.otpButtons.length - 1);
    this.otpButtons[next]?.focus();
  }
}

function isBackKey(event: KeyboardEvent): boolean {
  return event.key === 'Back'
    || event.key === 'BrowserBack'
    || event.key === 'Escape'
    || event.key === 'Return'
    || event.key === 'GoBack'
    || event.key === 'Exit'
    || event.keyCode === 10009
    || event.keyCode === 10182;
}

function formatError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
