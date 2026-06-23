import { beforeEach, describe, expect, it, vi } from 'vitest';
import { syncPlayerInfoMessage } from '../src/app/player-info-sync';

const horizonFactory = vi.hoisted(() => vi.fn());

vi.mock('@horizon/client', () => ({
  default: horizonFactory,
}));

function createObservable<T>(value: T) {
  return {
    subscribe(observer: { next?: (value: T) => void; complete?: () => void }) {
      observer.next?.(value);
      observer.complete?.();
      return {
        unsubscribe: vi.fn(),
      };
    },
  };
}

function createNonCompletingObservable<T>(value: T) {
  return {
    subscribe(observer: { next?: (value: T) => void }) {
      observer.next?.(value);
      return {
        unsubscribe: vi.fn(),
      };
    },
  };
}

describe('syncPlayerInfoMessage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    horizonFactory.mockReset();
    window.webapis = {
      network: {
        getIp: () => '192.168.50.180',
        getMac: () => 'aabbccddeeff',
        isConnectedToGateway: () => true,
        getActiveConnectionType: () => 'ETHERNET',
      },
    };
  });

  it('레퍼런스 PlayerInfoManager 필드명과 값으로 플레이어 정보를 업데이트한다', async () => {
    const disconnect = vi.fn();
    const update = vi.fn(() => createObservable({ replaced: 1 }));
    const findFetch = vi.fn(() => createObservable({
      id: 'player-guid-1',
      PIF_PlayerName: 'tizen',
      PIF_CurrentPlayList: 'current-list',
      PIF_DefaultPlayList: 'default-list',
      PIF_MacAddress: 'old-fingerprint',
      PIF_AuthKey: 'old-auth',
      command: 'pause',
    }));
    const collection = vi.fn(() => ({
      find: vi.fn(() => ({ fetch: findFetch })),
      update,
    }));
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect }));

    await syncPlayerInfoMessage({
      managerAddress: 'turtlesrv.ddns.net',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      appVersion: 'v1.0.0',
      authState: {
        isValid: true,
        mode: 'ONLINE',
        status: 'server-valid',
        reason: '',
        deviceFingerprint: 'ABCDEF1234',
        deviceId: 'device-id-1',
        licenseToken: 'token',
        serverChecked: true,
        usedOfflineFallback: false,
      },
    });

    expect(update).toHaveBeenCalledWith({
      id: 'player-guid-1',
      PIF_PlayerName: 'tizen',
      PIF_CurrentPlayList: 'current-list',
      PIF_DefaultPlayList: 'default-list',
      PIF_IPAddress: '192.168.50.180',
      PIF_OSName: 'Tizen v1.0.0',
      PIF_MacAddress: 'AA:BB:CC:DD:EE:FF',
      PIF_AuthKey: JSON.stringify({
        AuthProvider: 'LicenseHub',
        AuthSchema: 'ValidationResult',
        AuthVersion: 2,
        ProductId: 8,
        IsValid: true,
        DeviceId: 'device-id-1',
      }),
      command: 'pause',
    });
    expect(horizonFactory).toHaveBeenCalledWith({
      host: 'turtlesrv.ddns.net:8181',
      secure: false,
      lazyWrites: false,
    });
    expect(disconnect).toHaveBeenCalledTimes(1);
  });

  it('SSSP Network API가 없으면 PlayerInfoManager 동기화를 중단한다', async () => {
    window.webapis = {};
    const disconnect = vi.fn();
    const collection = vi.fn(() => ({
      find: vi.fn(() => ({ fetch: vi.fn(() => createObservable({ id: 'player-guid-1' })) })),
      update: vi.fn(() => createObservable({ replaced: 1 })),
    }));
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect }));

    await expect(syncPlayerInfoMessage({
      managerAddress: 'turtlesrv.ddns.net',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      appVersion: 'v1.0.0',
      authState: null,
    })).rejects.toThrow('SSSP Network API를 사용할 수 없습니다.');
    expect(disconnect).toHaveBeenCalledTimes(1);
  });

  it('Horizon observable이 complete를 보내지 않아도 첫 응답에서 동기화를 완료한다', async () => {
    const disconnect = vi.fn();
    const update = vi.fn(() => createNonCompletingObservable({ replaced: 1 }));
    const collection = vi.fn(() => ({
      find: vi.fn(() => ({
        fetch: vi.fn(() => createNonCompletingObservable({
          id: 'player-guid-1',
          PIF_PlayerName: 'tizen',
        })),
      })),
      update,
    }));
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect }));

    await expect(syncPlayerInfoMessage({
      managerAddress: 'turtlesrv.ddns.net',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      appVersion: 'v1.0.0',
      authState: null,
    })).resolves.toBeUndefined();

    expect(update).toHaveBeenCalledTimes(1);
    expect(disconnect).toHaveBeenCalledTimes(1);
  });
});
