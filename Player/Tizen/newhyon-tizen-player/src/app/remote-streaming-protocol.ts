export const REMOTE_STREAMING_PROTOCOL_VERSION = 1;
export const REMOTE_STREAMING_BACKEND = 'player-sssp-file-capture';

export interface RemoteEnvelope<TPayload = unknown> {
  readonly version: 1;
  readonly type: string;
  readonly id: string;
  readonly timestamp: string;
  readonly deviceId: string;
  readonly sessionId: string;
  readonly payload: TPayload;
}

export interface RemoteStreamProfile {
  readonly width: number;
  readonly height: number;
  readonly maxFps: number;
  readonly maxBitrateKbps: number;
  readonly reason?: string;
}

export interface RemoteStreamingPlaybackSnapshot {
  readonly state: 'idle' | 'preparing' | 'buffering' | 'playing';
  readonly playlistName: string;
  readonly pageName: string;
  readonly pageIndex: number;
  readonly pageCount: number;
  readonly width: number;
  readonly height: number;
}

export interface RemoteStreamRequestPayload {
  readonly action?: string;
  readonly backend?: string;
  readonly targetDeviceId?: string;
  readonly requestedProfile?: Partial<RemoteStreamProfile>;
}

export interface RemoteCommandPayload {
  readonly command?: string;
  readonly action?: string;
  readonly key?: string;
  readonly value?: unknown;
}

function createMessageId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `remote-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

export function createRemoteEnvelope<TPayload>(
  type: string,
  deviceId: string,
  sessionId: string,
  payload: TPayload,
): RemoteEnvelope<TPayload> {
  return {
    version: REMOTE_STREAMING_PROTOCOL_VERSION,
    type,
    id: createMessageId(),
    timestamp: new Date().toISOString(),
    deviceId,
    sessionId,
    payload,
  };
}

export function parseRemoteEnvelope(value: unknown): RemoteEnvelope | null {
  if (!value || typeof value !== 'object') {
    return null;
  }
  const candidate = value as RemoteEnvelope;
  if (candidate.version !== REMOTE_STREAMING_PROTOCOL_VERSION || typeof candidate.type !== 'string') {
    return null;
  }
  return candidate;
}
