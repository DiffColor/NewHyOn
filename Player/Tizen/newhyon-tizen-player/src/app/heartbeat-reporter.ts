import { SignalRHubClient, type HeartbeatPayload, type SignalRMessage } from './signalr-hub-client';
import type { PlayerIdentity } from './player-guid-sync';

const DEFAULT_INTERVAL_MS = 5000;
const MIN_INTERVAL_MS = 1000;
const UPDATE_KEEP_ALIVE_INTERVAL_MS = 5000;
const UPDATE_PROGRESS_MINIMUM_INTERVAL_MS = 500;
const STOPPED_STATUS = 'stopped';

export interface HeartbeatReporterOptions {
  readonly signalrUrl: string;
  readonly playerGuid: string;
  readonly playerName: string;
  readonly intervalMs?: number;
  readonly getVersion: () => string;
  readonly getStatus: () => string;
  readonly getProcess: () => number;
  readonly getCurrentPage: () => string;
  readonly getHdmiState: () => boolean;
  readonly onStatus?: (status: string, detail: string) => void;
  readonly onMessage?: (message: SignalRMessage) => void;
}

export class HeartbeatReporter {
  private readonly intervalMs: number;
  private client: SignalRHubClient;
  private identity: PlayerIdentity;
  private timerId: number | null = null;
  private sending = false;
  private stopped = false;
  private updateReportingSessionId = 0;
  private updateReportingActive = false;
  private lastUpdateStatus = 'UPDATING';
  private lastUpdateProgress = 0;
  private lastUpdateReportedAt = 0;

  constructor(private readonly options: HeartbeatReporterOptions) {
    this.intervalMs = Math.max(MIN_INTERVAL_MS, options.intervalMs ?? DEFAULT_INTERVAL_MS);
    this.identity = {
      playerGuid: options.playerGuid,
      playerName: options.playerName,
    };
    this.client = new SignalRHubClient({
      signalrUrl: options.signalrUrl,
      playerGuid: this.identity.playerGuid,
      playerName: this.identity.playerName,
      onStatus: options.onStatus,
      onMessage: options.onMessage,
    });
  }

  start(): void {
    if (this.stopped) {
      return;
    }

    if (this.timerId === null) {
      this.timerId = window.setInterval(() => {
        void this.sendNow();
      }, this.intervalMs);
    }

    void this.client.start()
      .then(() => this.sendNow())
      .catch((error) => this.options.onStatus?.('failed', formatError(error)));
  }

  async sendNow(): Promise<void> {
    if (this.stopped || this.sending) {
      return;
    }

    this.sending = true;
    try {
      const payload = await this.buildNextPayload();
      if (!payload) {
        return;
      }

      await this.client.sendHeartbeat(payload);
      this.options.onStatus?.('sent', `${payload.Status} ${payload.CurrentPage || '-'}`);
    } catch (error) {
      this.options.onStatus?.('failed', formatError(error));
      this.client.stop();
    } finally {
      this.sending = false;
    }
  }

  async stop(): Promise<void> {
    if (this.stopped) {
      return;
    }

    this.stopped = true;
    this.clearTimer();

    try {
      if (this.identity.playerGuid) {
        await this.client.sendHeartbeat(this.buildPayload(STOPPED_STATUS, 0, '', false));
        this.options.onStatus?.(STOPPED_STATUS, this.options.signalrUrl);
      }
    } catch (error) {
      this.options.onStatus?.('failed', formatError(error));
    } finally {
      this.client.stop();
    }
  }

  dispose(): void {
    if (this.stopped) {
      return;
    }

    this.stopped = true;
    this.clearTimer();
    this.client.stop();
  }

  beginUpdateReporting(): number {
    if (this.stopped) {
      return 0;
    }

    this.updateReportingSessionId += 1;
    if (this.updateReportingSessionId <= 0) {
      this.updateReportingSessionId = 1;
    }

    this.updateReportingActive = true;
    this.lastUpdateStatus = 'UPDATING';
    this.lastUpdateProgress = 0;
    this.lastUpdateReportedAt = 0;
    return this.updateReportingSessionId;
  }

  async reportUpdateNow(status: string, progress: number, force: boolean, sessionId: number): Promise<void> {
    if (this.stopped || !this.isCurrentUpdateSession(sessionId)) {
      return;
    }

    const normalizedStatus = normalizeUpdateStatus(status);
    const normalizedProgress = normalizeProcess(progress);
    const now = Date.now();
    const sameStatus = this.lastUpdateStatus.toLowerCase() === normalizedStatus.toLowerCase();
    if (!force && sameStatus && this.lastUpdateProgress === normalizedProgress) {
      return;
    }
    if (!force && sameStatus && this.lastUpdateReportedAt > 0 && now - this.lastUpdateReportedAt < UPDATE_PROGRESS_MINIMUM_INTERVAL_MS) {
      return;
    }

    this.lastUpdateStatus = normalizedStatus;
    this.lastUpdateProgress = normalizedProgress;
    this.lastUpdateReportedAt = now;
    await this.sendPayloadIfCurrent(this.buildPayload(normalizedStatus, normalizedProgress), sessionId);
  }

  async endUpdateReporting(sessionId: number, sendNormalHeartbeatNow: boolean): Promise<void> {
    if (!this.isCurrentUpdateSession(sessionId)) {
      return;
    }

    this.updateReportingActive = false;
    this.lastUpdateStatus = 'UPDATING';
    this.lastUpdateProgress = 0;
    this.lastUpdateReportedAt = 0;
    if (sendNormalHeartbeatNow) {
      await this.sendNow();
    }
  }

  private async buildNextPayload(): Promise<HeartbeatPayload | null> {
    if (!this.identity.playerGuid) {
      this.options.onStatus?.('guid-empty', this.identity.playerName);
      return null;
    }

    if (!this.updateReportingActive) {
      return this.buildPayload();
    }

    const now = Date.now();
    if (this.lastUpdateReportedAt > 0 && now - this.lastUpdateReportedAt < UPDATE_KEEP_ALIVE_INTERVAL_MS) {
      return null;
    }

    this.lastUpdateReportedAt = now;
    return this.buildPayload(this.lastUpdateStatus, this.lastUpdateProgress);
  }

  private buildPayload(
    status = this.options.getStatus(),
    process = this.options.getProcess(),
    currentPage = this.options.getCurrentPage(),
    hdmiState = this.options.getHdmiState(),
  ): HeartbeatPayload {
    return {
      ClientId: this.identity.playerGuid,
      Status: status.trim() || 'idle',
      Process: normalizeProcess(process),
      Version: normalizeVersion(this.options.getVersion()),
      CurrentPage: currentPage,
      HdmiState: hdmiState,
    };
  }

  private async sendPayloadIfCurrent(payload: HeartbeatPayload, sessionId: number): Promise<void> {
    if (!this.identity.playerGuid) {
      return;
    }
    if (!this.isCurrentUpdateSession(sessionId)) {
      return;
    }

    await this.client.sendHeartbeat({
      ...payload,
      ClientId: this.identity.playerGuid,
    });
  }

  private isCurrentUpdateSession(sessionId: number): boolean {
    return !this.stopped
      && this.updateReportingActive
      && sessionId > 0
      && this.updateReportingSessionId === sessionId;
  }

  private clearTimer(): void {
    if (this.timerId !== null) {
      window.clearInterval(this.timerId);
      this.timerId = null;
    }
  }
}

function normalizeProcess(process: number): number {
  if (!Number.isFinite(process)) {
    return 0;
  }

  return Math.min(100, Math.max(0, Math.round(process)));
}

function normalizeUpdateStatus(status: string): string {
  const normalized = status.trim();
  return normalized ? normalized.toUpperCase() : 'UPDATING';
}

function normalizeVersion(version: string): string {
  const normalized = version.trim();
  if (!normalized) {
    return 'v0.0.0';
  }

  return normalized.startsWith('v') ? normalized : `v${normalized}`;
}

function formatError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  return String(error);
}
