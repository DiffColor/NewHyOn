import {
  REMOTE_STREAMING_BACKEND,
  type RemoteCommandPayload,
  type RemoteEnvelope,
  type RemoteStreamingPlaybackSnapshot,
  type RemoteStreamRequestPayload,
} from './remote-streaming-protocol';
import {
  DEFAULT_PLAYER_SETTINGS,
  DEFAULT_REMOTE_STREAMING_GATEWAY_URL,
  type PlayerSettings,
} from './player-settings';
import { SignalingClient } from './signaling-client';
import { StreamingCoordinator } from './streaming-coordinator';

interface RemoteStreamingServiceOptions {
  readonly gatewayUrl: string;
  readonly deviceId: string;
  readonly displayName: string;
  readonly getPlaybackSnapshot: () => RemoteStreamingPlaybackSnapshot;
  readonly onCommand: (command: string, payload: RemoteCommandPayload) => Promise<boolean> | boolean;
  readonly onStatus?: (status: string, detail: string) => void;
}

const HEALTH_INTERVAL_MS = 1000;

function normalizeCommand(value: unknown): string {
  return String(value ?? '').trim().toLowerCase();
}

function createSessionId(deviceId: string): string {
  return `player-${deviceId || 'tizen'}-${Date.now().toString(36)}`;
}

function buildGatewayUrl(rawGatewayUrl: string): string {
  const safe = rawGatewayUrl.trim();
  if (!safe) {
    return '';
  }
  if (/^wss?:\/\//i.test(safe)) {
    return safe;
  }
  if (/^https?:\/\//i.test(safe)) {
    const url = new URL(safe);
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    url.pathname = '/ws';
    url.search = '';
    url.hash = '';
    return url.toString();
  }
  const hostAndPath = safe.replace(/\/+$/g, '');
  return `ws://${hostAndPath.endsWith('/ws') ? hostAndPath : `${hostAndPath}/ws`}`;
}

function capabilities() {
  return {
    kind: 'tizen-web-player',
    hasSsspCaptureScreen: typeof window.webapis?.systemcontrol?.captureScreen === 'function',
    canPublishSsspFileCapture: Boolean(window.tizen?.filesystem?.resolve),
    hasRtcPeerConnection: typeof window.RTCPeerConnection === 'function',
    hasGetDisplayMedia: typeof navigator.mediaDevices?.getDisplayMedia === 'function',
  };
}

export class RemoteStreamingService {
  private readonly sessionId: string;
  private readonly signaling: SignalingClient;
  private readonly coordinator: StreamingCoordinator;
  private healthTimerId: number | null = null;

  constructor(private readonly options: RemoteStreamingServiceOptions) {
    this.sessionId = createSessionId(options.deviceId);
    this.signaling = new SignalingClient({
      gatewayUrl: buildGatewayUrl(options.gatewayUrl),
      deviceId: options.deviceId,
      sessionId: this.sessionId,
      helloPayload: {
        role: 'player',
        deviceId: options.deviceId,
        displayName: options.displayName,
        capabilities: capabilities(),
      },
      onStatus: (status, detail) => this.reportStatus(`ws:${status}`, detail),
      onMessage: (message) => {
        void this.handleMessage(message);
      },
    });
    this.coordinator = new StreamingCoordinator({
      signaling: this.signaling,
      getPlaybackSnapshot: options.getPlaybackSnapshot,
      onStatus: (status, detail) => this.reportStatus(`stream:${status}`, detail),
    });
  }

  start(): void {
    this.signaling.start();
    this.healthTimerId = window.setInterval(() => this.sendPlaybackHealth(), HEALTH_INTERVAL_MS);
    this.sendPlaybackHealth();
  }

  stop(): void {
    if (this.healthTimerId !== null) {
      window.clearInterval(this.healthTimerId);
      this.healthTimerId = null;
    }
    this.coordinator.stop('service-stopped');
    this.signaling.stop();
  }

  private sendPlaybackHealth(): void {
    this.signaling.send('playback.health', {
      backend: REMOTE_STREAMING_BACKEND,
      playback: this.options.getPlaybackSnapshot(),
      capabilities: capabilities(),
      streamActive: this.coordinator.isActive,
    });
  }

  private async handleMessage(message: RemoteEnvelope): Promise<void> {
    if (message.deviceId && message.deviceId !== this.options.deviceId) {
      return;
    }

    if (message.type === 'stream.request') {
      this.coordinator.start(message.payload as RemoteStreamRequestPayload);
      return;
    }

    if (message.type === 'stream.stop') {
      this.coordinator.stop('viewer-stop');
      return;
    }

    if (message.type === 'remote.command') {
      await this.handleRemoteCommand(message.payload as RemoteCommandPayload);
    }
  }

  private async handleRemoteCommand(payload: RemoteCommandPayload): Promise<void> {
    const command = normalizeCommand(payload.command || payload.action);
    if (!command) {
      this.signaling.send('remote.command.result', {
        ok: false,
        error: 'EMPTY_COMMAND',
      });
      return;
    }

    if (command === 'sssp-snapshot') {
      const ok = await this.coordinator.captureSnapshot();
      this.signaling.send('remote.command.result', { command, ok });
      return;
    }

    const ok = await this.options.onCommand(command, payload);
    this.signaling.send('remote.command.result', { command, ok });
  }

  private reportStatus(status: string, detail: string): void {
    this.options.onStatus?.(status, detail);
  }
}

export function buildRemoteStreamingGatewayUrl(managerAddress: string): string {
  return buildGatewayUrl(managerAddress);
}

export function resolveRemoteStreamingGatewayUrl(
  settings: Pick<PlayerSettings, 'managerAddress' | 'remoteStreamingGatewayUrl'>,
): string {
  const configuredGatewayUrl = settings.remoteStreamingGatewayUrl.trim();
  if (configuredGatewayUrl) {
    return buildGatewayUrl(configuredGatewayUrl);
  }

  const managerAddress = settings.managerAddress.trim();
  if (
    DEFAULT_REMOTE_STREAMING_GATEWAY_URL
    && (!managerAddress || managerAddress === DEFAULT_PLAYER_SETTINGS.managerAddress)
  ) {
    return buildGatewayUrl(DEFAULT_REMOTE_STREAMING_GATEWAY_URL);
  }

  return buildRemoteStreamingGatewayUrl(settings.managerAddress);
}

export function resolveRemoteStreamingDeviceId(
  settings: Pick<PlayerSettings, 'playerId'>,
  playerGuid: string | null | undefined,
  duid: string | null | undefined,
): string {
  const serverGuid = playerGuid?.trim() ?? '';
  if (serverGuid) {
    return serverGuid;
  }

  const configuredPlayerId = settings.playerId.trim();
  if (configuredPlayerId && configuredPlayerId !== DEFAULT_PLAYER_SETTINGS.playerId) {
    return configuredPlayerId;
  }

  const deviceDuid = duid?.trim() ?? '';
  return deviceDuid || configuredPlayerId || DEFAULT_PLAYER_SETTINGS.playerId;
}
