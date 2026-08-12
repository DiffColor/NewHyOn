import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CommandQueueClient } from '../src/app/command-queue-client';

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

describe('CommandQueueClient', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    horizonFactory.mockReset();
  });

  it('플레이어 대상 pending 및 오래된 sent 명령을 CreatedAt ASC, 동률 ID ASC로 가져오고 교체/만료 명령은 제외한다', async () => {
    const rows = [
      {
        id: 'cmd-new-sent',
        PlayerIds: ['player-guid-1'],
        Command: 'check',
        Status: { 'player-guid-1': 'sent' },
        CreatedAt: '2026-06-22 10:00:00',
        UpdatedAt: new Date().toISOString(),
      },
      {
        id: 'cmd-replaced',
        PlayerIds: ['player-guid-1'],
        Command: 'updatelist',
        Status: { 'player-guid-1': 'pending' },
        CreatedAt: '2026-06-22 11:00:00',
        ReplacedBy: 'cmd-latest',
      },
      {
        id: 'cmd-expired',
        PlayerIds: ['player-guid-1'],
        Command: 'updatelist',
        Status: { 'player-guid-1': 'pending' },
        CreatedAt: '2026-06-22 10:30:00',
        ExpiresAt: '2026-06-22 10:31:00',
      },
      {
        id: 'cmd-latest',
        PlayerIds: ['player-guid-1'],
        Command: 'updatelist',
        Status: { 'player-guid-1': 'pending' },
        CreatedAt: '2026-06-22 10:15:00',
      },
      {
        id: 'cmd-old-sent',
        PlayerIds: ['player-guid-1'],
        Command: 'check',
        Status: { 'player-guid-1': 'sent' },
        CreatedAt: '2026-06-22 08:00:00',
        UpdatedAt: '2026-06-22 08:00:01',
      },
      {
        id: 'cmd-pending-b',
        PlayerIds: ['PLAYER-GUID-1'],
        Command: 'getmac',
        Status: { 'player-guid-1': 'pending' },
        CreatedAt: '2026-06-22 09:00:00',
      },
      {
        id: 'cmd-pending-a',
        PlayerIds: ['PLAYER-GUID-1'],
        Command: 'getmac',
        Status: { 'player-guid-1': 'pending' },
        CreatedAt: '2026-06-22 09:00:00',
      },
      {
        id: 'cmd-other-player',
        PlayerIds: ['other'],
        Command: 'check',
        Status: { other: 'pending' },
        CreatedAt: '2026-06-22 07:00:00',
      },
    ];
    const fetch = vi.fn(() => createObservable(rows));
    const orderedFetch = vi.fn(() => createObservable(rows));
    const orderedLimit = vi.fn(() => ({ fetch }));
    const order = vi.fn(() => ({ fetch: orderedFetch, limit: orderedLimit }));
    const collectionLimit = vi.fn(() => ({ fetch }));
    const collection = vi.fn(() => ({
      order,
      limit: collectionLimit,
      fetch,
      find: vi.fn(),
      update: vi.fn(),
    }));
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect: vi.fn() }));

    const client = new CommandQueueClient('turtlesrv.ddns.net');
    const commands = await client.fetchPendingCommands('player-guid-1');

    expect(order).toHaveBeenCalledWith(['CreatedAt', 'id'], 'ascending');
    expect(orderedFetch).toHaveBeenCalledOnce();
    expect(orderedLimit).not.toHaveBeenCalled();
    expect(collectionLimit).not.toHaveBeenCalled();
    expect(commands.map((entry) => entry.id)).toEqual([
      'cmd-old-sent',
      'cmd-pending-a',
      'cmd-pending-b',
      'cmd-latest',
    ]);
  });

  it('플레이어별 Status를 ack로 갱신한다', async () => {
    const update = vi.fn(() => createObservable({ replaced: 1 }));
    const findFetch = vi.fn(() => createObservable({
      id: 'cmd-1',
      Status: {
        'player-guid-1': 'pending',
        other: 'pending',
      },
    }));
    const collection = vi.fn(() => ({
      order: vi.fn(),
      limit: vi.fn(),
      find: vi.fn(() => ({ fetch: findFetch })),
      update,
    }));
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect: vi.fn() }));

    const client = new CommandQueueClient('turtlesrv.ddns.net');
    await client.markAck('cmd-1', 'PLAYER-GUID-1');

    expect(update).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-1',
      Status: {
        'player-guid-1': 'ack',
        other: 'pending',
      },
    }));
  });

  it('가장 오래된 sent 명령이 재시도 대기 중이면 더 최신 pending 명령을 반환하지 않는다', async () => {
    const rows = [
      {
        id: 'cmd-later-pending',
        PlayerIds: ['player-guid-1'],
        Command: 'getmac',
        Status: { 'player-guid-1': 'pending' },
        CreatedAt: '2026-06-22 09:05:00',
      },
      {
        id: 'cmd-earlier-retry-wait',
        PlayerIds: ['player-guid-1'],
        Command: 'updatelist',
        Status: { 'player-guid-1': 'sent' },
        CreatedAt: '2026-06-22 09:00:00',
        UpdatedAt: new Date().toISOString(),
      },
    ];
    const collection = vi.fn(() => ({
      order: vi.fn(() => ({ fetch: vi.fn(() => createObservable(rows)) })),
      fetch: vi.fn(() => createObservable(rows)),
      find: vi.fn(),
      update: vi.fn(),
    }));
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect: vi.fn() }));

    const client = new CommandQueueClient('turtlesrv.ddns.net');

    await expect(client.fetchNextPending('player-guid-1')).resolves.toBeNull();
  });
});
