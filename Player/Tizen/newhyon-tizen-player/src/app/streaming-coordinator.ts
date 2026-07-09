import { PlaybackImpactGuard } from './playback-impact-guard';
import {
  REMOTE_STREAMING_BACKEND,
  type RemoteStreamingPlaybackSnapshot,
  type RemoteStreamProfile,
  type RemoteStreamRequestPayload,
} from './remote-streaming-protocol';
import type { SignalingClient } from './signaling-client';

interface StreamingCoordinatorOptions {
  readonly signaling: SignalingClient;
  readonly getPlaybackSnapshot: () => RemoteStreamingPlaybackSnapshot;
  readonly onStatus?: (status: string, detail: string) => void;
}

interface CaptureCandidate {
  readonly root: string;
  readonly relativePath?: string;
}

const CAPTURE_CANDIDATES: CaptureCandidate[] = [
  { root: 'wgt-private-tmp', relativePath: 'ScreenCapture.jpg' },
  { root: 'wgt-private-data', relativePath: 'ScreenCapture.jpg' },
  { root: '/opt/share/magicinfo/ScreenCapture.jpg' },
  { root: 'documents', relativePath: 'ScreenCapture.jpg' },
];

const FPS_LADDER = [1, 2, 3, 5, 8, 10, 15] as const;
const CAPTURE_FILE_SETTLE_DELAY_MS = 120;
const CAPTURE_FILE_RETRY_WINDOW_MS = 1500;
const UPSHIFT_STABLE_FRAME_THRESHOLD = 24;

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });
}

function formatError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }
  if (error && typeof error === 'object') {
    const source = error as Record<string, unknown>;
    const name = typeof source.name === 'string' ? source.name : '';
    const message = typeof source.message === 'string' ? source.message : '';
    const code = source.code === undefined ? '' : String(source.code);
    const parts = [name, message, code ? `code=${code}` : ''].filter(Boolean);
    if (parts.length > 0) {
      return parts.join(' ');
    }
    try {
      return JSON.stringify(source);
    } catch {
      return Object.prototype.toString.call(error);
    }
  }
  return String(error);
}

function normalizeAction(value: unknown): string {
  return String(value ?? '').trim().toLowerCase();
}

function chooseInitialFps(profile: RemoteStreamProfile): number {
  const maxFps = Math.max(1, Math.min(15, Math.round(Number(profile.maxFps) || 1)));
  for (let index = FPS_LADDER.length - 1; index >= 0; index -= 1) {
    if (FPS_LADDER[index] <= maxFps) {
      return FPS_LADDER[index];
    }
  }
  return 1;
}

function downshiftFps(currentFps: number): number {
  const index = FPS_LADDER.findIndex((value) => value >= currentFps);
  return FPS_LADDER[Math.max(0, index - 1)] ?? 1;
}

function upshiftFps(currentFps: number, maxFps: number): number {
  const index = FPS_LADDER.findIndex((value) => value > currentFps && value <= maxFps);
  return index >= 0 ? FPS_LADDER[index] : currentFps;
}

function resolveTizenFile(candidate: CaptureCandidate): Promise<TizenFile> {
  const filesystem = window.tizen?.filesystem;
  if (!filesystem?.resolve) {
    return Promise.reject(new Error('Tizen filesystem API를 찾지 못했습니다.'));
  }

  return new Promise((resolve, reject) => {
    filesystem.resolve!(
      candidate.root,
      (file) => {
        try {
          resolve(candidate.relativePath ? file.resolve(candidate.relativePath) : file);
        } catch (error) {
          reject(error);
        }
      },
      reject,
      'r',
    );
  });
}

function readFileBytes(file: TizenFile): Promise<Uint8Array> {
  return new Promise((resolve, reject) => {
    file.openStream(
      'r',
      (stream) => {
        try {
          const byteCount = Math.max(0, Number(file.fileSize ?? stream.bytesAvailable ?? 0));
          if (!byteCount || typeof stream.readBytes !== 'function') {
            throw new Error('캡처 파일이 비어 있거나 readBytes를 지원하지 않습니다.');
          }
          resolve(Uint8Array.from(stream.readBytes(byteCount)));
        } catch (error) {
          reject(error);
        } finally {
          stream.close();
        }
      },
      reject,
    );
  });
}

function captureSsspScreen(): void {
  const systemcontrol = window.webapis?.systemcontrol;
  const captureScreen = systemcontrol?.captureScreen;
  if (typeof captureScreen !== 'function') {
    throw new Error('webapis.systemcontrol.captureScreen을 찾지 못했습니다.');
  }

  try {
    captureScreen.call(systemcontrol, 'wgt-private-tmp');
  } catch (firstError) {
    try {
      captureScreen.call(systemcontrol);
    } catch (secondError) {
      throw new Error(`captureScreen 실패: tmp=${formatError(firstError)} default=${formatError(secondError)}`);
    }
  }
}

async function readFirstCaptureFile(): Promise<Uint8Array> {
  const errors: string[] = [];
  for (const candidate of CAPTURE_CANDIDATES) {
    try {
      const file = await resolveTizenFile(candidate);
      const bytes = await readFileBytes(file);
      if (bytes.length > 0) {
        return bytes;
      }
      errors.push(`${candidate.root}: empty`);
    } catch (error) {
      errors.push(`${candidate.root}: ${formatError(error)}`);
    }
  }
  throw new Error(`캡처 파일을 읽지 못했습니다. ${errors.join(' / ')}`);
}

async function readCaptureFileWithRetry(): Promise<Uint8Array> {
  const startedAt = performance.now();
  let lastError: unknown = null;
  do {
    try {
      return await readFirstCaptureFile();
    } catch (error) {
      lastError = error;
      await delay(CAPTURE_FILE_SETTLE_DELAY_MS);
    }
  } while (performance.now() - startedAt < CAPTURE_FILE_RETRY_WINDOW_MS);
  throw lastError instanceof Error ? lastError : new Error(formatError(lastError));
}

export class StreamingCoordinator {
  private readonly guard = new PlaybackImpactGuard();
  private timerId: number | null = null;
  private active = false;
  private sequence = 0;
  private currentFps = 1;
  private stableFrameCount = 0;
  private profile: RemoteStreamProfile = {
    width: 960,
    height: 540,
    maxFps: 8,
    maxBitrateKbps: 650,
    reason: 'idle',
  };

  constructor(private readonly options: StreamingCoordinatorOptions) {}

  get isActive(): boolean {
    return this.active;
  }

  start(payload: RemoteStreamRequestPayload = {}): void {
    if (normalizeAction(payload.action || 'start') === 'stop') {
      this.stop('viewer-stop');
      return;
    }
    if (payload.backend && payload.backend !== REMOTE_STREAMING_BACKEND) {
      this.reportStatus('error', `지원하지 않는 backend: ${payload.backend}`);
      return;
    }

    this.profile = this.guard.resolveProfile(this.options.getPlaybackSnapshot(), payload.requestedProfile);
    this.currentFps = chooseInitialFps(this.profile);
    this.stableFrameCount = 0;
    if (this.active) {
      this.reportStatus('active', `${this.currentFps}fps ${this.profile.reason ?? ''}`.trim());
      return;
    }

    this.active = true;
    this.sequence = 0;
    this.reportStatus('started', `${this.currentFps}fps ${this.profile.reason ?? ''}`.trim());
    this.scheduleNext(0);
  }

  stop(reason = 'stopped'): void {
    this.active = false;
    if (this.timerId !== null) {
      window.clearTimeout(this.timerId);
      this.timerId = null;
    }
    this.reportStatus('stopped', reason);
  }

  async captureSnapshot(): Promise<boolean> {
    try {
      const bytes = await this.captureFrameBytes();
      this.sendFrame(bytes, 'snapshot');
      this.options.signaling.send('diagnostic.snapshot', {
        backend: REMOTE_STREAMING_BACKEND,
        sequence: this.sequence,
        byteLength: bytes.byteLength,
        contentType: 'image/jpeg',
        playback: this.options.getPlaybackSnapshot(),
      });
      return true;
    } catch (error) {
      this.reportStatus('error', formatError(error));
      return false;
    }
  }

  private scheduleNext(delayMs: number): void {
    if (!this.active) {
      return;
    }
    if (this.timerId !== null) {
      window.clearTimeout(this.timerId);
    }
    this.timerId = window.setTimeout(() => {
      this.timerId = null;
      void this.tick();
    }, Math.max(0, delayMs));
  }

  private async tick(): Promise<void> {
    if (!this.active) {
      return;
    }

    const startedAt = performance.now();
    try {
      const snapshot = this.options.getPlaybackSnapshot();
      this.profile = this.guard.resolveProfile(snapshot, this.profile);
      const bytes = await this.captureFrameBytes();
      this.sendFrame(bytes, 'stream');
      this.adaptFps(performance.now() - startedAt);
    } catch (error) {
      this.reportStatus('error', formatError(error));
      this.currentFps = downshiftFps(this.currentFps);
    }

    const intervalMs = 1000 / Math.max(1, this.currentFps);
    this.scheduleNext(Math.max(40, intervalMs - (performance.now() - startedAt)));
  }

  private async captureFrameBytes(): Promise<Uint8Array> {
    captureSsspScreen();
    await delay(CAPTURE_FILE_SETTLE_DELAY_MS);
    return readCaptureFileWithRetry();
  }

  private sendFrame(bytes: Uint8Array, mode: 'stream' | 'snapshot'): void {
    const snapshot = this.options.getPlaybackSnapshot();
    this.sequence += 1;
    const metadataSent = this.options.signaling.send('stream.frame', {
      backend: REMOTE_STREAMING_BACKEND,
      mode,
      sequence: this.sequence,
      byteLength: bytes.byteLength,
      contentType: 'image/jpeg',
      width: this.profile.width,
      height: this.profile.height,
      fps: this.currentFps,
      profile: this.profile,
      playback: snapshot,
    });
    if (metadataSent) {
      this.options.signaling.sendBinary(bytes);
    }
    this.reportStatus('frame', `${this.sequence}:${bytes.byteLength} bytes`);
  }

  private adaptFps(elapsedMs: number): void {
    const targetIntervalMs = 1000 / Math.max(1, this.currentFps);
    if (elapsedMs > targetIntervalMs * 1.4) {
      this.currentFps = downshiftFps(this.currentFps);
      this.stableFrameCount = 0;
      return;
    }
    this.stableFrameCount += 1;
    if (this.stableFrameCount >= UPSHIFT_STABLE_FRAME_THRESHOLD) {
      const nextFps = upshiftFps(this.currentFps, this.profile.maxFps);
      if (nextFps !== this.currentFps) {
        this.currentFps = nextFps;
      }
      this.stableFrameCount = 0;
    }
  }

  private reportStatus(status: string, detail: string): void {
    this.options.onStatus?.(status, detail);
    this.options.signaling.send('stream.status', {
      backend: REMOTE_STREAMING_BACKEND,
      active: this.active,
      status,
      detail,
      sequence: this.sequence,
      fps: this.currentFps,
      profile: this.profile,
    });
  }
}
