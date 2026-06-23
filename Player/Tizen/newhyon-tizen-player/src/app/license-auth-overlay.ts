import QRCode from 'qrcode';
import {
  LICENSEHUB_OFFLINE_CODE_CHARSET,
  LicenseHubAuthService,
  type LicenseAuthState,
  type OfflineAuthContext,
} from './licensehub-auth';

type AuthMode = 'ONLINE' | 'OFFLINE';
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

interface StepButton {
  readonly button: HTMLButtonElement;
  readonly enabled: boolean;
  readonly reached: boolean;
  readonly active: boolean;
}

const OTP_KEYS: OtpKey[] = [
  ...LICENSEHUB_OFFLINE_CODE_CHARSET.split('').map((value) => ({ label: value, value })),
  { label: '지움', action: 'backspace' },
  { label: '전체', action: 'clear' },
  { label: '완료', action: 'done' },
];

const OTP_COLUMNS = 8;
const OTP_LENGTH = 5;

export class LicenseAuthOverlay {
  private readonly root = document.createElement('aside');
  private readonly controls: AuthControl[] = [];
  private readonly title = document.createElement('h2');
  private readonly onlineModeButton = this.createModeButton('ONLINE', '온라인 인증');
  private readonly offlineModeButton = this.createModeButton('OFFLINE', '오프라인 인증');
  private readonly onlineContent = document.createElement('section');
  private readonly offlineContent = document.createElement('section');
  private readonly onlineQrPanel = document.createElement('div');
  private readonly onlineOtpPanel = document.createElement('div');
  private readonly onlineCompletionPanel = document.createElement('div');
  private readonly onlineCompletionText = document.createElement('div');
  private readonly offlineQrPanel = document.createElement('div');
  private readonly offlineOtpPanel = document.createElement('div');
  private readonly onlineQrCanvas = document.createElement('canvas');
  private readonly offlineStartQrCanvas = document.createElement('canvas');
  private readonly offlineProofQrCanvas = document.createElement('canvas');
  private readonly onlineGuideText = document.createElement('p');
  private readonly offlineGuideText = document.createElement('p');
  private readonly onlineOtpBoxes = this.createOtpBoxes();
  private readonly offlineOtpBoxes = this.createOtpBoxes();
  private readonly onlineStep1Button = this.createStepButton('online-step-1', '1단계 QR 재생성');
  private readonly onlineStep2Button = this.createStepButton('online-step-2', 'OTP 입력');
  private readonly onlineStep3Button = this.createStepButton('online-step-3', '인증 완료');
  private readonly offlineStep1Button = this.createStepButton('offline-step-1', '1단계 QR 재생성');
  private readonly offlineStep2Button = this.createStepButton('offline-step-2', 'OTP 입력');
  private readonly offlineStep3Button = this.createStepButton('offline-step-3', '2단계 QR 생성');
  private readonly offlineStep4Button = this.createStepButton('offline-step-4', '완료');
  private readonly onlineStepLine1 = this.createStepLine();
  private readonly onlineStepLine2 = this.createStepLine();
  private readonly offlineStepLine1 = this.createStepLine();
  private readonly offlineStepLine2 = this.createStepLine();
  private readonly offlineStepLine3 = this.createStepLine();
  private readonly status = document.createElement('p');
  private readonly otpPad = document.createElement('div');
  private readonly otpButtons: HTMLButtonElement[] = [];
  private activeMode: AuthMode = 'ONLINE';
  private onlineOtp = '';
  private offlineOtp = '';
  private offlineContext: OfflineAuthContext | null = null;
  private resolve: AuthResolve | null = null;
  private openState = false;
  private onlineQrPayload = '';
  private offlineStartQrPayload = '';
  private offlineProofQrPayload = '';
  private onlineStep2Enabled = false;
  private onlineStep3Enabled = false;
  private offlineStep2Enabled = false;
  private offlineStep3Enabled = false;
  private offlineStep4Enabled = false;

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
    this.setSelectedMode('ONLINE');
    this.setStatus(initialMessage);
    this.initializeVisualFlow();
    void this.generateOnlineQr(true);
    this.focusControl(0);
    return new Promise((resolve) => {
      this.resolve = resolve;
    });
  }

  close(): void {
    this.root.classList.add('license-auth-overlay--hidden');
    this.root.setAttribute('aria-hidden', 'true');
    this.openState = false;
    this.closeOtpPad();
  }

  handleKeyDown(event: KeyboardEvent): boolean {
    if (!this.openState) {
      return false;
    }

    if (this.otpPad.classList.contains('license-auth-otp--visible')) {
      if (isBackKey(event)) {
        event.preventDefault();
        this.cancelAuthentication();
        return true;
      }

      return this.handleOtpKeyDown(event);
    }

    if (isBackKey(event)) {
      event.preventDefault();
      this.cancelAuthentication();
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
    this.title.textContent = '디바이스 인증';

    const modeToggle = document.createElement('div');
    modeToggle.className = 'license-auth-mode-toggle';
    modeToggle.setAttribute('role', 'tablist');
    modeToggle.append(this.onlineModeButton, this.offlineModeButton);

    const contentFrame = document.createElement('div');
    contentFrame.className = 'license-auth-content-frame';
    this.onlineContent.className = 'license-auth-content';
    this.offlineContent.className = 'license-auth-content license-auth-content--hidden';
    this.onlineContent.append(this.createOnlinePanel(), this.createOnlineStepper());
    this.offlineContent.append(this.createOfflinePanel(), this.createOfflineStepper());
    contentFrame.append(this.onlineContent, this.offlineContent);

    this.status.className = 'license-auth-status';
    this.status.setAttribute('role', 'status');

    this.otpPad.className = 'license-auth-otp';
    this.renderOtpPad();

    panel.append(this.title, modeToggle, contentFrame, this.status, this.otpPad);
    this.root.appendChild(panel);
    this.controls.splice(
      0,
      this.controls.length,
      this.onlineModeButton,
      this.offlineModeButton,
      this.onlineStep1Button,
      this.onlineStep2Button,
      this.onlineStep3Button,
      this.offlineStep1Button,
      this.offlineStep2Button,
      this.offlineStep3Button,
      this.offlineStep4Button,
    );
  }

  private createOnlinePanel(): HTMLElement {
    const panelCard = document.createElement('div');
    panelCard.className = 'license-auth-panel-card';

    this.onlineQrPanel.className = 'license-auth-view';
    this.onlineGuideText.className = 'license-auth-guide';
    this.onlineGuideText.textContent = '모바일에서 QR을 스캔한 뒤 OTP 입력 단계로 진행하세요.';
    this.onlineQrPanel.append(
      this.createViewTitle('Scan QR Code'),
      this.createQrFrame(this.onlineQrCanvas, 'online qr'),
      this.onlineGuideText,
    );

    this.onlineOtpPanel.className = 'license-auth-view license-auth-view--hidden';
    this.onlineOtpPanel.append(
      this.createViewTitle('OTP 번호 입력'),
      this.createViewGuide('모바일에서 발급된 OTP를 입력한 뒤 인증 완료를 진행하세요.'),
      this.createOtpRow(this.onlineOtpBoxes),
    );

    this.onlineCompletionPanel.className = 'license-auth-view license-auth-view--completion license-auth-view--hidden';
    this.onlineCompletionText.className = 'license-auth-completion-text';
    this.onlineCompletionPanel.append(this.onlineCompletionText);

    panelCard.append(this.onlineQrPanel, this.onlineOtpPanel, this.onlineCompletionPanel);
    return panelCard;
  }

  private createOfflinePanel(): HTMLElement {
    const panelCard = document.createElement('div');
    panelCard.className = 'license-auth-panel-card';

    const qrFrame = document.createElement('div');
    qrFrame.className = 'license-auth-qr-frame';
    this.offlineStartQrCanvas.className = 'license-auth-qr';
    this.offlineStartQrCanvas.setAttribute('aria-label', 'offline start qr');
    this.offlineProofQrCanvas.className = 'license-auth-qr license-auth-qr--hidden';
    this.offlineProofQrCanvas.setAttribute('aria-label', 'offline proof qr');
    qrFrame.append(this.offlineStartQrCanvas, this.offlineProofQrCanvas);

    this.offlineQrPanel.className = 'license-auth-view';
    this.offlineGuideText.className = 'license-auth-guide';
    this.offlineGuideText.textContent = '1단계 QR을 생성해 모바일로 스캔해 주세요.';
    this.offlineQrPanel.append(this.createViewTitle('Scan QR Code'), qrFrame, this.offlineGuideText);

    this.offlineOtpPanel.className = 'license-auth-view license-auth-view--hidden';
    this.offlineOtpPanel.append(
      this.createViewTitle('OTP 번호 입력'),
      this.createViewGuide('OTP 입력 후 2단계 증빙 QR을 생성해 주세요.'),
      this.createOtpRow(this.offlineOtpBoxes),
    );

    panelCard.append(this.offlineQrPanel, this.offlineOtpPanel);
    return panelCard;
  }

  private createOnlineStepper(): HTMLElement {
    const stepper = document.createElement('div');
    stepper.className = 'license-auth-stepper license-auth-stepper--online';
    stepper.append(
      this.onlineStep1Button,
      this.onlineStepLine1,
      this.onlineStep2Button,
      this.onlineStepLine2,
      this.onlineStep3Button,
    );
    return stepper;
  }

  private createOfflineStepper(): HTMLElement {
    const stepper = document.createElement('div');
    stepper.className = 'license-auth-stepper license-auth-stepper--offline';
    stepper.append(
      this.offlineStep1Button,
      this.offlineStepLine1,
      this.offlineStep2Button,
      this.offlineStepLine2,
      this.offlineStep3Button,
      this.offlineStepLine3,
      this.offlineStep4Button,
    );
    return stepper;
  }

  private createModeButton(mode: AuthMode, label: string): HTMLButtonElement {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'license-auth-mode-button';
    button.dataset.authAction = mode === 'ONLINE' ? 'mode-online' : 'mode-offline';
    button.setAttribute('role', 'tab');
    button.textContent = label;
    button.addEventListener('click', () => {
      void this.activate(button.dataset.authAction ?? '');
    });
    return button;
  }

  private createStepButton(action: string, label: string): HTMLButtonElement {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'license-auth-step';
    button.dataset.authAction = action;
    button.textContent = label;
    button.addEventListener('click', () => {
      void this.activate(action);
    });
    return button;
  }

  private createStepLine(): HTMLElement {
    const line = document.createElement('span');
    line.className = 'license-auth-step-line';
    line.setAttribute('aria-hidden', 'true');
    return line;
  }

  private createViewTitle(text: string): HTMLElement {
    const title = document.createElement('h3');
    title.className = 'license-auth-view-title';
    title.textContent = text;
    return title;
  }

  private createViewGuide(text: string): HTMLElement {
    const guide = document.createElement('p');
    guide.className = 'license-auth-guide';
    guide.textContent = text;
    return guide;
  }

  private createQrFrame(canvas: HTMLCanvasElement, label: string): HTMLElement {
    const frame = document.createElement('div');
    frame.className = 'license-auth-qr-frame';
    canvas.className = 'license-auth-qr';
    canvas.setAttribute('aria-label', label);
    frame.appendChild(canvas);
    return frame;
  }

  private createOtpBoxes(): HTMLDivElement[] {
    return Array.from({ length: OTP_LENGTH }, () => {
      const box = document.createElement('div');
      box.className = 'license-auth-otp-box';
      box.textContent = '';
      return box;
    });
  }

  private createOtpRow(boxes: HTMLElement[]): HTMLElement {
    const row = document.createElement('div');
    row.className = 'license-auth-otp-row';
    row.append(...boxes);
    return row;
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
      button.addEventListener('click', () => {
        this.activateOtp(button);
      });
      this.otpPad.appendChild(button);
      this.otpButtons.push(button);
    });
  }

  private initializeVisualFlow(): void {
    this.onlineQrPayload = '';
    this.offlineStartQrPayload = '';
    this.offlineProofQrPayload = '';
    this.onlineOtp = '';
    this.offlineOtp = '';
    this.offlineContext = null;
    this.showOnlineQrPanel();
    this.showOfflineQrPanel();
    this.setOnlineCompletionMessage(null);
    this.offlineGuideText.textContent = '1단계 QR을 생성해 모바일로 스캔해 주세요.';
    this.offlineStartQrCanvas.classList.remove('license-auth-qr--hidden');
    this.offlineProofQrCanvas.classList.add('license-auth-qr--hidden');
    this.updateOtpBoxes('ONLINE');
    this.updateOtpBoxes('OFFLINE');
    this.setOnlineStepperState(1, false, false);
    this.setOfflineStepperState(1, false, false, false);
  }

  private async activate(action: string): Promise<void> {
    if (action === 'mode-online') {
      this.setSelectedMode('ONLINE');
      await this.generateOnlineQr(true);
    } else if (action === 'mode-offline') {
      this.setSelectedMode('OFFLINE');
      await this.generateOfflineStart(true);
    } else if (action === 'online-step-1') {
      await this.generateOnlineQr(false);
    } else if (action === 'online-step-2') {
      this.openOnlineOtpStep();
    } else if (action === 'online-step-3') {
      await this.completeOnline();
    } else if (action === 'offline-step-1') {
      await this.generateOfflineStart(false);
    } else if (action === 'offline-step-2') {
      this.openOfflineOtpStep();
    } else if (action === 'offline-step-3') {
      await this.generateOfflineProof();
    } else if (action === 'offline-step-4') {
      await this.completeOffline();
    }
  }

  private setSelectedMode(mode: AuthMode): void {
    this.activeMode = mode;
    this.onlineContent.classList.toggle('license-auth-content--hidden', mode !== 'ONLINE');
    this.offlineContent.classList.toggle('license-auth-content--hidden', mode !== 'OFFLINE');
    this.onlineModeButton.classList.toggle('license-auth-mode-button--active', mode === 'ONLINE');
    this.offlineModeButton.classList.toggle('license-auth-mode-button--active', mode === 'OFFLINE');
    this.onlineModeButton.setAttribute('aria-selected', String(mode === 'ONLINE'));
    this.offlineModeButton.setAttribute('aria-selected', String(mode === 'OFFLINE'));
  }

  private async generateOnlineQr(triggeredByToggle: boolean): Promise<void> {
    try {
      const result = await this.options.service.buildOnlineQr();
      this.onlineQrPayload = result.qrText;
      this.onlineOtp = '';
      this.setOnlineCompletionMessage(null);
      this.updateOtpBoxes('ONLINE');
      await this.renderQr(this.onlineQrCanvas, result.qrText);
      this.showOnlineQrPanel();
      this.setOnlineStepperState(1, true, false);
      this.setStatus(triggeredByToggle ? '온라인 모드로 전환되어 1단계 QR을 자동 생성했습니다.' : '온라인 1단계 QR 재생성 완료');
    } catch (error) {
      this.setStatus(`온라인 QR 생성 실패: ${formatError(error)}`, true);
    }
  }

  private openOnlineOtpStep(): void {
    if (!this.onlineStep2Enabled || !this.onlineQrPayload) {
      this.setStatus('먼저 1단계 QR을 생성해 주세요.', true);
      return;
    }

    this.showOnlineOtpPanel();
    this.setOnlineStepperState(this.onlineOtp.length === OTP_LENGTH ? 3 : 2, true, this.onlineOtp.length === OTP_LENGTH);
    this.openOtpPad('ONLINE');
    this.setStatus('OTP를 입력한 뒤 인증 완료를 진행해 주세요.');
  }

  private async completeOnline(): Promise<void> {
    if (!this.onlineStep3Enabled) {
      this.setStatus('먼저 OTP 인증을 완료해 주세요.', true);
      return;
    }

    try {
      const result = await this.options.service.claimOnline(this.onlineOtp);
      if (!result.isValid) {
        this.setStatus(result.reason || '온라인 인증에 실패했습니다.', true);
        return;
      }

      this.setOnlineStepperState(3, true, true);
      this.showOnlineCompletionPanel('온라인 인증이 완료되었습니다.');
      this.finish(result);
    } catch (error) {
      this.setStatus(`온라인 인증 실패: ${formatError(error)}`, true);
    }
  }

  private async generateOfflineStart(triggeredByToggle: boolean): Promise<void> {
    try {
      const result = await this.options.service.buildOfflineStart();
      this.offlineContext = result.context;
      this.offlineStartQrPayload = result.qrText;
      this.offlineProofQrPayload = '';
      this.offlineOtp = '';
      this.updateOtpBoxes('OFFLINE');
      await this.renderQr(this.offlineStartQrCanvas, result.qrText);
      this.offlineProofQrCanvas.classList.add('license-auth-qr--hidden');
      this.offlineStartQrCanvas.classList.remove('license-auth-qr--hidden');
      this.offlineGuideText.textContent = '1단계 QR을 모바일에서 스캔한 뒤, 스텝퍼에서 OTP 입력 단계로 이동하세요.';
      this.showOfflineQrPanel();
      this.setOfflineStepperState(1, true, false, false);
      this.setStatus(triggeredByToggle ? '오프라인 모드로 전환되어 1단계 QR을 자동 생성했습니다.' : '오프라인 1단계 QR 재생성 완료');
    } catch (error) {
      this.setStatus(`오프라인 시작 QR 생성 실패: ${formatError(error)}`, true);
    }
  }

  private openOfflineOtpStep(): void {
    if (!this.offlineStep2Enabled || !this.offlineStartQrPayload) {
      this.setStatus('먼저 1단계 QR을 생성해 주세요.', true);
      return;
    }

    this.showOfflineOtpPanel();
    this.setOfflineStepperState(this.offlineOtp.length === OTP_LENGTH ? 3 : 2, true, this.offlineOtp.length === OTP_LENGTH, false);
    this.openOtpPad('OFFLINE');
    this.setStatus('OTP 입력 후 2단계 QR을 생성해 주세요.');
  }

  private async generateOfflineProof(): Promise<void> {
    if (!this.offlineStep3Enabled || !this.offlineStartQrPayload) {
      this.setStatus('먼저 OTP를 입력하고 2단계 QR을 생성해 주세요.', true);
      return;
    }

    if (!this.offlineContext) {
      this.setStatus('먼저 오프라인 1단계 QR을 생성해 주세요.', true);
      return;
    }

    try {
      const result = await this.options.service.buildOfflineProof(this.offlineContext, this.offlineOtp);
      this.offlineContext = result.context;
      this.offlineProofQrPayload = result.qrText;
      await this.renderQr(this.offlineProofQrCanvas, result.qrText);
      this.offlineStartQrCanvas.classList.add('license-auth-qr--hidden');
      this.offlineProofQrCanvas.classList.remove('license-auth-qr--hidden');
      this.offlineGuideText.textContent = '2단계 QR을 모바일에서 인식했으면 완료 버튼을 누르거나 창을 닫아 주세요.';
      this.showOfflineQrPanel();
      this.setOfflineStepperState(3, true, true, true);
      this.setStatus('오프라인 2단계 QR 생성 완료. 모바일에서 인식한 뒤 완료를 누르거나 창을 닫아 주세요.');
    } catch (error) {
      this.setStatus(`오프라인 증빙 QR 생성 실패: ${formatError(error)}`, true);
    }
  }

  private async completeOffline(): Promise<void> {
    if (!this.offlineStep4Enabled || !this.offlineContext || !this.offlineProofQrPayload) {
      this.setStatus('2단계 QR을 모바일에서 인식한 뒤 완료할 수 있습니다.', true);
      return;
    }

    try {
      const result = await this.options.service.completeOffline(this.offlineContext);
      this.setOfflineStepperState(4, true, true, true);
      this.finish(result);
    } catch (error) {
      this.setStatus(`오프라인 인증 실패: ${formatError(error)}`, true);
    }
  }

  private showOnlineQrPanel(): void {
    this.onlineQrPanel.classList.remove('license-auth-view--hidden');
    this.onlineOtpPanel.classList.add('license-auth-view--hidden');
    this.onlineCompletionPanel.classList.add('license-auth-view--hidden');
  }

  private showOnlineOtpPanel(): void {
    this.onlineQrPanel.classList.add('license-auth-view--hidden');
    this.onlineOtpPanel.classList.remove('license-auth-view--hidden');
    this.onlineCompletionPanel.classList.add('license-auth-view--hidden');
  }

  private showOnlineCompletionPanel(message: string): void {
    this.onlineQrPanel.classList.add('license-auth-view--hidden');
    this.onlineOtpPanel.classList.add('license-auth-view--hidden');
    this.setOnlineCompletionMessage(message);
  }

  private showOfflineQrPanel(): void {
    this.offlineQrPanel.classList.remove('license-auth-view--hidden');
    this.offlineOtpPanel.classList.add('license-auth-view--hidden');
  }

  private showOfflineOtpPanel(): void {
    this.offlineQrPanel.classList.add('license-auth-view--hidden');
    this.offlineOtpPanel.classList.remove('license-auth-view--hidden');
  }

  private setOnlineCompletionMessage(message: string | null): void {
    const normalized = message?.trim() ?? '';
    this.onlineCompletionPanel.classList.toggle('license-auth-view--hidden', !normalized);
    this.onlineCompletionText.textContent = normalized;
  }

  private setOnlineStepperState(currentStep: number, step2Enabled: boolean, step3Enabled: boolean): void {
    this.onlineStep2Enabled = step2Enabled;
    this.onlineStep3Enabled = step3Enabled;
    this.applyStepButton({ button: this.onlineStep1Button, enabled: true, reached: currentStep >= 1, active: currentStep === 1 });
    this.applyStepButton({ button: this.onlineStep2Button, enabled: step2Enabled, reached: currentStep >= 2, active: currentStep === 2 });
    this.applyStepButton({ button: this.onlineStep3Button, enabled: step3Enabled, reached: currentStep >= 3, active: currentStep === 3 });
    this.onlineStepLine1.classList.toggle('license-auth-step-line--active', currentStep >= 2);
    this.onlineStepLine2.classList.toggle('license-auth-step-line--active', currentStep >= 3);
  }

  private setOfflineStepperState(currentStep: number, step2Enabled: boolean, step3Enabled: boolean, step4Enabled: boolean): void {
    this.offlineStep2Enabled = step2Enabled;
    this.offlineStep3Enabled = step3Enabled;
    this.offlineStep4Enabled = step4Enabled;
    this.applyStepButton({ button: this.offlineStep1Button, enabled: true, reached: currentStep >= 1, active: currentStep === 1 });
    this.applyStepButton({ button: this.offlineStep2Button, enabled: step2Enabled, reached: currentStep >= 2, active: currentStep === 2 });
    this.applyStepButton({ button: this.offlineStep3Button, enabled: step3Enabled, reached: currentStep >= 3, active: currentStep === 3 });
    this.applyStepButton({ button: this.offlineStep4Button, enabled: step4Enabled, reached: currentStep >= 4, active: currentStep === 4 });
    this.offlineStepLine1.classList.toggle('license-auth-step-line--active', currentStep >= 2);
    this.offlineStepLine2.classList.toggle('license-auth-step-line--active', currentStep >= 3);
    this.offlineStepLine3.classList.toggle('license-auth-step-line--active', currentStep >= 4);
  }

  private applyStepButton(step: StepButton): void {
    step.button.disabled = !step.enabled;
    step.button.classList.toggle('license-auth-step--reached', step.reached);
    step.button.classList.toggle('license-auth-step--active', step.active);
  }

  private finish(state: LicenseAuthState): void {
    this.setStatus(state.mode === 'OFFLINE' ? '오프라인 인증이 완료되었습니다.' : '온라인 인증이 완료되었습니다.');
    this.options.onStatus?.(state.status, state.mode);
    this.resolve?.(state);
    this.resolve = null;
    this.close();
  }

  private cancelAuthentication(): void {
    const cancelledState: LicenseAuthState = {
      isValid: false,
      mode: 'NONE',
      status: 'cancelled',
      reason: '사용자가 LicenseHub 인증을 취소했습니다.',
      deviceFingerprint: '',
      deviceId: '',
      licenseToken: '',
      serverChecked: false,
      usedOfflineFallback: false,
    };
    this.setStatus(cancelledState.reason, true);
    this.options.onStatus?.('cancelled', cancelledState.reason);
    this.resolve?.(cancelledState);
    this.resolve = null;
    this.close();
  }

  private async renderQr(canvas: HTMLCanvasElement, text: string): Promise<void> {
    await QRCode.toCanvas(canvas, text, {
      width: 720,
      margin: 1,
      color: {
        dark: '#000000',
        light: '#ffffff',
      },
      errorCorrectionLevel: 'M',
    });
  }

  private openOtpPad(mode: AuthMode): void {
    this.otpPad.dataset.otpMode = mode;
    this.otpPad.classList.add('license-auth-otp--visible');
    this.updateOtpBoxes(mode);
    this.otpButtons[0]?.focus();
  }

  private closeOtpPad(): void {
    this.otpPad.classList.remove('license-auth-otp--visible');
    this.focusActiveStep();
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
    if (value && current.length < OTP_LENGTH) {
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
      this.setOnlineStepperState(next.length === OTP_LENGTH ? 3 : 2, true, next.length === OTP_LENGTH);
    } else {
      this.offlineOtp = next;
      this.setOfflineStepperState(next.length === OTP_LENGTH ? 3 : 2, true, next.length === OTP_LENGTH, false);
    }
    this.updateOtpBoxes(mode);
  }

  private updateOtpBoxes(mode: AuthMode): void {
    const value = mode === 'ONLINE' ? this.onlineOtp : this.offlineOtp;
    const boxes = mode === 'ONLINE' ? this.onlineOtpBoxes : this.offlineOtpBoxes;
    boxes.forEach((box, index) => {
      box.textContent = value[index] ?? '';
      box.classList.toggle('license-auth-otp-box--filled', Boolean(value[index]));
      box.classList.toggle('license-auth-otp-box--active', index === Math.min(value.length, OTP_LENGTH - 1));
    });
  }

  private setStatus(message: string, error = false): void {
    this.status.textContent = message;
    this.status.classList.toggle('license-auth-status--error', error);
    this.status.classList.add('license-auth-status--visible');
    this.options.onStatus?.(error ? 'failed' : 'ready', message);
  }

  private focusControl(index: number): void {
    const controls = this.visibleControls();
    controls[Math.min(Math.max(0, index), controls.length - 1)]?.focus();
  }

  private focusRelative(offset: number): void {
    const controls = this.visibleControls();
    const index = controls.findIndex((control) => control === document.activeElement);
    const nextIndex = index < 0 ? 0 : (index + offset + controls.length) % controls.length;
    controls[nextIndex]?.focus();
  }

  private visibleControls(): AuthControl[] {
    return this.controls.filter((control) => {
      if (control.disabled) {
        return false;
      }
      const content = control.closest('.license-auth-content');
      return !content || !content.classList.contains('license-auth-content--hidden');
    });
  }

  private focusActiveStep(): void {
    const active = this.root.querySelector<HTMLButtonElement>('.license-auth-content:not(.license-auth-content--hidden) .license-auth-step--active');
    active?.focus();
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
