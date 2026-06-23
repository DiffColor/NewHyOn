import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SignalRHubClient, type HeartbeatPayload } from '../src/app/signalr-hub-client';

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

function createPayload(): HeartbeatPayload {
  return {
    ClientId: 'player-guid-1',
    Status: 'playing',
    Process: 0,
    Version: 'v0.0.0',
    CurrentPage: 'page',
    HdmiState: true,
  };
}

describe('SignalRHubClient', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.stubGlobal('WebSocket', MockWebSocket);
  });

  it('SignalR negotiate 후 WebSocket JSON 프로토콜로 ReportHeartbeat를 호출한다', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      expect(String(input)).toBe('http://10.0.0.30:5000/Data/negotiate?playerName=tizen&playerGuid=player-guid-1&negotiateVersion=1');
      return {
        ok: true,
        json: async () => ({ connectionToken: 'connection-token-1' }),
      } as Response;
    });
    vi.stubGlobal('fetch', fetchMock);

    let socket: MockWebSocket | null = null;
    const client = new SignalRHubClient({
      signalrUrl: 'http://10.0.0.30:5000/Data',
      playerName: 'tizen',
      playerGuid: 'player-guid-1',
      webSocketFactory: (url) => {
        socket = new MockWebSocket(url);
        return socket as unknown as WebSocket;
      },
    });

    const sendPromise = client.sendHeartbeat(createPayload());
    await vi.waitFor(() => expect(socket).not.toBeNull());

    if (!socket) {
      throw new Error('mock websocket was not created');
    }
    const activeSocket = socket as unknown as MockWebSocket;

    expect(activeSocket.url).toBe('ws://10.0.0.30:5000/Data?playerName=tizen&playerGuid=player-guid-1&id=connection-token-1');
    activeSocket.open();
    await vi.waitFor(() => expect(activeSocket.sent.length).toBeGreaterThan(1));

    expect(activeSocket.sent[0]).toBe(`${JSON.stringify({ protocol: 'json', version: 1 })}${RECORD_SEPARATOR}`);
    const invocation = JSON.parse(activeSocket.sent[1].replace(RECORD_SEPARATOR, '')) as {
      invocationId: string;
      target: string;
      arguments: HeartbeatPayload[];
    };
    expect(invocation.target).toBe('ReportHeartbeat');
    expect(invocation.arguments[0]).toEqual(createPayload());

    activeSocket.complete(invocation.invocationId);
    await expect(sendPromise).resolves.toBeUndefined();
    client.stop();
  });

  it('서버 ReceiveMessage 프레임을 원격 명령 메시지로 전달한다', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      json: async () => ({ connectionToken: 'connection-token-1' }),
    } as Response)));

    let socket: MockWebSocket | null = null;
    const onMessage = vi.fn();
    const client = new SignalRHubClient({
      signalrUrl: 'http://10.0.0.30:5000/Data',
      playerName: 'tizen',
      playerGuid: 'player-guid-1',
      onMessage,
      webSocketFactory: (url) => {
        socket = new MockWebSocket(url);
        return socket as unknown as WebSocket;
      },
    });

    const startPromise = client.start();
    await vi.waitFor(() => expect(socket).not.toBeNull());
    const activeSocket = socket as unknown as MockWebSocket;
    activeSocket.open();
    await startPromise;

    activeSocket.onmessage?.({
      data: `${JSON.stringify({
        type: 1,
        target: 'ReceiveMessage',
        arguments: [{
          dataType: 'CommandQueue',
          data: { commandId: 'cmd-1', command: 'check' },
        }],
      })}${RECORD_SEPARATOR}`,
    });

    expect(onMessage).toHaveBeenCalledWith({
      dataType: 'CommandQueue',
      data: { commandId: 'cmd-1', command: 'check' },
    });
    client.stop();
  });
});
