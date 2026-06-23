import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RemoteCommandService } from '../src/app/remote-command-service';

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

function encodePayload(value: unknown): string {
  return window.btoa(JSON.stringify(value));
}

describe('RemoteCommandService', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    horizonFactory.mockReset();
  });

  it('CommandQueue pending updatelist를 처리하고 ack 및 CommandHistory done을 기록한다', async () => {
    const updateQueue = vi.fn(() => createObservable({ replaced: 1 }));
    const upsertHistory = vi.fn(() => createObservable({ inserted: 1 }));
    const updateHistory = vi.fn(() => createObservable({ replaced: 1 }));
    const command = {
      id: 'cmd-1',
      PlayerIds: ['player-guid-1'],
      Command: 'updatelist',
      payloadJson: encodePayload({
        PageList: { PLI_PageListName: 'remote-list' },
        Pages: [{
          PIC_PageName: 'page',
          PIC_PlaytimeSecond: 10,
          PIC_Elements: [],
        }],
      }),
      Status: { 'player-guid-1': 'pending' },
      CreatedAt: '2026-06-22 09:00:00',
    };
    const horizonCollection = vi.fn((name: string) => {
      if (name === 'CommandQueue') {
        return {
          order: vi.fn(() => ({ limit: vi.fn(() => ({ fetch: vi.fn(() => createObservable([command])) })) })),
          limit: vi.fn(() => ({ fetch: vi.fn(() => createObservable([command])) })),
          find: vi.fn(() => ({ fetch: vi.fn(() => createObservable(command)) })),
          update: updateQueue,
        };
      }

      return {
        upsert: upsertHistory,
        update: updateHistory,
      };
    });
    horizonFactory.mockReturnValue(Object.assign(horizonCollection, { disconnect: vi.fn() }));
    const onUpdateList = vi.fn(async () => true);
    const service = new RemoteCommandService({
      managerAddress: 'turtlesrv.ddns.net',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      onUpdateList,
      onCheck: vi.fn(async () => true),
      onGetMac: vi.fn(async () => true),
      onReboot: vi.fn(async () => true),
      onPowerOff: vi.fn(async () => true),
    });

    await service.checkNow();

    expect(onUpdateList).toHaveBeenCalledWith(expect.objectContaining({
      PageList: { PLI_PageListName: 'remote-list' },
    }), false, 'cmd-1');
    expect(updateQueue).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-1',
      AttemptCount: 1,
    }));
    expect(updateQueue).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-1',
      Status: {
        'player-guid-1': 'ack',
      },
    }));
    expect(upsertHistory).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-1',
      command: 'updatelist',
      refQueueId: 'cmd-1',
      status: 'queued',
    }));
    expect(updateHistory).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-1',
      status: 'done',
    }));
  });

  it('Pages가 비어 있는 updatelist도 인트로 재생 대상으로 ack한다', async () => {
    const updateQueue = vi.fn(() => createObservable({ replaced: 1 }));
    const upsertHistory = vi.fn(() => createObservable({ inserted: 1 }));
    const updateHistory = vi.fn(() => createObservable({ replaced: 1 }));
    const command = {
      id: 'cmd-empty-list',
      PlayerIds: ['player-guid-1'],
      Command: 'updatelist',
      payloadJson: encodePayload({
        PageList: { PLI_PageListName: 'empty-list' },
        Pages: [],
      }),
      Status: { 'player-guid-1': 'pending' },
      CreatedAt: '2026-06-22 09:00:00',
    };
    const horizonCollection = vi.fn((name: string) => {
      if (name === 'CommandQueue') {
        return {
          order: vi.fn(() => ({ limit: vi.fn(() => ({ fetch: vi.fn(() => createObservable([command])) })) })),
          limit: vi.fn(() => ({ fetch: vi.fn(() => createObservable([command])) })),
          find: vi.fn(() => ({ fetch: vi.fn(() => createObservable(command)) })),
          update: updateQueue,
        };
      }

      return {
        upsert: upsertHistory,
        update: updateHistory,
      };
    });
    horizonFactory.mockReturnValue(Object.assign(horizonCollection, { disconnect: vi.fn() }));
    const onUpdateList = vi.fn(async () => true);
    const service = new RemoteCommandService({
      managerAddress: 'turtlesrv.ddns.net',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      onUpdateList,
      onCheck: vi.fn(async () => true),
      onGetMac: vi.fn(async () => true),
      onReboot: vi.fn(async () => true),
      onPowerOff: vi.fn(async () => true),
    });

    await service.checkNow();

    expect(onUpdateList).toHaveBeenCalledWith(expect.objectContaining({
      PageList: { PLI_PageListName: 'empty-list' },
      Pages: [],
    }), false, 'cmd-empty-list');
    expect(updateQueue).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-empty-list',
      Status: {
        'player-guid-1': 'ack',
      },
    }));
    expect(updateHistory).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-empty-list',
      status: 'done',
    }));
  });

  it('SignalR CommandQueue envelope도 같은 처리 경로로 ack한다', async () => {
    const updateQueue = vi.fn(() => createObservable({ replaced: 1 }));
    const upsertHistory = vi.fn(() => createObservable({ inserted: 1 }));
    const updateHistory = vi.fn(() => createObservable({ replaced: 1 }));
    const command = {
      id: 'cmd-2',
      Status: { 'player-guid-1': 'sent' },
    };
    const horizonCollection = vi.fn((name: string) => {
      if (name === 'CommandQueue') {
        return {
          order: vi.fn(),
          limit: vi.fn(),
          find: vi.fn(() => ({ fetch: vi.fn(() => createObservable(command)) })),
          update: updateQueue,
        };
      }

      return {
        upsert: upsertHistory,
        update: updateHistory,
      };
    });
    horizonFactory.mockReturnValue(Object.assign(horizonCollection, { disconnect: vi.fn() }));
    const onCheck = vi.fn(async () => true);
    const service = new RemoteCommandService({
      managerAddress: 'turtlesrv.ddns.net',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      onUpdateList: vi.fn(async () => true),
      onCheck,
      onGetMac: vi.fn(async () => true),
      onReboot: vi.fn(async () => true),
      onPowerOff: vi.fn(async () => true),
    });

    service.handleSignalRMessage({
      dataType: 'CommandQueue',
      data: {
        commandId: 'cmd-2',
        command: 'check',
      },
    });
    await vi.waitFor(() => expect(onCheck).toHaveBeenCalledTimes(1));

    expect(updateQueue).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-2',
      Status: {
        'player-guid-1': 'ack',
      },
    }));
  });

  it('SignalR CommandQueue 알림에 payload가 없어도 Queue row를 다시 읽어 updatelist를 처리한다', async () => {
    const updateQueue = vi.fn(() => createObservable({ replaced: 1 }));
    const upsertHistory = vi.fn(() => createObservable({ inserted: 1 }));
    const updateHistory = vi.fn(() => createObservable({ replaced: 1 }));
    const command = {
      id: 'cmd-signalr-list',
      PlayerIds: ['player-guid-1'],
      Command: 'updatelist',
      payloadJson: encodePayload({
        PageList: { PLI_PageListName: 'signalr-list' },
        Pages: [{
          PIC_PageName: 'page',
          PIC_PlaytimeSecond: 10,
          PIC_Elements: [],
        }],
      }),
      Status: { 'player-guid-1': 'sent' },
      CreatedAt: '2026-06-22 09:00:00',
    };
    const horizonCollection = vi.fn((name: string) => {
      if (name === 'CommandQueue') {
        return {
          order: vi.fn(),
          limit: vi.fn(),
          find: vi.fn(() => ({ fetch: vi.fn(() => createObservable(command)) })),
          update: updateQueue,
        };
      }

      return {
        upsert: upsertHistory,
        update: updateHistory,
      };
    });
    horizonFactory.mockReturnValue(Object.assign(horizonCollection, { disconnect: vi.fn() }));
    const onUpdateList = vi.fn(async () => true);
    const service = new RemoteCommandService({
      managerAddress: 'turtlesrv.ddns.net',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      onUpdateList,
      onCheck: vi.fn(async () => true),
      onGetMac: vi.fn(async () => true),
      onReboot: vi.fn(async () => true),
      onPowerOff: vi.fn(async () => true),
    });

    service.handleSignalRMessage({
      dataType: 'CommandQueue',
      data: {
        commandId: 'cmd-signalr-list',
        command: 'updatelist',
      },
    });
    await vi.waitFor(() => expect(onUpdateList).toHaveBeenCalledTimes(1));

    expect(onUpdateList).toHaveBeenCalledWith(expect.objectContaining({
      PageList: { PLI_PageListName: 'signalr-list' },
    }), false, 'cmd-signalr-list');
    expect(updateQueue).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-signalr-list',
      AttemptCount: 1,
    }));
    expect(updateQueue).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-signalr-list',
      Status: {
        'player-guid-1': 'ack',
      },
    }));
  });

  it('CommandQueue pending updateweekly를 처리한다', async () => {
    const updateQueue = vi.fn(() => createObservable({ replaced: 1 }));
    const upsertHistory = vi.fn(() => createObservable({ inserted: 1 }));
    const updateHistory = vi.fn(() => createObservable({ replaced: 1 }));
    const command = {
      id: 'cmd-weekly',
      PlayerIds: ['player-guid-1'],
      Command: 'updateweekly',
      payloadJson: encodePayload({
        Schedule: {
          WeeklySchedule: {
            MonSch: {
              IsOnAir: false,
              StartHour: 9,
              StartMinute: 0,
              EndHour: 18,
              EndMinute: 30,
            },
          },
        },
      }),
      Status: { 'player-guid-1': 'pending' },
      CreatedAt: '2026-06-22 09:00:00',
    };
    const horizonCollection = vi.fn((name: string) => {
      if (name === 'CommandQueue') {
        return {
          order: vi.fn(() => ({ limit: vi.fn(() => ({ fetch: vi.fn(() => createObservable([command])) })) })),
          limit: vi.fn(() => ({ fetch: vi.fn(() => createObservable([command])) })),
          find: vi.fn(() => ({ fetch: vi.fn(() => createObservable(command)) })),
          update: updateQueue,
        };
      }

      return {
        upsert: upsertHistory,
        update: updateHistory,
      };
    });
    horizonFactory.mockReturnValue(Object.assign(horizonCollection, { disconnect: vi.fn() }));
    const onUpdateWeekly = vi.fn(async () => true);
    const service = new RemoteCommandService({
      managerAddress: 'turtlesrv.ddns.net',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      onUpdateList: vi.fn(async () => true),
      onUpdateWeekly,
      onCheck: vi.fn(async () => true),
      onGetMac: vi.fn(async () => true),
      onReboot: vi.fn(async () => true),
      onPowerOff: vi.fn(async () => true),
    });

    await service.checkNow();

    expect(onUpdateWeekly).toHaveBeenCalledWith(expect.objectContaining({
      Schedule: expect.objectContaining({
        WeeklySchedule: expect.any(Object),
      }),
    }));
    expect(updateQueue).toHaveBeenCalledWith(expect.objectContaining({
      id: 'cmd-weekly',
      Status: {
        'player-guid-1': 'ack',
      },
    }));
  });
});
