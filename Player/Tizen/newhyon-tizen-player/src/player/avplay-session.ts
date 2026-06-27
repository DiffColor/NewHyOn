import type { RingLogger } from '../core/logger';
import type { SeamlessContentItem, SeamlessSlotPlan } from '../domain/page-plan';
import { resolveAvplaySourceUrl } from './source-resolver';

const DISPLAY_METHOD_FILL = 'PLAYER_DISPLAY_MODE_FULL_SCREEN';
const DISPLAY_METHOD_CONTAIN = 'PLAYER_DISPLAY_MODE_LETTER_BOX';
const AVPLAY_BASE_WIDTH = 1920;
const AVPLAY_BASE_HEIGHT = 1080;
const MAX_AVPLAYSTORE_PLAYERS = 4;
const PLAYERS_PER_SEAMLESS_SESSION = 2;
const FIRST_FRAME_READY_TIMEOUT_MS = 5000;
const STOPPABLE_STATES = new Set(['READY', 'PLAYING', 'PAUSED']);
const VIDEO_DURATION_MATCH_TOLERANCE_MS = 250;

export interface VideoSessionEvents {
  readonly onEnded: () => void;
  readonly onError: (message: string) => void;
}

type StreamEndedHandler = () => boolean | Promise<boolean> | void | Promise<void>;

interface AvplayLane {
  readonly player: AVPlayApi;
  readonly objectElement: HTMLObjectElement;
}

interface DisplayContext {
  readonly slot: SeamlessSlotPlan;
  readonly slotElement: HTMLElement;
}

interface PreparedLane {
  readonly laneIndex: number;
  readonly itemId: string;
  readonly durationMs: number | null;
}

export interface AvplayPlayOptions {
  readonly waitForFirstFrame?: boolean;
}

export interface AvplayPlaybackInfo {
  readonly durationMs: number | null;
}

interface FirstFrameReadyGate {
  readonly promise: Promise<void>;
  readonly markReady: (reason: string) => void;
  readonly fail: (message: string) => void;
  readonly cancel: () => void;
}

function formatAvplayError(error: AVPlayErrorLike | undefined, message: string): string {
  return String(message || error?.message || error?.code || error?.name || 'unknown');
}

function getPlayerState(player: AVPlayApi): string {
  return (player.getState?.() ?? 'UNKNOWN').toUpperCase();
}

export class AvplaySession {
  private currentItem: SeamlessContentItem | null = null;
  private currentEndedHandler: StreamEndedHandler | null = null;
  private currentLaneIndex: number | null = null;
  private heldLaneIndex: number | null = null;
  private displayContext: DisplayContext | null = null;
  private preparedLane: PreparedLane | null = null;
  private detachedLaneIndexes: number[] = [];
  private traceSeq = 0;
  private operationSeq = 0;

  constructor(
    readonly index: number,
    private readonly lanes: readonly [AvplayLane, AvplayLane],
    host: HTMLElement,
    private readonly logger: RingLogger,
    private readonly events: VideoSessionEvents,
  ) {
    this.lanes.forEach((lane) => {
      lane.objectElement.type = 'application/avplayer';
      lane.objectElement.className = 'avplay-object';
      lane.objectElement.style.visibility = 'hidden';
      lane.objectElement.setAttribute('aria-hidden', 'true');
      host.appendChild(lane.objectElement);
    });
  }

  async play(
    item: SeamlessContentItem,
    slot: SeamlessSlotPlan,
    slotElement: HTMLElement,
    preserveAspectRatio: boolean,
    onStreamEnded: StreamEndedHandler,
    options: AvplayPlayOptions = {},
  ): Promise<AvplayPlaybackInfo> {
    const operationId = this.nextOperationId();
    this.displayContext = { slot, slotElement };
    const preparedLaneIndex = this.preparedLane?.itemId === item.id ? this.preparedLane.laneIndex : null;
    const nextLaneIndex = preparedLaneIndex ?? (this.currentLaneIndex === 0 ? 1 : 0);
    const lane = this.lanes[nextLaneIndex];
    const sourceUrl = resolveAvplaySourceUrl(item.sourceUrl);
    const firstFrameReady = options.waitForFirstFrame
      ? this.createFirstFrameReadyGate(nextLaneIndex, item.name)
      : null;
    this.logger.info(
      'avplay',
      preparedLaneIndex === null
        ? `slot ${this.index} lane ${nextLaneIndex + 1} open: ${item.name}`
        : `slot ${this.index} lane ${nextLaneIndex + 1} play prepared: ${item.name}`,
    );
    try {
      const previousLaneIndex = this.currentLaneIndex;
      let durationMs = this.preparedLane?.itemId === item.id ? this.preparedLane.durationMs : null;
      if (preparedLaneIndex === null) {
        this.clearPreparedLane();
        this.resetLaneForPlayback(nextLaneIndex);
        this.configureLaneForItem(nextLaneIndex, item, sourceUrl, slot, slotElement, preserveAspectRatio, firstFrameReady);
        await this.prepareLaneAsync(nextLaneIndex, item.name);
        this.assertOperationCurrent(operationId, nextLaneIndex, 'play.prepareAsync', item.name);
        durationMs = this.readDurationMs(nextLaneIndex, item.name);
      } else {
        this.assertOperationCurrent(operationId, nextLaneIndex, 'play.preparedLane', item.name);
        this.callLane(nextLaneIndex, 'setListener', () => {
          lane.player.setListener(this.createLaneListener(nextLaneIndex, firstFrameReady));
        }, item.name);
        this.applyDisplayRectToLane(nextLaneIndex, slot, slotElement);
        this.setLaneDisplayMethod(nextLaneIndex, preserveAspectRatio ? DISPLAY_METHOD_CONTAIN : DISPLAY_METHOD_FILL);
      }
      this.setLaneLooping(nextLaneIndex, this.shouldLoopForDuration(item, durationMs));
      this.preparedLane = null;
      this.setLaneVideoStillMode(nextLaneIndex, 'false');
      this.assertOperationCurrent(operationId, nextLaneIndex, 'play.beforePlay', item.name);
      this.callLane(nextLaneIndex, 'play', () => {
        lane.player.play();
      }, item.name);

      this.currentItem = item;
      this.currentEndedHandler = onStreamEnded;
      this.currentLaneIndex = nextLaneIndex;
      this.heldLaneIndex = previousLaneIndex;
      this.updateObjectVisibility();
      await firstFrameReady?.promise;
      this.assertOperationCurrent(operationId, nextLaneIndex, 'play.firstFrame', item.name);
      this.freezeAndStopHeldLane();
      return { durationMs };
    } catch (error) {
      firstFrameReady?.cancel();
      throw error;
    }
  }

  async prepare(
    item: SeamlessContentItem,
    slot: SeamlessSlotPlan,
    slotElement: HTMLElement,
    preserveAspectRatio: boolean,
  ): Promise<AvplayPlaybackInfo> {
    const operationId = this.nextOperationId();
    this.displayContext = { slot, slotElement };
    if (this.preparedLane?.itemId === item.id) {
      this.applyDisplayRectToLane(this.preparedLane.laneIndex, slot, slotElement);
      return { durationMs: this.preparedLane.durationMs };
    }

    const laneIndex = this.currentLaneIndex === 0 ? 1 : 0;
    const sourceUrl = resolveAvplaySourceUrl(item.sourceUrl);
    this.clearPreparedLane();
    this.resetLaneForPlayback(laneIndex);
    this.configureLaneForItem(laneIndex, item, sourceUrl, slot, slotElement, preserveAspectRatio, null);
    await this.prepareLaneAsync(laneIndex, item.name);
    this.assertOperationCurrent(operationId, laneIndex, 'prepare.prepareAsync', item.name);
    const durationMs = this.readDurationMs(laneIndex, item.name);
    this.setLaneLooping(laneIndex, this.shouldLoopForDuration(item, durationMs));
    this.preparedLane = { laneIndex, itemId: item.id, durationMs };
    this.updateObjectVisibility();
    this.logger.info('avplay', `slot ${this.index} lane ${laneIndex + 1} prepared next: ${item.name}`);
    return { durationMs };
  }

  clearPrepared(): void {
    this.nextOperationId();
    this.logger.info('avplay-trace', `session ${this.index} clearPrepared ${this.traceContext()}`);
    this.clearPreparedLane();
  }

  hide(): void {
    this.nextOperationId();
    this.logger.info('avplay-trace', `session ${this.index} hide ${this.traceContext()}`);
    if (this.currentLaneIndex !== null) {
      this.hideLaneSurface(this.currentLaneIndex, 'hide');
    }
    if (this.heldLaneIndex !== null && this.heldLaneIndex !== this.currentLaneIndex) {
      this.hideLaneSurface(this.heldLaneIndex, 'hide-held');
    }
    this.currentItem = null;
    this.currentEndedHandler = null;
    this.currentLaneIndex = null;
    this.heldLaneIndex = null;
    this.preparedLane = null;
    this.updateObjectVisibility();
  }

  hideCurrentKeepPrepared(): Promise<void> {
    const laneToStop = this.currentLaneIndex;
    this.logger.info('avplay-trace', `session ${this.index} hideCurrentKeepPrepared lane=${laneToStop !== null ? laneToStop + 1 : '-'} ${this.traceContext()}`);
    this.currentItem = null;
    this.currentEndedHandler = null;
    this.currentLaneIndex = null;
    this.heldLaneIndex = null;
    this.updateObjectVisibility();
    if (laneToStop !== null && this.preparedLane?.laneIndex !== laneToStop) {
      this.stopLane(laneToStop);
      this.closeLane(laneToStop);
    }
    return Promise.resolve();
  }

  detachCurrentSurfaceForTransition(): boolean {
    const laneIndexes = [this.currentLaneIndex, this.heldLaneIndex]
      .filter((laneIndex, index, values): laneIndex is number => laneIndex !== null && values.indexOf(laneIndex) === index);
    if (laneIndexes.length === 0) {
      return false;
    }

    this.detachedLaneIndexes = Array.from(new Set([...this.detachedLaneIndexes, ...laneIndexes]));
    this.logger.info('avplay-trace', `session ${this.index} detach surface lanes=${laneIndexes.map((laneIndex) => laneIndex + 1).join(',')} ${this.traceContext()}`);
    this.currentItem = null;
    this.currentEndedHandler = null;
    this.currentLaneIndex = null;
    this.heldLaneIndex = null;
    this.updateObjectVisibility();
    return true;
  }

  stopDetachedSurface(keepPrepared: boolean): void {
    const laneIndexes = this.detachedLaneIndexes;
    this.detachedLaneIndexes = [];
    laneIndexes.forEach((laneIndex) => {
      if (keepPrepared && this.preparedLane?.laneIndex === laneIndex) {
        return;
      }

      this.stopLane(laneIndex);
      this.closeLane(laneIndex);
    });
  }

  applyDisplayRect(slot: SeamlessSlotPlan, slotElement: HTMLElement): void {
    this.displayContext = { slot, slotElement };
    this.lanes.forEach((_lane, laneIndex) => {
      this.applyDisplayRectToLane(laneIndex, slot, slotElement);
    });
    this.updateObjectVisibility();
  }

  applyDisplayMethod(preserveAspectRatio: boolean): void {
    const displayMethod = preserveAspectRatio ? DISPLAY_METHOD_CONTAIN : DISPLAY_METHOD_FILL;
    this.lanes.forEach((lane, laneIndex) => {
      try {
        if (this.displayContext) {
          this.applyDisplayRectToLane(laneIndex, this.displayContext.slot, this.displayContext.slotElement);
        }
        this.setLaneDisplayMethod(laneIndex, displayMethod);
      } catch (error) {
        this.logger.warn('avplay', `slot ${this.index} lane ${laneIndex + 1} display method 적용 실패: ${String(error)}`);
      }
    });
  }

  setLooping(shouldLoop: boolean): void {
    if (this.currentLaneIndex === null) {
      return;
    }

    this.callLaneSafe(this.currentLaneIndex, 'setLooping', () => {
      const lane = this.lanes[this.currentLaneIndex!];
      lane.player.setLooping?.(shouldLoop);
    }, String(shouldLoop));
  }

  pause(): void {
    if (this.currentLaneIndex === null) {
      return;
    }

    const lane = this.currentLane();
    if (lane && this.laneState(this.currentLaneIndex) === 'PLAYING') {
      this.callLane(this.currentLaneIndex, 'pause', () => {
        lane.player.pause();
      });
    }
  }

  resume(): void {
    if (this.currentLaneIndex === null) {
      return;
    }

    const lane = this.currentLane();
    if (lane && this.laneState(this.currentLaneIndex) === 'PAUSED') {
      this.callLane(this.currentLaneIndex, 'resume.play', () => {
        lane.player.play();
      });
    }
  }

  stop(): void {
    this.nextOperationId();
    this.logger.info('avplay-trace', `session ${this.index} stop all ${this.traceContext()}`);
    this.detachedLaneIndexes = [];
    this.lanes.forEach((_lane, laneIndex) => {
      this.stopLane(laneIndex);
      this.closeLane(laneIndex);
    });
    this.currentItem = null;
    this.currentEndedHandler = null;
    this.currentLaneIndex = null;
    this.heldLaneIndex = null;
    this.preparedLane = null;
    this.updateObjectVisibility();
  }

  state(): string {
    const lane = this.currentLane();
    if (!lane) {
      return 'IDLE';
    }

    return getPlayerState(lane.player);
  }

  private createLaneListener(laneIndex: number, firstFrameReady: FirstFrameReadyGate | null): AVPlayListener {
    return {
      onbufferingcomplete: () => {
        this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} buffering complete`);
      },
      oncurrentplaytime: (currentTime) => {
        firstFrameReady?.markReady(`currentplaytime=${currentTime}`);
      },
      onstreamcompleted: () => {
        this.logger.info('avplay-trace', `event onstreamcompleted slot ${this.index} lane ${laneIndex + 1} state=${this.laneState(laneIndex)} ${this.traceContext()}`);
        void this.handleStreamCompleted(laneIndex);
      },
      onerror: (error) => {
        const message = `slot ${this.index} lane ${laneIndex + 1} AVPlay 오류: ${formatAvplayError(error, '')}`;
        this.logger.error('avplay-trace', `event onerror slot ${this.index} lane ${laneIndex + 1} state=${this.laneState(laneIndex)} ${this.traceContext()} error=${formatAvplayError(error, '')}`);
        firstFrameReady?.fail(message);
        this.events.onError(message);
      },
      onerrormsg: (error, message) => {
        const errorMessage = `slot ${this.index} lane ${laneIndex + 1} AVPlay 오류: ${formatAvplayError(error, message)}`;
        this.logger.error('avplay-trace', `event onerrormsg slot ${this.index} lane ${laneIndex + 1} state=${this.laneState(laneIndex)} ${this.traceContext()} error=${formatAvplayError(error, message)}`);
        firstFrameReady?.fail(errorMessage);
        this.events.onError(errorMessage);
      },
    };
  }

  private async handleStreamCompleted(laneIndex: number): Promise<void> {
    if (laneIndex !== this.currentLaneIndex) {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} stale stream completed ignored ${this.traceContext()}`);
      return;
    }

    this.logger.info('avplay', `slot ${this.index} lane ${laneIndex + 1} stream completed: ${this.currentItem?.name ?? '-'}`);
    const shouldComplete = await this.currentEndedHandler?.();
    if (shouldComplete === false) {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} stream completed deferred ${this.traceContext()}`);
      return;
    }

    if (this.currentLaneIndex !== laneIndex) {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} stream completed 후 이미 전환됨 ${this.traceContext()}`);
      return;
    }

    this.freezeAndStopLane(laneIndex);
    this.heldLaneIndex = laneIndex;
    this.currentLaneIndex = null;
    this.currentItem = null;
    this.currentEndedHandler = null;
    this.updateObjectVisibility();
    this.events.onEnded();
  }

  private createFirstFrameReadyGate(laneIndex: number, itemName: string): FirstFrameReadyGate {
    let settled = false;
    let timerId: number | null = null;
    let resolvePromise: (() => void) | null = null;
    let rejectPromise: ((error: Error) => void) | null = null;

    const promise = new Promise<void>((resolve, reject) => {
      resolvePromise = resolve;
      rejectPromise = reject;
      timerId = window.setTimeout(() => {
        if (settled) {
          return;
        }
        settled = true;
        reject(new Error(`slot ${this.index} lane ${laneIndex + 1} 첫 프레임 준비 시간 초과: ${itemName}`));
      }, FIRST_FRAME_READY_TIMEOUT_MS);
    });

    const clear = () => {
      if (timerId !== null) {
        window.clearTimeout(timerId);
        timerId = null;
      }
    };

    return {
      promise,
      markReady: (reason) => {
        if (settled) {
          return;
        }
        settled = true;
        clear();
        this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} first frame ready: ${reason}`);
        resolvePromise?.();
      },
      fail: (message) => {
        if (settled) {
          return;
        }
        settled = true;
        clear();
        rejectPromise?.(new Error(message));
      },
      cancel: () => {
        if (settled) {
          return;
        }
        settled = true;
        clear();
        resolvePromise?.();
      },
    };
  }

  private currentLane(): AvplayLane | null {
    if (this.currentLaneIndex === null) {
      return null;
    }

    return this.lanes[this.currentLaneIndex];
  }

  private resetLaneForPlayback(laneIndex: number): void {
    this.logger.info('avplay-trace', `session ${this.index} resetLane lane ${laneIndex + 1} state=${this.laneState(laneIndex)} ${this.traceContext()}`);
    this.stopLane(laneIndex);
    this.closeLane(laneIndex);
    if (this.heldLaneIndex === laneIndex) {
      this.heldLaneIndex = null;
    }
    if (this.preparedLane?.laneIndex === laneIndex) {
      this.preparedLane = null;
    }
  }

  private clearPreparedLane(): void {
    if (!this.preparedLane) {
      return;
    }

    const laneIndex = this.preparedLane.laneIndex;
    this.preparedLane = null;
    this.logger.info('avplay-trace', `session ${this.index} clearPreparedLane lane ${laneIndex + 1} state=${this.laneState(laneIndex)} ${this.traceContext()}`);
    if (laneIndex !== this.currentLaneIndex && laneIndex !== this.heldLaneIndex) {
      this.stopLane(laneIndex);
      this.closeLane(laneIndex);
    }
  }

  private prepareLaneAsync(laneIndex: number, itemName: string): Promise<void> {
    const lane = this.lanes[laneIndex];
    return new Promise<void>((resolve, reject) => {
      let settled = false;
      const traceId = this.traceBegin(laneIndex, 'prepareAsync', itemName);
      const resolveOnce = () => {
        if (settled) {
          return;
        }
        settled = true;
        this.traceEnd(traceId, laneIndex, 'prepareAsync', itemName);
        this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} prepared: ${itemName}`);
        resolve();
      };
      const rejectOnce = (error: AVPlayErrorLike) => {
        if (settled) {
          return;
        }
        settled = true;
        this.traceFail(traceId, laneIndex, 'prepareAsync', error, itemName);
        reject(new Error(`slot ${this.index} lane ${laneIndex + 1} AVPlay prepareAsync 오류: ${formatAvplayError(error, '')}`));
      };

      try {
        lane.player.prepareAsync(resolveOnce, rejectOnce);
      } catch (error) {
        rejectOnce(error as AVPlayErrorLike);
      }
    });
  }

  private configureLaneForItem(
    laneIndex: number,
    item: SeamlessContentItem,
    sourceUrl: string,
    slot: SeamlessSlotPlan,
    slotElement: HTMLElement,
    preserveAspectRatio: boolean,
    firstFrameReady: FirstFrameReadyGate | null,
  ): void {
    const lane = this.lanes[laneIndex];
    this.callLane(laneIndex, 'open', () => {
      lane.player.open(sourceUrl);
    }, `${item.name} ${sourceUrl}`);
    this.callLane(laneIndex, 'setListener', () => {
      lane.player.setListener(this.createLaneListener(laneIndex, firstFrameReady));
    }, item.name);
    this.applyDisplayRectToLane(laneIndex, slot, slotElement);
    this.setLaneDisplayMethod(laneIndex, preserveAspectRatio ? DISPLAY_METHOD_CONTAIN : DISPLAY_METHOD_FILL);
    this.callLaneSafe(laneIndex, 'setTimeoutForBuffering', () => {
      lane.player.setTimeoutForBuffering?.(30);
    }, '30s');
  }

  private readDurationMs(laneIndex: number, itemName: string): number | null {
    const duration = this.lanes[laneIndex].player.getDuration?.();
    if (typeof duration !== 'number' || !Number.isFinite(duration) || duration <= 0) {
      this.logger.warn('avplay', `slot ${this.index} lane ${laneIndex + 1} duration 확인 실패: ${itemName}`);
      return null;
    }

    const durationMs = Math.round(duration);
    this.logger.info('avplay', `slot ${this.index} lane ${laneIndex + 1} duration ${durationMs}ms: ${itemName}`);
    return durationMs;
  }

  private shouldLoopForDuration(item: SeamlessContentItem, durationMs: number | null): boolean {
    if (durationMs === null) {
      return item.shouldLoop;
    }

    const displayMs = Math.max(1, item.durationSeconds) * 1000;
    return item.shouldLoop || displayMs > durationMs + VIDEO_DURATION_MATCH_TOLERANCE_MS;
  }

  private setLaneDisplayMethod(laneIndex: number, displayMethod: string): void {
    const lane = this.lanes[laneIndex];
    this.callLaneSafe(laneIndex, 'setDisplayMethod', () => {
      lane.player.setDisplayMethod?.(displayMethod);
    }, displayMethod);
  }

  private setLaneLooping(laneIndex: number, shouldLoop: boolean): void {
    const lane = this.lanes[laneIndex];
    this.callLaneSafe(laneIndex, 'setLooping', () => {
      lane.player.setLooping?.(shouldLoop);
    }, String(shouldLoop));
  }

  private setLaneVideoStillMode(laneIndex: number, mode: 'true' | 'false'): void {
    const lane = this.lanes[laneIndex];
    this.callLaneSafe(laneIndex, 'setVideoStillMode', () => {
      lane.player.setVideoStillMode?.(mode);
    }, mode);
  }

  private freezeAndStopLane(laneIndex: number): void {
    this.setLaneVideoStillMode(laneIndex, 'true');
    this.stopLane(laneIndex);
  }

  private freezeAndStopHeldLane(): void {
    if (this.heldLaneIndex === null || this.heldLaneIndex === this.currentLaneIndex) {
      return;
    }

    this.freezeAndStopLane(this.heldLaneIndex);
  }

  private stopLane(laneIndex: number): void {
    const lane = this.lanes[laneIndex];
    const state = this.laneState(laneIndex);
    if (!STOPPABLE_STATES.has(state)) {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} stop skipped state=${state} ${this.traceContext()}`);
      return;
    }

    this.callLaneSafe(laneIndex, 'stop', () => {
      lane.player.stop();
    });
  }

  private closeLane(laneIndex: number): void {
    const lane = this.lanes[laneIndex];
    const state = this.laneState(laneIndex);
    if (state === 'IDLE') {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} close skipped state=IDLE ${this.traceContext()}`);
      return;
    }

    this.callLaneSafe(laneIndex, 'close', () => {
      lane.player.close?.();
    });
  }

  private applyDisplayRectToLane(laneIndex: number, slot: SeamlessSlotPlan, slotElement: HTMLElement): void {
    const lane = this.lanes[laneIndex];
    const state = this.laneState(laneIndex);
    if (state !== 'IDLE' && state !== 'READY' && state !== 'PLAYING' && state !== 'PAUSED') {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} setDisplayRect skipped state=${state} ${this.traceContext()}`);
      return;
    }

    const rect = slotElement.getBoundingClientRect();
    const viewportWidth = Math.max(document.documentElement.clientWidth, 1);
    const viewportHeight = Math.max(document.documentElement.clientHeight, 1);
    const left = Math.round((rect.left / viewportWidth) * AVPLAY_BASE_WIDTH);
    const top = Math.round((rect.top / viewportHeight) * AVPLAY_BASE_HEIGHT);
    const width = Math.max(1, Math.round((rect.width / viewportWidth) * AVPLAY_BASE_WIDTH));
    const height = Math.max(1, Math.round((rect.height / viewportHeight) * AVPLAY_BASE_HEIGHT));

    lane.objectElement.style.left = `${rect.left}px`;
    lane.objectElement.style.top = `${rect.top}px`;
    lane.objectElement.style.width = `${rect.width}px`;
    lane.objectElement.style.height = `${rect.height}px`;
    this.callLane(laneIndex, 'setDisplayRect', () => {
      lane.player.setDisplayRect(left, top, width, height);
    }, `${left},${top},${width}x${height}`);
    this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} rect ${slot.left},${slot.top},${slot.width}x${slot.height}`);
  }

  private hideLaneSurface(laneIndex: number, reason: string): void {
    const lane = this.lanes[laneIndex];
    const state = this.laneState(laneIndex);
    lane.objectElement.style.visibility = 'hidden';
    lane.objectElement.style.left = '0px';
    lane.objectElement.style.top = '0px';
    lane.objectElement.style.width = '1px';
    lane.objectElement.style.height = '1px';
    if (state !== 'IDLE' && state !== 'READY' && state !== 'PLAYING' && state !== 'PAUSED') {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} hide surface skipped state=${state} ${this.traceContext()}`);
      return;
    }

    this.callLaneSafe(laneIndex, 'setDisplayRect.hide', () => {
      lane.player.setDisplayRect(0, 0, 1, 1);
    }, reason);
  }

  private callLane<T>(laneIndex: number, operation: string, callback: () => T, detail = ''): T {
    const traceId = this.traceBegin(laneIndex, operation, detail);
    try {
      const result = callback();
      this.traceEnd(traceId, laneIndex, operation, detail);
      return result;
    } catch (error) {
      this.traceFail(traceId, laneIndex, operation, error, detail);
      throw error;
    }
  }

  private callLaneSafe(laneIndex: number, operation: string, callback: () => void, detail = ''): void {
    const traceId = this.traceBegin(laneIndex, operation, detail);
    try {
      callback();
      this.traceEnd(traceId, laneIndex, operation, detail);
    } catch (error) {
      this.traceFail(traceId, laneIndex, operation, error, detail);
    }
  }

  private traceBegin(laneIndex: number, operation: string, detail = ''): number {
    this.traceSeq += 1;
    const traceId = this.traceSeq;
    this.logger.info(
      'avplay-trace',
      `#${traceId} > slot ${this.index} lane ${laneIndex + 1} ${operation} state=${this.laneState(laneIndex)} ${this.traceContext()}${this.formatTraceDetail(detail)}`,
    );
    return traceId;
  }

  private traceEnd(traceId: number, laneIndex: number, operation: string, detail = ''): void {
    this.logger.info(
      'avplay-trace',
      `#${traceId} < slot ${this.index} lane ${laneIndex + 1} ${operation} state=${this.laneState(laneIndex)} ${this.traceContext()}${this.formatTraceDetail(detail)}`,
    );
  }

  private traceFail(traceId: number, laneIndex: number, operation: string, error: unknown, detail = ''): void {
    this.logger.error(
      'avplay-trace',
      `#${traceId} ! slot ${this.index} lane ${laneIndex + 1} ${operation} state=${this.laneState(laneIndex)} ${this.traceContext()} error=${formatAvplayError(error as AVPlayErrorLike, String(error))}${this.formatTraceDetail(detail)}`,
    );
  }

  private laneState(laneIndex: number): string {
    try {
      return getPlayerState(this.lanes[laneIndex].player);
    } catch (error) {
      return `STATE_ERROR:${String(error)}`;
    }
  }

  private traceContext(): string {
    const prepared = this.preparedLane
      ? `${this.preparedLane.laneIndex + 1}:${this.preparedLane.itemId}`
      : '-';
    return `current=${this.currentLaneIndex !== null ? this.currentLaneIndex + 1 : '-'} held=${this.heldLaneIndex !== null ? this.heldLaneIndex + 1 : '-'} prepared=${prepared} item=${this.currentItem?.name ?? '-'}`;
  }

  private formatTraceDetail(detail: string): string {
    return detail ? ` detail=${detail}` : '';
  }

  private nextOperationId(): number {
    this.operationSeq += 1;
    return this.operationSeq;
  }

  private assertOperationCurrent(operationId: number, laneIndex: number, operation: string, itemName: string): void {
    if (operationId === this.operationSeq) {
      return;
    }

    this.logger.info(
      'avplay-trace',
      `slot ${this.index} lane ${laneIndex + 1} stale ${operation} ignored ${this.traceContext()}${this.formatTraceDetail(itemName)}`,
    );
    throw new Error(`slot ${this.index} lane ${laneIndex + 1} AVPlay 작업이 폐기되었습니다: ${itemName}`);
  }

  private updateObjectVisibility(): void {
    const slotHidden = this.displayContext?.slotElement.style.visibility === 'hidden';
    const slotZIndex = this.displayContext?.slot.zIndex ?? 0;
    this.lanes.forEach((lane, laneIndex) => {
      const shouldShow = !slotHidden && (laneIndex === this.currentLaneIndex || laneIndex === this.heldLaneIndex);
      if (laneIndex === this.currentLaneIndex) {
        lane.objectElement.style.zIndex = String(slotZIndex + 2);
      } else if (laneIndex === this.heldLaneIndex) {
        lane.objectElement.style.zIndex = String(slotZIndex + 1);
      } else {
        lane.objectElement.style.zIndex = String(slotZIndex);
      }
      lane.objectElement.style.visibility = shouldShow ? 'visible' : 'hidden';
    });
  }
}

export class AvplaySessionPool {
  private readonly sessions: AvplaySession[] = [];
  private readonly slotLeases = new Map<number, AvplaySession>();
  private readonly store: AVPlayStoreManager;

  constructor(
    private readonly host: HTMLElement,
    private readonly logger: RingLogger,
    private readonly createEvents: (index: number) => VideoSessionEvents,
  ) {
    const store = window.webapis?.avplaystore;
    if (!store) {
      throw new Error('AVPlayStore API를 찾지 못했습니다. avplay-seamless-still-mode 기준 재생은 삼성 Tizen Signage 실장비의 webapis.avplaystore가 필요합니다.');
    }

    this.store = store;
  }

  acquire(slotIndex: number): AvplaySession {
    const leased = this.slotLeases.get(slotIndex);
    if (leased) {
      return leased;
    }

    const leasedSessions = new Set(this.slotLeases.values());
    const reusableSession = this.sessions.find((session) => !leasedSessions.has(session));
    if (reusableSession) {
      this.slotLeases.set(slotIndex, reusableSession);
      return reusableSession;
    }

    const createdStorePlayers = this.sessions.length * PLAYERS_PER_SEAMLESS_SESSION;
    if (createdStorePlayers + PLAYERS_PER_SEAMLESS_SESSION > MAX_AVPLAYSTORE_PLAYERS) {
      throw new Error(
        `Tizen AVPlayStore 세션 한도를 초과했습니다: slot ${slotIndex + 1}, seamless players ${createdStorePlayers}/${MAX_AVPLAYSTORE_PLAYERS}`,
      );
    }

    const sessionIndex = this.sessions.length;
    const session = new AvplaySession(
      sessionIndex,
      [
        this.createLaneObject(),
        this.createLaneObject(),
      ],
      this.host,
      this.logger,
      this.createEvents(sessionIndex),
    );
    this.sessions.push(session);
    this.slotLeases.set(slotIndex, session);
    return session;
  }

  resetLeases(): void {
    this.slotLeases.clear();
  }

  release(session: AvplaySession): void {
    Array.from(this.slotLeases.entries())
      .filter(([, leasedSession]) => leasedSession === session)
      .forEach(([slotIndex]) => {
        this.slotLeases.delete(slotIndex);
      });
  }

  commitTransitionLeases(slotIndexOffset: number): void {
    const committed = new Map<number, AvplaySession>();
    this.slotLeases.forEach((session, slotIndex) => {
      if (slotIndex >= slotIndexOffset) {
        committed.set(slotIndex - slotIndexOffset, session);
      }
    });
    this.slotLeases.clear();
    committed.forEach((session, slotIndex) => {
      this.slotLeases.set(slotIndex, session);
    });
  }

  releaseTransitionLeases(slotIndexOffset: number): void {
    Array.from(this.slotLeases.keys())
      .filter((slotIndex) => slotIndex >= slotIndexOffset)
      .forEach((slotIndex) => {
        this.slotLeases.delete(slotIndex);
      });
  }

  getLeased(slotIndex: number): AvplaySession | null {
    const session = this.slotLeases.get(slotIndex);
    if (!session) {
      return null;
    }

    return session;
  }

  pauseAll(): void {
    this.sessions.forEach((session) => session.pause());
  }

  resumeAll(): void {
    this.sessions.forEach((session) => session.resume());
  }

  stopAll(): void {
    this.sessions.forEach((session) => session.stop());
  }

  private createLaneObject(): AvplayLane {
    const player = this.store.getPlayer();
    if (!player) {
      throw new Error('AVPlayStore 플레이어를 확보하지 못했습니다.');
    }

    return {
      player,
      objectElement: document.createElement('object'),
    };
  }
}
