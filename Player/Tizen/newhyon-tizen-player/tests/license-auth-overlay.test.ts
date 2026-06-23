import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LicenseAuthOverlay } from '../src/app/license-auth-overlay';
import type { LicenseAuthState, LicenseHubAuthService, OfflineAuthContext } from '../src/app/licensehub-auth';

const toCanvas = vi.hoisted(() => vi.fn(async () => undefined));

vi.mock('qrcode', () => ({
  default: {
    toCanvas,
  },
}));

function keyEvent(key: string): KeyboardEvent {
  return new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
}

function createOfflineContext(): OfflineAuthContext {
  return {
    productId: 8,
    deviceId: 'device-1',
    deviceFingerprint: 'fingerprint-1',
    deviceName: 'tizen',
    platform: 'tizen',
    osVersion: '1.0.0',
    sessionId: 'session-1',
    nonce: 'nonce-1',
    authMode: 'NORMAL_OFFLINE',
    keyVersion: 1,
    keyId: 'off_k_v1',
    issuedSlot: 1,
    issuedAt: new Date('2026-06-23T00:00:00.000Z'),
    proofNonce: 'proof-1',
  };
}

function validState(mode: 'ONLINE' | 'OFFLINE'): LicenseAuthState {
  return {
    isValid: true,
    mode,
    status: mode === 'ONLINE' ? 'online-verified' : 'offline-verified',
    reason: '',
    deviceFingerprint: 'fingerprint-1',
    deviceId: 'device-1',
    licenseToken: 'token-1',
    serverChecked: mode === 'ONLINE',
    usedOfflineFallback: mode === 'OFFLINE',
  };
}

function createService(): LicenseHubAuthService {
  const offlineContext = createOfflineContext();
  return {
    buildOnlineQr: vi.fn(async () => ({
      qrText: 'LICENSEHUB-DEVICE:online',
      deviceFingerprint: 'fingerprint-1',
    })),
    claimOnline: vi.fn(async () => ({
      ...validState('ONLINE'),
      saved: true,
    })),
    buildOfflineStart: vi.fn(async () => ({
      qrText: 'LH3-DEVICE:offline-start',
      context: offlineContext,
    })),
    buildOfflineProof: vi.fn(async () => ({
      qrText: 'LH3-PROOF:offline-proof',
      context: offlineContext,
    })),
    completeOffline: vi.fn(async () => validState('OFFLINE')),
    validateStoredOrBootstrap: vi.fn(async () => validState('ONLINE')),
  } as unknown as LicenseHubAuthService;
}

describe('LicenseAuthOverlay', () => {
  beforeEach(() => {
    document.body.textContent = '';
    toCanvas.mockClear();
  });

  it('안드로이드 인증앱과 같은 탭, QR 패널, 스텝퍼 UI를 연다', async () => {
    const overlay = new LicenseAuthOverlay({ service: createService() });

    overlay.open('QR 생성 후 인증을 진행해 주세요.');
    await vi.waitFor(() => expect(toCanvas).toHaveBeenCalledWith(
      expect.any(HTMLCanvasElement),
      'LICENSEHUB-DEVICE:online',
      expect.any(Object),
    ));

    expect(document.querySelector('.license-auth-title')?.textContent).toBe('디바이스 인증');
    expect(document.querySelectorAll('.license-auth-mode-button')).toHaveLength(2);
    expect(document.querySelector('.license-auth-mode-button--active')?.textContent).toBe('온라인 인증');
    expect(document.querySelector('.license-auth-view-title')?.textContent).toBe('Scan QR Code');
    expect(Array.from(document.querySelectorAll('.license-auth-step')).map((node) => node.textContent)).toEqual([
      '1단계 QR 재생성',
      'OTP 입력',
      '인증 완료',
      '1단계 QR 재생성',
      'OTP 입력',
      '2단계 QR 생성',
      '완료',
    ]);
  });

  it('온라인 OTP 단계는 5칸 입력 UI를 보여주고 OTP 완료 시 인증 완료 단계를 활성화한다', async () => {
    const overlay = new LicenseAuthOverlay({ service: createService() });
    overlay.open('start');
    await vi.waitFor(() => expect(toCanvas).toHaveBeenCalled());

    const otpStep = document.querySelector<HTMLButtonElement>('[data-auth-action="online-step-2"]');
    otpStep?.focus();
    overlay.handleKeyDown(keyEvent('Enter'));

    expect(document.querySelectorAll('.license-auth-content:not(.license-auth-content--hidden) .license-auth-otp-box')).toHaveLength(5);
    expect(document.querySelector('.license-auth-otp--visible')).not.toBeNull();

    for (let i = 0; i < 5; i += 1) {
      overlay.handleKeyDown(keyEvent('Enter'));
    }

    expect(Array.from(document.querySelectorAll('.license-auth-content:not(.license-auth-content--hidden) .license-auth-otp-box')).map((node) => node.textContent)).toEqual([
      '2',
      '2',
      '2',
      '2',
      '2',
    ]);
    expect(document.querySelector<HTMLButtonElement>('[data-auth-action="online-step-3"]')?.disabled).toBe(false);
  });

  it('오프라인 탭은 4단계 스텝퍼와 2단계 QR 흐름을 사용한다', async () => {
    const overlay = new LicenseAuthOverlay({ service: createService() });
    overlay.open('start');
    await vi.waitFor(() => expect(toCanvas).toHaveBeenCalled());

    const offlineMode = document.querySelector<HTMLButtonElement>('[data-auth-action="mode-offline"]');
    offlineMode?.focus();
    overlay.handleKeyDown(keyEvent('Enter'));
    await vi.waitFor(() => expect(toCanvas).toHaveBeenCalledWith(
      expect.any(HTMLCanvasElement),
      'LH3-DEVICE:offline-start',
      expect.any(Object),
    ));

    expect(document.querySelector('.license-auth-mode-button--active')?.textContent).toBe('오프라인 인증');
    expect(document.querySelector('.license-auth-content:not(.license-auth-content--hidden) [data-auth-action="offline-step-4"]')?.textContent).toBe('완료');
    expect(document.querySelector('.license-auth-content:not(.license-auth-content--hidden) .license-auth-guide')?.textContent)
      .toContain('1단계 QR을 모바일에서 스캔');
  });

  it('인증창에서 Back 키를 누르면 인증을 취소하고 창을 닫는다', async () => {
    const overlay = new LicenseAuthOverlay({ service: createService() });
    const authResult = overlay.open('start');
    await vi.waitFor(() => expect(toCanvas).toHaveBeenCalled());

    const handled = overlay.handleKeyDown(keyEvent('Back'));
    await expect(authResult).resolves.toMatchObject({
      isValid: false,
      mode: 'NONE',
      status: 'cancelled',
    });

    expect(handled).toBe(true);
    expect(document.querySelector('.license-auth-overlay')?.classList.contains('license-auth-overlay--hidden')).toBe(true);
  });
});
