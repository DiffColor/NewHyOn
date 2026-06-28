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

  it('플레이어 대상 최신 pending 및 오래된 sent 명령만 가져오고 교체/만료 명령은 제외한다', async () => {
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
        id: 'cmd-pending',
        PlayerIds: ['PLAYER-GUID-1'],
        Command: 'getmac',
        Status: { 'player-guid-1': 'pending' },
        CreatedAt: '2026-06-22 09:00:00',
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
        id: 'cmd-other-player',
        PlayerIds: ['other'],
        Command: 'check',
        Status: { other: 'pending' },
        CreatedAt: '2026-06-22 07:00:00',
      },
    ];
    const fetch = vi.fn(() => createObservable(rows));
    const collection = vi.fn(() => ({
      order: vi.fn(() => ({ limit: vi.fn(() => ({ fetch })) })),
      limit: vi.fn(() => ({ fetch })),
      find: vi.fn(),
      update: vi.fn(),
    }));
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect: vi.fn() }));

    const client = new CommandQueueClient('turtlesrv.ddns.net');
    const commands = await client.fetchPendingCommands('player-guid-1');

    expect(commands.map((entry) => entry.id)).toEqual(['cmd-latest', 'cmd-pending', 'cmd-old-sent']);
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
});
