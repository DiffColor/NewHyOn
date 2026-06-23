const RECORD_SEPARATOR = String.fromCharCode(0x1e);
const CONNECT_TIMEOUT_MS = 15000;
const HEARTBEAT_TIMEOUT_MS = 5000;

export interface HeartbeatPayload {
  readonly ClientId: string;
  readonly Status: string;
  readonly Process: number;
  readonly Version: string;
  readonly CurrentPage: string;
  readonly HdmiState: boolean;
}

export interface SignalRMessage {
  readonly from?: string;
  readonly to?: string;
  readonly command?: string;
  readonly dataType?: string;
  readonly data?: unknown;
  readonly From?: string;
  readonly To?: string;
  readonly Command?: string;
  readonly DataType?: string;
  readonly Data?: unknown;
}

export interface SignalRCommandEnvelope {
  readonly commandId?: string;
  readonly command?: string;
  readonly playerId?: string;
  readonly payloadJson?: string;
  readonly createdAt?: string;
  readonly isUrgent?: boolean;
  readonly CommandId?: string;
  readonly Command?: string;
  readonly PlayerId?: string;
  readonly PayloadJson?: string;
  readonly CreatedAt?: string;
  readonly IsUrgent?: boolean;
}

export interface SignalRHubClientOptions {
  readonly signalrUrl: string;
  readonly playerName: string;
  readonly playerGuid: string;
  readonly onStatus?: (status: string, detail: string) => void;
  readonly onMessage?: (message: SignalRMessage) => void;
  readonly webSocketFactory?: (url: string) => WebSocket;
}

interface SignalRNegotiateResponse {
  readonly connectionToken?: string;
  readonly connectionId?: string;
}

interface PendingInvocation {
  readonly resolve: () => void;
  readonly reject: (error: Error) => void;
  readonly timerId: number;
}

export class SignalRHubClient {
  private readonly endpointUrl: string;
  private readonly webSocketFactory: (url: string) => WebSocket;
  private playerName: string;
  private playerGuid: string;
  private socket: WebSocket | null = null;
  private connectPromise: Promise<void> | null = null;
  private handshakeComplete = false;
  private nextInvocationId = 1;
  private readonly pendingInvocations = new Map<string, PendingInvocation>();

  constructor(private readonly options: SignalRHubClientOptions) {
    this.endpointUrl = options.signalrUrl.replace(/\/$/, '');
    this.webSocketFactory = options.webSocketFactory ?? ((url) => new WebSocket(url));
    this.playerName = options.playerName;
    this.playerGuid = options.playerGuid;
  }

  async start(): Promise<void> {
    await this.ensureConnected();
  }

  async sendHeartbeat(payload: HeartbeatPayload): Promise<void> {
    try {
      await this.invokeHeartbeat(payload);
      return;
    } catch (error) {
      this.stop();
      await this.invokeHeartbeat(payload);
    }
  }

  setIdentity(playerName: string, playerGuid: string): void {
    const nextPlayerName = playerName.trim();
    const nextPlayerGuid = playerGuid.trim();
    if (
      this.playerName.toLowerCase() === nextPlayerName.toLowerCase()
      && this.playerGuid.toLowerCase() === nextPlayerGuid.toLowerCase()
    ) {
      return;
    }

    this.playerName = nextPlayerName;
    this.playerGuid = nextPlayerGuid;
    this.stop();
  }

  private async invokeHeartbeat(payload: HeartbeatPayload): Promise<void> {
    await this.ensureConnected();
    const socket = this.requireOpenSocket();
    const invocationId = String(this.nextInvocationId++);
    const message = {
      type: 1,
      invocationId,
      target: 'ReportHeartbeat',
      arguments: [payload],
    };

    await new Promise<void>((resolve, reject) => {
      const timerId = window.setTimeout(() => {
        this.pendingInvocations.delete(invocationId);
        reject(new Error(`SignalR ReportHeartbeat 응답 시간이 초과되었습니다: ${HEARTBEAT_TIMEOUT_MS}ms`));
      }, HEARTBEAT_TIMEOUT_MS);

      this.pendingInvocations.set(invocationId, { resolve, reject, timerId });
      try {
        socket.send(`${JSON.stringify(message)}${RECORD_SEPARATOR}`);
      } catch (error) {
        window.clearTimeout(timerId);
        this.pendingInvocations.delete(invocationId);
        reject(error instanceof Error ? error : new Error(String(error)));
      }
    });
  }

  stop(): void {
    this.rejectPending(new Error('SignalR 연결이 종료되었습니다.'));
    this.connectPromise = null;
    this.handshakeComplete = false;
    const socket = this.socket;
    this.socket = null;
    if (socket && socket.readyState !== WebSocket.CLOSED && socket.readyState !== WebSocket.CLOSING) {
      socket.close();
    }
  }

  private async ensureConnected(): Promise<void> {
    if (this.socket?.readyState === WebSocket.OPEN && this.handshakeComplete) {
      return;
    }

    if (!this.connectPromise) {
      this.connectPromise = this.connect();
    }

    try {
      await this.connectPromise;
    } finally {
      this.connectPromise = null;
    }
  }

  private async connect(): Promise<void> {
    this.stop();
    this.options.onStatus?.('connecting', this.endpointUrl);
    const connectionToken = await this.negotiate();
    const wsUrl = this.buildWebSocketUrl(connectionToken);

    await new Promise<void>((resolve, reject) => {
      const socket = this.webSocketFactory(wsUrl);
      this.socket = socket;
      let settled = false;
      const timerId = window.setTimeout(() => {
        if (settled) {
          return;
        }
        settled = true;
        this.stop();
        reject(new Error(`SignalR 연결 시간이 초과되었습니다: ${CONNECT_TIMEOUT_MS}ms`));
      }, CONNECT_TIMEOUT_MS);

      socket.onopen = () => {
        socket.send(`${JSON.stringify({ protocol: 'json', version: 1 })}${RECORD_SEPARATOR}`);
      };
      socket.onmessage = (event) => {
        try {
          this.handleSocketMessage(String(event.data));
          if (!settled && this.handshakeComplete) {
            settled = true;
            window.clearTimeout(timerId);
            this.options.onStatus?.('connected', this.endpointUrl);
            resolve();
          }
        } catch (error) {
          if (!settled) {
            settled = true;
            window.clearTimeout(timerId);
            reject(error instanceof Error ? error : new Error(String(error)));
          }
        }
      };
      socket.onerror = () => {
        if (settled) {
          return;
        }
        settled = true;
        window.clearTimeout(timerId);
        reject(new Error('SignalR WebSocket 오류가 발생했습니다.'));
      };
      socket.onclose = () => {
        this.handshakeComplete = false;
        this.rejectPending(new Error('SignalR 연결이 닫혔습니다.'));
        if (!settled) {
          settled = true;
          window.clearTimeout(timerId);
          reject(new Error('SignalR 연결이 완료되기 전에 닫혔습니다.'));
        }
      };
    });
  }

  private async negotiate(): Promise<string> {
    const response = await fetch(this.appendQuery(`${this.endpointUrl}/negotiate`, {
      negotiateVersion: '1',
    }), {
      method: 'POST',
      cache: 'no-store',
    });
    if (!response.ok) {
      throw new Error(`SignalR negotiate 실패: ${response.status} ${response.statusText}`);
    }

    const body = await response.json() as SignalRNegotiateResponse;
    const token = body.connectionToken ?? body.connectionId ?? '';
    if (!token.trim()) {
      throw new Error('SignalR negotiate 응답에 connectionToken이 없습니다.');
    }

    return token;
  }

  private buildWebSocketUrl(connectionToken: string): string {
    const url = new URL(this.endpointUrl);
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    return this.appendQuery(url.toString(), { id: connectionToken });
  }

  private appendQuery(url: string, values: Record<string, string>): string {
    const next = new URL(url);
    next.searchParams.set('playerName', this.playerName);
    next.searchParams.set('playerGuid', this.playerGuid);
    Object.entries(values).forEach(([key, value]) => next.searchParams.set(key, value));
    return next.toString();
  }

  private handleSocketMessage(raw: string): void {
    const frames = raw.split(RECORD_SEPARATOR).filter((frame) => frame.length > 0);
    for (const frame of frames) {
      const message = JSON.parse(frame) as {
        type?: number;
        invocationId?: string;
        target?: string;
        arguments?: unknown[];
        error?: string;
      };
      if (!this.handshakeComplete && message.type === undefined) {
        if (message.error) {
          throw new Error(`SignalR handshake 실패: ${message.error}`);
        }
        this.handshakeComplete = true;
        continue;
      }

      if (message.type === 1 && message.target === 'ReceiveMessage') {
        this.handleReceiveMessage(message.arguments?.[0]);
        continue;
      }

      if (message.type === 3 && message.invocationId) {
        const pending = this.pendingInvocations.get(message.invocationId);
        if (!pending) {
          continue;
        }

        window.clearTimeout(pending.timerId);
        this.pendingInvocations.delete(message.invocationId);
        if (message.error) {
          pending.reject(new Error(`SignalR ReportHeartbeat 실패: ${message.error}`));
        } else {
          pending.resolve();
        }
      }
    }
  }

  private handleReceiveMessage(value: unknown): void {
    if (!value) {
      return;
    }

    let message: unknown = value;
    if (typeof value === 'string') {
      const trimmed = value.trim();
      if (!trimmed) {
        return;
      }
      message = JSON.parse(trimmed) as unknown;
    }

    if (!message || typeof message !== 'object') {
      return;
    }

    this.options.onMessage?.(message as SignalRMessage);
  }

  private requireOpenSocket(): WebSocket {
    if (!this.socket || this.socket.readyState !== WebSocket.OPEN || !this.handshakeComplete) {
      throw new Error('SignalR WebSocket이 연결되어 있지 않습니다.');
    }

    return this.socket;
  }

  private rejectPending(error: Error): void {
    for (const [invocationId, pending] of this.pendingInvocations.entries()) {
      window.clearTimeout(pending.timerId);
      pending.reject(error);
      this.pendingInvocations.delete(invocationId);
    }
  }
}
