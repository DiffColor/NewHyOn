import { beforeEach, describe, expect, it, vi } from 'vitest';
import { webcrypto } from 'node:crypto';
import { LicenseHubAuthService, LicenseHubAuthStorage } from '../src/app/licensehub-auth';

describe('LicenseHubAuthService', () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.stubGlobal('crypto', webcrypto);
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network unavailable')));
    window.webapis = {
      productinfo: {
        getDuid: () => 'duid-1',
        getModelCode: () => 'QM32C',
      },
    };
  });

  it('온라인 인증 QR payload를 LicenseHub 형식으로 생성한다', async () => {
    const service = new LicenseHubAuthService({
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      appVersion: '1.0.0',
    });

    const result = await service.buildOnlineQr();

    expect(result.qrText).toMatch(/^LICENSEHUB-DEVICE:/);
    expect(result.deviceFingerprint).toMatch(/^[0-9A-F]{64}$/);
    const payload = JSON.parse(result.qrText.replace('LICENSEHUB-DEVICE:', '')) as {
      ProductId: number;
      Version: number;
      Platform: string;
      DeviceName: string;
      DeviceFingerprint: string;
    };
    expect(payload).toMatchObject({
      ProductId: 8,
      Version: 1,
      Platform: 'tizen',
      DeviceName: 'tizen',
      DeviceFingerprint: result.deviceFingerprint,
    });
  });

  it('기기 fingerprint는 DUID만 기준으로 하며 모델명/플레이어 정보 변경에 흔들리지 않는다', async () => {
    const firstService = new LicenseHubAuthService({
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      appVersion: '1.0.0',
    });
    const first = await firstService.buildOnlineQr();

    window.webapis = {
      productinfo: {
        getDuid: () => 'duid-1',
        getModelCode: () => 'OTHER-MODEL',
      },
    };
    const secondService = new LicenseHubAuthService({
      playerGuid: 'changed-guid',
      playerName: 'changed-name',
      appVersion: '2.0.0',
    });
    const second = await secondService.buildOnlineQr();

    window.webapis = {
      productinfo: {
        getDuid: () => 'duid-2',
        getModelCode: () => 'OTHER-MODEL',
      },
    };
    const thirdService = new LicenseHubAuthService({
      playerGuid: 'changed-guid',
      playerName: 'changed-name',
      appVersion: '2.0.0',
    });
    const third = await thirdService.buildOnlineQr();

    expect(second.deviceFingerprint).toBe(first.deviceFingerprint);
    expect(third.deviceFingerprint).not.toBe(first.deviceFingerprint);
  });

  it('오프라인 증빙 QR 생성 후 offline marker를 저장한다', async () => {
    const storage = new LicenseHubAuthStorage();
    const service = new LicenseHubAuthService({
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      appVersion: '1.0.0',
    }, storage);

    const start = await service.buildOfflineStart();
    expect(start.qrText).toMatch(/^LH3-DEVICE:/);
    expect(JSON.parse(start.qrText.replace('LH3-DEVICE:', ''))).toMatchObject({
      Version: 3,
      Type: 'DEVICE_START',
      ProductId: 8,
      AuthMode: 'NORMAL_OFFLINE',
      KeyId: 'off_k_v1',
    });

    const proof = await service.buildOfflineProof(start.context, '23456');
    expect(proof.qrText).toMatch(/^LH3-PROOF:/);
    expect(JSON.parse(proof.qrText.replace('LH3-PROOF:', ''))).toMatchObject({
      Version: 3,
      Type: 'DEVICE_PROOF',
      SessionId: start.context.sessionId,
      AuthMode: 'NORMAL_OFFLINE',
      Otp: '23456',
    });

    const state = await service.completeOffline(proof.context);
    expect(state).toMatchObject({
      isValid: true,
      mode: 'OFFLINE',
      status: 'offline-verified',
    });

    const stored = await storage.read();
    expect(stored?.authMarker).toContain('"AuthProvider":"LicenseHub"');
    expect(stored?.authMarker).toContain('"ProductId":8');
  });

  it('오프라인 marker가 있어도 서버 차단 응답이 오면 인증을 거부하고 저장값을 지운다', async () => {
    const storage = new LicenseHubAuthStorage();
    const service = new LicenseHubAuthService({
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      appVersion: '1.0.0',
    }, storage);

    const start = await service.buildOfflineStart();
    await service.completeOffline(start.context);
    expect((await storage.read())?.authMarker).toContain('"IsValid":true');

    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({
      isIssued: false,
      status: 'device_blocked',
      reason: '차단된 디바이스입니다.',
    }), { status: 200 }));

    const state = await service.validateStoredOrBootstrap();

    expect(state).toMatchObject({
      isValid: false,
      status: 'device_blocked',
      reason: '차단된 디바이스입니다.',
    });
    expect(await storage.read()).toBeNull();
  });

  it('DUID가 없으면 인증을 진행하지 않는다', async () => {
    window.webapis = {
      productinfo: {
        getDuid: () => '',
      },
    };
    const service = new LicenseHubAuthService({
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      appVersion: '1.0.0',
    });

    await expect(service.buildOnlineQr()).rejects.toThrow('Tizen DUID');
  });
});
