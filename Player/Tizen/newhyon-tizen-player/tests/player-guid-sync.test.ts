import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PlayerGuidSynchronizer, resolvePlayerIdentityOnce } from '../src/app/player-guid-sync';

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

describe('PlayerGuidSynchronizer', () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.restoreAllMocks();
    horizonFactory.mockReset();
  });

  it('PlayerInfoManager의 id를 원격 GUID로 동기화한다', async () => {
    const disconnect = vi.fn();
    const findAllFetch = vi.fn(() => createObservable([{
      id: 'remote-guid-1',
      PIF_GUID: 'legacy-guid',
      PIF_PlayerName: 'tizen',
      PIF_CurrentPlayList: 'playlist',
    }]));
    const collection = vi.fn(() => ({
      find: vi.fn(),
      findAll: vi.fn(() => ({ fetch: findAllFetch })),
      limit: vi.fn(),
    }));
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect }));

    const identity = await resolvePlayerIdentityOnce('turtlesrv.ddns.net', 'tizen');

    expect(identity).toEqual({
      playerGuid: 'remote-guid-1',
      playerName: 'tizen',
    });
    expect(horizonFactory).toHaveBeenCalledWith({
      host: 'turtlesrv.ddns.net:8181',
      secure: false,
      lazyWrites: false,
    });
    expect(disconnect).toHaveBeenCalledTimes(1);
  });

  it('강제 검증이 아니면 원격 재조회를 하지 않고 현재 GUID를 그대로 사용한다', async () => {
    const disconnect = vi.fn();
    const findAllFetch = vi.fn(() => createObservable([{
      id: 'remote-guid-2',
      PIF_PlayerName: 'tizen',
    }]));
    const collection = vi.fn(() => ({
      find: vi.fn(),
      findAll: vi.fn(() => ({ fetch: findAllFetch })),
      limit: vi.fn(),
    }));
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect }));

    const synchronizer = new PlayerGuidSynchronizer({
      managerAddress: 'turtlesrv.ddns.net',
      playerName: 'tizen',
      initialIdentity: {
        playerGuid: 'remote-guid-1',
        playerName: 'tizen',
      },
    });

    await expect(synchronizer.ensurePlayerIdentity()).resolves.toMatchObject({ playerGuid: 'remote-guid-1' });

    expect(findAllFetch).not.toHaveBeenCalled();
    expect(disconnect).not.toHaveBeenCalled();
  });

  it('원격 조회는 성공했지만 플레이어가 없으면 기존 GUID를 제거한다', async () => {
    window.localStorage.setItem('newhyon-tizen-player.guid-sync.v1', JSON.stringify({
      playerGuid: 'old-guid',
      playerName: 'tizen',
      guidVerified: true,
      lastVerifiedAt: 0,
    }));
    const disconnect = vi.fn();
    const findByGuidFetch = vi.fn(() => createObservable(null));
    const findAllFetch = vi.fn(() => createObservable([]));
    const collection = vi.fn(() => ({
      find: vi.fn(() => ({ fetch: findByGuidFetch })),
      findAll: vi.fn(() => ({ fetch: findAllFetch })),
      limit: vi.fn(),
    }));
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect }));

    const synchronizer = new PlayerGuidSynchronizer({
      managerAddress: 'turtlesrv.ddns.net',
      playerName: 'tizen',
    });

    await expect(synchronizer.ensurePlayerIdentity(true)).resolves.toBeNull();
    expect(window.localStorage.getItem('newhyon-tizen-player.guid-sync.v1')).toBeNull();
  });
});
