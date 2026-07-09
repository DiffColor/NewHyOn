import { createRemoteEnvelope, parseRemoteEnvelope, type RemoteEnvelope } from './remote-streaming-protocol';

interface SignalingClientOptions {
  readonly gatewayUrl: string;
  readonly deviceId: string;
  readonly sessionId: string;
  readonly helloPayload: Record<string, unknown>;
  readonly reconnectDelayMs?: number;
  readonly onStatus?: (status: string, detail: string) => void;
  readonly onMessage?: (message: RemoteEnvelope) => void;
}

const DEFAULT_RECONNECT_DELAY_MS = 5000;

export class SignalingClient {
  private socket: WebSocket | null = null;
  private reconnectTimerId: number | null = null;
  private stopped = true;

  constructor(private readonly options: SignalingClientOptions) {}

  get isOpen(): boolean {
    return this.socket?.readyState === WebSocket.OPEN;
  }

  start(): void {
    if (!this.stopped) {
      return;
    }
    this.stopped = false;
    this.connect();
  }

  stop(): void {
    this.stopped = true;
    if (this.reconnectTimerId !== null) {
      window.clearTimeout(this.reconnectTimerId);
      this.reconnectTimerId = null;
    }
    const socket = this.socket;
    this.socket = null;
    if (socket && socket.readyState !== WebSocket.CLOSED && socket.readyState !== WebSocket.CLOSING) {
      socket.close(1000, 'player stopped');
    }
  }

  send(type: string, payload: Record<string, unknown>): boolean {
    return this.sendEnvelope(createRemoteEnvelope(type, this.options.deviceId, this.options.sessionId, payload));
  }

  sendEnvelope(envelope: RemoteEnvelope): boolean {
    if (!this.isOpen || !this.socket) {
      return false;
    }
    this.socket.send(JSON.stringify(envelope));
    return true;
  }

  sendBinary(bytes: Uint8Array): boolean {
    if (!this.isOpen || !this.socket) {
      return false;
    }
    this.socket.send(bytes);
    return true;
  }

  private connect(): void {
    if (this.stopped || !this.options.gatewayUrl) {
      return;
    }

    try {
      const socket = new WebSocket(this.options.gatewayUrl);
      socket.binaryType = 'arraybuffer';
      this.socket = socket;
      this.options.onStatus?.('connecting', this.options.gatewayUrl);

      socket.onopen = () => {
        this.options.onStatus?.('open', this.options.gatewayUrl);
        this.send('hello', this.options.helloPayload);
      };

      socket.onmessage = (event) => {
        if (typeof event.data !== 'string') {
          return;
        }
        let parsed: unknown;
        try {
          parsed = JSON.parse(event.data);
        } catch {
          return;
        }
        const envelope = parseRemoteEnvelope(parsed);
        if (envelope) {
          this.options.onMessage?.(envelope);
        }
      };

      socket.onerror = () => {
        this.options.onStatus?.('error', this.options.gatewayUrl);
      };

      socket.onclose = () => {
        if (this.socket === socket) {
          this.socket = null;
        }
        this.options.onStatus?.('closed', this.options.gatewayUrl);
        this.scheduleReconnect();
      };
    } catch (error) {
      this.options.onStatus?.('error', error instanceof Error ? error.message : String(error));
      this.scheduleReconnect();
    }
  }

  private scheduleReconnect(): void {
    if (this.stopped || this.reconnectTimerId !== null) {
      return;
    }
    this.reconnectTimerId = window.setTimeout(() => {
      this.reconnectTimerId = null;
      this.connect();
    }, Math.max(1000, this.options.reconnectDelayMs ?? DEFAULT_RECONNECT_DELAY_MS));
  }
}
