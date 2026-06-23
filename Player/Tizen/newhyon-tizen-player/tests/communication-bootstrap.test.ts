import { beforeEach, describe, expect, it, vi } from 'vitest';
import { bootstrapCommunicationSettings } from '../src/app/communication-bootstrap';
import { DEFAULT_PLAYER_SETTINGS, loadPlayerSettings } from '../src/app/player-settings';

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

describe('communication bootstrap', () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.restoreAllMocks();
    horizonFactory.mockReset();
  });

  it('managerAddress를 데이터서버 IP로 사용해 서버 설정을 받고 DB/SignalR/FTP 설정을 완료한다', async () => {
    const disconnect = vi.fn();
    const fetchServerSettings = vi.fn(() => createObservable({
      dataServerIp: '10.0.0.20',
      messageServerIp: '10.0.0.30',
      ftpPort: 10022,
      ftpPasvMinPort: 24000,
      ftpPasvMaxPort: 24240,
      ftpRootPath: '/MediaRoot',
      ftpUserName: 'ftp-user',
      ftpPassword: 'ftp-pass',
    }));
    const fetchPlayerInfo = vi.fn(() => createObservable([{
      id: 'player-guid-1',
      PIF_GUID: 'legacy-local-guid',
      PIF_PlayerName: 'tizen',
    }]));
    const horizonCollection = vi.fn((collectionName: string) => {
      if (collectionName === 'ServerSettings') {
        return {
          find: vi.fn(() => ({ fetch: fetchServerSettings })),
          findAll: vi.fn(),
          limit: vi.fn(() => ({ fetch: vi.fn() })),
        };
      }

      return {
        find: vi.fn(),
        findAll: vi.fn(() => ({ fetch: fetchPlayerInfo })),
        limit: vi.fn(),
      };
    });
    horizonFactory.mockReturnValue(Object.assign(horizonCollection, { disconnect }));

    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url === 'http://10.0.0.30:5000/Data/negotiate?playerName=tizen&playerGuid=player-guid-1&negotiateVersion=1') {
        return {
          ok: true,
          json: async () => ({ connectionId: 'c1' }),
        } as Response;
      }

      throw new Error(`unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    const result = await bootstrapCommunicationSettings({
      ...DEFAULT_PLAYER_SETTINGS,
      managerAddress: '10.0.0.10',
    });

    expect(result).toMatchObject({
      horizonHost: '10.0.0.10:8181',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      dataServerAddress: '10.0.0.20',
      messageServerAddress: '10.0.0.30',
      dbHost: '10.0.0.20',
      dbStatus: 'connected',
      signalrUrl: 'http://10.0.0.30:5000/Data',
      signalrStatus: 'connected',
      ftpHost: '10.0.0.20',
      ftpPort: 10022,
      ftpPasvMinPort: 24000,
      ftpPasvMaxPort: 24240,
      ftpRootPath: '/MediaRoot',
      ftpUserName: 'ftp-user',
      ftpPassword: 'ftp-pass',
      ftpStatus: 'connected',
      ftpStatusDetail: '10.0.0.20:10022/MediaRoot',
      signalrNegotiated: true,
    });
    expect(horizonFactory).toHaveBeenCalledWith({
      host: '10.0.0.10:8181',
      secure: false,
      lazyWrites: false,
    });
    expect(horizonCollection).toHaveBeenCalledWith('ServerSettings');
    expect(horizonCollection).toHaveBeenCalledWith('PlayerInfoManager');
    expect(disconnect).toHaveBeenCalledTimes(2);
    expect(loadPlayerSettings()).toMatchObject({
      managerAddress: '10.0.0.10',
      dataServerAddress: '10.0.0.20',
      messageServerAddress: '10.0.0.30',
      ftpPort: 10022,
      ftpRootPath: '/MediaRoot',
    });
  });

  it('ServerSettings에 FTP 계정 필드가 없으면 레퍼런스 플레이어의 기본 FTP 계정을 사용한다', async () => {
    const disconnect = vi.fn();
    const fetchServerSettings = vi.fn(() => createObservable({
      DataServerIp: '10.0.0.20',
      MessageServerIp: '10.0.0.30',
      FTP_Port: 21,
      FTP_RootPath: '/NewHyOn',
    }));
    const fetchPlayerInfo = vi.fn(() => createObservable([{
      id: 'player-guid-1',
      PIF_PlayerName: 'tizen',
    }]));
    const horizonCollection = vi.fn((collectionName: string) => {
      if (collectionName === 'ServerSettings') {
        return {
          find: vi.fn(() => ({ fetch: fetchServerSettings })),
          findAll: vi.fn(),
          limit: vi.fn(() => ({ fetch: vi.fn() })),
        };
      }

      return {
        find: vi.fn(),
        findAll: vi.fn(() => ({ fetch: fetchPlayerInfo })),
        limit: vi.fn(),
      };
    });
    horizonFactory.mockReturnValue(Object.assign(horizonCollection, { disconnect }));
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      json: async () => ({ connectionId: 'c1' }),
    } as Response)));

    const result = await bootstrapCommunicationSettings({
      ...DEFAULT_PLAYER_SETTINGS,
      managerAddress: '10.0.0.10',
    });

    expect(result).toMatchObject({
      ftpUserName: 'asdf',
      ftpPassword: 'Emfndhk!',
      ftpHost: '10.0.0.20',
      ftpPort: 21,
      ftpRootPath: '/NewHyOn',
    });
  });

  it('SignalR negotiate가 실패하면 통신 부트스트랩을 실패시킨다', async () => {
    const disconnect = vi.fn();
    const fetchServerSettings = vi.fn(() => createObservable({
      DataServerIp: '10.0.0.20',
      MessageServerIp: '10.0.0.30',
      FTP_Port: 21,
      FTP_RootPath: '/NewHyOn',
    }));
    const fetchPlayerInfo = vi.fn(() => createObservable([{
      id: 'player-guid-1',
      PIF_PlayerName: 'tizen',
    }]));
    const horizonCollection = vi.fn((collectionName: string) => {
      if (collectionName === 'ServerSettings') {
        return {
          find: vi.fn(() => ({ fetch: fetchServerSettings })),
          findAll: vi.fn(),
          limit: vi.fn(() => ({ fetch: vi.fn() })),
        };
      }

      return {
        find: vi.fn(),
        findAll: vi.fn(() => ({ fetch: fetchPlayerInfo })),
        limit: vi.fn(),
      };
    });
    horizonFactory.mockReturnValue(Object.assign(horizonCollection, { disconnect }));
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: false,
      status: 503,
      statusText: 'Service Unavailable',
    } as Response)));

    await expect(bootstrapCommunicationSettings({
      ...DEFAULT_PLAYER_SETTINGS,
      managerAddress: '10.0.0.10',
    })).rejects.toThrow('SignalR negotiate 실패: 503 Service Unavailable');
  });
});
