import { beforeEach, describe, expect, it, vi } from 'vitest';
import { HeartbeatReporter } from '../src/app/heartbeat-reporter';
import type { HeartbeatPayload } from '../src/app/signalr-hub-client';

const RECORD_SEPARATOR = String.fromCharCode(0x1e);

class MockWebSocket {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSING = 2;
  static readonly CLOSED = 3;

  readyState = MockWebSocket.CONNECTING;
  onopen: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  onerror: (() => void) | null = null;
  onclose: (() => void) | null = null;
  readonly sent: string[] = [];

  constructor(readonly url: string) {}

  send(data: string): void {
    this.sent.push(data);
  }

  close(): void {
    this.readyState = MockWebSocket.CLOSED;
    this.onclose?.();
  }

  open(): void {
    this.readyState = MockWebSocket.OPEN;
    this.onopen?.();
    this.onmessage?.({ data: `{}${RECORD_SEPARATOR}` });
  }

  complete(invocationId: string): void {
    this.onmessage?.({
      data: `${JSON.stringify({ type: 3, invocationId })}${RECORD_SEPARATOR}`,
    });
  }
}

describe('HeartbeatReporter', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.stubGlobal('WebSocket', MockWebSocket);
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      json: async () => ({ connectionToken: 'connection-token-1' }),
    } as Response)));
  });

  it('종료 시 offline heartbeat를 ReportHeartbeat로 보낸다', async () => {
    let socket: MockWebSocket | null = null;
    vi.stubGlobal('WebSocket', class extends MockWebSocket {
      constructor(url: string) {
        super(url);
        socket = this;
      }
    });

    const reporter = new HeartbeatReporter({
      signalrUrl: 'http://10.0.0.30:5000/Data',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      getVersion: () => '1.0.0',
      getStatus: () => 'playing',
      getProcess: () => 0,
      getCurrentPage: () => 'page',
      getHdmiState: () => true,
    });

    const stopPromise = reporter.stop();
    await vi.waitFor(() => expect(socket).not.toBeNull());
    if (!socket) {
      throw new Error('mock websocket was not created');
    }

    const activeSocket = socket as MockWebSocket;
    activeSocket.open();
    await vi.waitFor(() => expect(activeSocket.sent.length).toBeGreaterThan(1));

    const invocation = JSON.parse(activeSocket.sent[1].replace(RECORD_SEPARATOR, '')) as {
      invocationId: string;
      target: string;
      arguments: HeartbeatPayload[];
    };
    expect(invocation.target).toBe('ReportHeartbeat');
    expect(invocation.arguments[0]).toEqual({
      ClientId: 'player-guid-1',
      Status: 'stopped',
      Process: 0,
      Version: 'v1.0.0',
      CurrentPage: '',
      HdmiState: false,
    });

    activeSocket.complete(invocation.invocationId);
    await expect(stopPromise).resolves.toBeUndefined();
  });

  it('dispose는 offline heartbeat를 보내지 않고 reporter만 정리한다', () => {
    const fetchMock = vi.mocked(fetch);
    let socketCreated = false;
    vi.stubGlobal('WebSocket', class extends MockWebSocket {
      constructor(url: string) {
        super(url);
        socketCreated = true;
      }
    });

    const reporter = new HeartbeatReporter({
      signalrUrl: 'http://10.0.0.30:5000/Data',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      getVersion: () => '1.0.0',
      getStatus: () => 'playing',
      getProcess: () => 0,
      getCurrentPage: () => 'page',
      getHdmiState: () => true,
    });

    reporter.dispose();

    expect(socketCreated).toBe(false);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
