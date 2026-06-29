import type { RingLogger } from '../core/logger';
import type { SeamlessContentItem, SeamlessSlotPlan } from '../domain/page-plan';
import { resolveAvplaySourceUrl } from './source-resolver';

const DISPLAY_METHOD_FILL = 'PLAYER_DISPLAY_MODE_FULL_SCREEN';
const DISPLAY_METHOD_CONTAIN = 'PLAYER_DISPLAY_MODE_LETTER_BOX';
const AVPLAY_BASE_WIDTH = 1920;
const AVPLAY_BASE_HEIGHT = 1080;
const FIRST_FRAME_READY_TIMEOUT_MS = 5000;
const FIRST_FRAME_PRESENTABLE_PLAYTIME_MS = 33;
const FIRST_FRAME_PRESENTABLE_PLAYTIME_DELTA_MS = 1;
const STOPPABLE_STATES = new Set(['READY', 'PLAYING', 'PAUSED']);
const VIDEO_DURATION_MATCH_TOLERANCE_MS = 250;
const AVPLAY_LAYER_BELOW_SLOT_OFFSET = -1;
const AVPLAY_LAYER_HELD_OFFSET = 1;
const AVPLAY_LAYER_CURRENT_OFFSET = 2;

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
  readonly itemKey: string;
  readonly durationMs: number | null;
}

interface PrepareInFlight {
  readonly itemKey: string;
  readonly laneIndex: number;
  readonly promise: Promise<AvplayPlaybackInfo>;
}

export interface AvplayPlayOptions {
  readonly waitForFirstFrame?: boolean;
}

export interface AvplayPlaybackInfo {
  readonly durationMs: number | null;
}

interface FirstFrameReadyGate {
  readonly promise: Promise<void>;
  readonly start: () => void;
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
  private prepareInFlight: PrepareInFlight | null = null;
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
    const itemKey = this.playbackKey(item);
    const preparedLaneIndex = this.preparedLane?.itemKey === itemKey ? this.preparedLane.laneIndex : null;
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
      let durationMs = this.preparedLane?.itemKey === itemKey ? this.preparedLane.durationMs : null;
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
      firstFrameReady?.start();

      this.currentItem = item;
      this.currentEndedHandler = onStreamEnded;
      this.currentLaneIndex = nextLaneIndex;
      this.heldLaneIndex = previousLaneIndex;
      if (firstFrameReady) {
        await firstFrameReady.promise;
        this.assertCurrentPlaybackFirstFrame(operationId, nextLaneIndex, item);
      }
      this.updateObjectVisibility();
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
    this.displayContext = { slot, slotElement };
    const itemKey = this.playbackKey(item);
    if (this.preparedLane?.itemKey === itemKey) {
      this.applyDisplayRectToLane(this.preparedLane.laneIndex, slot, slotElement);
      return { durationMs: this.preparedLane.durationMs };
    }

    const inFlightResult = await this.waitForPrepareLaneAvailable(itemKey, slot, slotElement);
    if (inFlightResult) {
      return inFlightResult;
    }

    const operationId = this.nextOperationId();
    const laneIndex = this.currentLaneIndex === 0 ? 1 : 0;
    let resolvePrepare: (value: AvplayPlaybackInfo) => void = () => undefined;
    let rejectPrepare: (reason: unknown) => void = () => undefined;
    const preparePromise = new Promise<AvplayPlaybackInfo>((resolve, reject) => {
      resolvePrepare = resolve;
      rejectPrepare = reject;
    });
    this.prepareInFlight = { itemKey, laneIndex, promise: preparePromise };
    void (async (): Promise<void> => {
      const sourceUrl = resolveAvplaySourceUrl(item.sourceUrl);
      this.clearPreparedLane();
      this.resetLaneForPlayback(laneIndex);
      this.configureLaneForItem(laneIndex, item, sourceUrl, slot, slotElement, preserveAspectRatio, null);
      await this.prepareLaneAsync(laneIndex, item.name);
      this.assertOperationCurrent(operationId, laneIndex, 'prepare.prepareAsync', item.name);
      const durationMs = this.readDurationMs(laneIndex, item.name);
      this.setLaneLooping(laneIndex, this.shouldLoopForDuration(item, durationMs));
      this.preparedLane = { laneIndex, itemKey, durationMs };
      this.updateObjectVisibility();
      this.logger.info('avplay', `slot ${this.index} lane ${laneIndex + 1} prepared next: ${item.name}`);
      resolvePrepare({ durationMs });
    })().catch((error) => {
      rejectPrepare(error);
    });
    try {
      return await preparePromise;
    } finally {
      if (this.prepareInFlight?.promise === preparePromise) {
        this.prepareInFlight = null;
      }
    }
  }

  clearPrepared(): void {
    if (this.prepareInFlight) {
      this.logger.info(
        'avplay-trace',
        `session ${this.index} clearPrepared deferred while prepareAsync in-flight lane=${this.prepareInFlight.laneIndex + 1} ${this.traceContext()}`,
      );
      return;
    }

    this.nextOperationId();
    this.logger.info('avplay-trace', `session ${this.index} clearPrepared ${this.traceContext()}`);
    this.clearPreparedLane();
  }

  hide(): void {
    this.nextOperationId();
    this.logger.info('avplay-trace', `session ${this.index} hide ${this.traceContext()}`);
    const lanesToHide: Array<{ readonly laneIndex: number; readonly reason: string }> = [];
    if (this.heldLaneIndex !== null && this.heldLaneIndex !== this.currentLaneIndex) {
      lanesToHide.push({ laneIndex: this.heldLaneIndex, reason: 'hide-held' });
    }
    if (this.currentLaneIndex !== null) {
      lanesToHide.push({ laneIndex: this.currentLaneIndex, reason: 'hide' });
    }
    this.hideLaneSurfaces(lanesToHide);
    this.currentItem = null;
    this.currentEndedHandler = null;
    this.currentLaneIndex = null;
    this.heldLaneIndex = null;
    this.preparedLane = null;
    this.prepareInFlight = null;
    this.updateObjectVisibility();
  }

  hideCurrentKeepPrepared(options: { readonly deferStopUntilNextFrame?: boolean } = {}): Promise<void> {
    const laneToStop = this.currentLaneIndex;
    this.logger.info('avplay-trace', `session ${this.index} hideCurrentKeepPrepared lane=${laneToStop !== null ? laneToStop + 1 : '-'} ${this.traceContext()}`);
    if (laneToStop !== null) {
      this.hideLaneSurface(laneToStop, 'hide-current-keep-prepared');
    }
    this.currentItem = null;
    this.currentEndedHandler = null;
    this.currentLaneIndex = null;
    this.heldLaneIndex = null;
    this.updateObjectVisibility();
    if (laneToStop !== null && this.preparedLane?.laneIndex !== laneToStop) {
      const stopCurrentLane = () => {
        this.stopLane(laneToStop);
        this.closeLane(laneToStop);
      };
      if (options.deferStopUntilNextFrame === true) {
        return this.afterNextFrame(stopCurrentLane);
      }
      stopCurrentLane();
    }
    return Promise.resolve();
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
    this.lanes.forEach((_lane, laneIndex) => {
      this.stopLane(laneIndex);
      this.closeLane(laneIndex);
    });
    this.currentItem = null;
    this.currentEndedHandler = null;
    this.currentLaneIndex = null;
    this.heldLaneIndex = null;
    this.preparedLane = null;
    this.prepareInFlight = null;
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
    let firstFrameObservedPlaytime: number | null = null;
    return {
      onbufferingcomplete: () => {
        this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} buffering complete`);
      },
      oncurrentplaytime: (currentTime) => {
        const nextObservedPlaytime = this.markFirstPresentableFrameReady(
          laneIndex,
          firstFrameReady,
          currentTime,
          firstFrameObservedPlaytime,
        );
        firstFrameObservedPlaytime = nextObservedPlaytime;
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
    let started = false;
    let timerId: number | null = null;
    let resolvePromise: (() => void) | null = null;
    let rejectPromise: ((error: Error) => void) | null = null;

    const promise = new Promise<void>((resolve, reject) => {
      resolvePromise = resolve;
      rejectPromise = reject;
    });

    const startTimer = () => {
      if (settled || started) {
        return;
      }

      started = true;
      timerId = window.setTimeout(() => {
        if (settled) {
          return;
        }
        settled = true;
        rejectPromise?.(new Error(`slot ${this.index} lane ${laneIndex + 1} 첫 프레임 준비 시간 초과: ${itemName}`));
      }, FIRST_FRAME_READY_TIMEOUT_MS);
    };

    const clear = () => {
      if (timerId !== null) {
        window.clearTimeout(timerId);
        timerId = null;
      }
    };

    return {
      promise,
      start: startTimer,
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

  private markFirstPresentableFrameReady(
    laneIndex: number,
    firstFrameReady: FirstFrameReadyGate | null,
    currentTime: number,
    observedPlaytime: number | null,
  ): number | null {
    if (!firstFrameReady) {
      return observedPlaytime;
    }

    if (!Number.isFinite(currentTime)) {
      this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} first frame pending: currentplaytime=${currentTime}`);
      return observedPlaytime;
    }

    if (currentTime >= FIRST_FRAME_PRESENTABLE_PLAYTIME_MS) {
      firstFrameReady.markReady(`currentplaytime=${currentTime}`);
      return currentTime;
    }

    if (observedPlaytime === null) {
      this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} first frame pending: currentplaytime=${currentTime}`);
      return currentTime;
    }

    const playtimeDelta = currentTime - observedPlaytime;
    if (playtimeDelta >= FIRST_FRAME_PRESENTABLE_PLAYTIME_DELTA_MS) {
      firstFrameReady.markReady(`currentplaytime=${currentTime} delta=${playtimeDelta}`);
      return currentTime;
    }

    this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} first frame pending: currentplaytime=${currentTime} delta=${playtimeDelta}`);
    return currentTime;
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

  private playbackKey(item: SeamlessContentItem): string {
    return `${item.contentType}:${resolveAvplaySourceUrl(item.sourceUrl)}`;
  }

  private async waitForPrepareLaneAvailable(
    itemKey: string,
    slot: SeamlessSlotPlan,
    slotElement: HTMLElement,
  ): Promise<AvplayPlaybackInfo | null> {
    while (this.prepareInFlight) {
      const inFlight = this.prepareInFlight;
      if (inFlight.itemKey === itemKey) {
        const result = await inFlight.promise;
        if (this.preparedLane?.itemKey === itemKey) {
          this.applyDisplayRectToLane(this.preparedLane.laneIndex, slot, slotElement);
        }
        return result;
      }

      this.logger.info(
        'avplay-trace',
        `session ${this.index} wait prepareAsync before next prepare lane=${inFlight.laneIndex + 1} requested=${itemKey} active=${inFlight.itemKey} ${this.traceContext()}`,
      );
      try {
        await inFlight.promise;
      } catch (error) {
        this.logger.warn('avplay', `slot ${this.index} 이전 prepareAsync 완료 대기 중 오류: ${String(error)}`);
      }

      if (this.preparedLane?.itemKey === itemKey) {
        this.applyDisplayRectToLane(this.preparedLane.laneIndex, slot, slotElement);
        return { durationMs: this.preparedLane.durationMs };
      }
    }

    return null;
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

  private afterNextFrame(action: () => void): Promise<void> {
    return new Promise((resolve) => {
      window.requestAnimationFrame(() => {
        window.setTimeout(() => {
          action();
          resolve();
        }, 0);
      });
    });
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
    this.hideLaneSurfaces([{ laneIndex, reason }]);
  }

  private hideLaneSurfaces(lanesToHide: ReadonlyArray<{ readonly laneIndex: number; readonly reason: string }>): void {
    if (lanesToHide.length === 0) {
      return;
    }

    lanesToHide.forEach(({ laneIndex }) => {
      const lane = this.lanes[laneIndex];
      lane.objectElement.style.visibility = 'hidden';
      lane.objectElement.setAttribute('aria-hidden', 'true');
    });
    lanesToHide.forEach(({ laneIndex, reason }) => {
      const lane = this.lanes[laneIndex];
      lane.objectElement.style.zIndex = String(this.avplayLayerBelowSlot());
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} surface hidden lowered ${this.traceContext()}${this.formatTraceDetail(reason)}`);
    });
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
      ? `${this.preparedLane.laneIndex + 1}:${this.preparedLane.itemKey}`
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

  private assertCurrentPlaybackFirstFrame(operationId: number, laneIndex: number, item: SeamlessContentItem): void {
    if (operationId === this.operationSeq) {
      return;
    }

    if (this.currentLaneIndex === laneIndex && this.currentItem?.id === item.id) {
      this.logger.info(
        'avplay-trace',
        `slot ${this.index} lane ${laneIndex + 1} play.firstFrame accepted after concurrent prepare ${this.traceContext()}${this.formatTraceDetail(item.name)}`,
      );
      return;
    }

    this.logger.info(
      'avplay-trace',
      `slot ${this.index} lane ${laneIndex + 1} stale play.firstFrame ignored ${this.traceContext()}${this.formatTraceDetail(item.name)}`,
    );
    throw new Error(`slot ${this.index} lane ${laneIndex + 1} AVPlay 작업이 폐기되었습니다: ${item.name}`);
  }

  private updateObjectVisibility(): void {
    const slotHidden = this.displayContext?.slotElement.style.visibility === 'hidden';
    const slotZIndex = this.displayContext?.slot.zIndex ?? 0;
    this.lanes.forEach((lane, laneIndex) => {
      const shouldShow = !slotHidden && (laneIndex === this.currentLaneIndex || laneIndex === this.heldLaneIndex);
      if (laneIndex === this.currentLaneIndex) {
        lane.objectElement.style.zIndex = String(slotZIndex + AVPLAY_LAYER_CURRENT_OFFSET);
      } else if (laneIndex === this.heldLaneIndex) {
        lane.objectElement.style.zIndex = String(slotZIndex + AVPLAY_LAYER_HELD_OFFSET);
      } else {
        lane.objectElement.style.zIndex = String(slotZIndex + AVPLAY_LAYER_BELOW_SLOT_OFFSET);
      }
      lane.objectElement.style.visibility = shouldShow ? 'visible' : 'hidden';
      lane.objectElement.setAttribute('aria-hidden', shouldShow ? 'false' : 'true');
    });
  }

  private avplayLayerBelowSlot(): number {
    return (this.displayContext?.slot.zIndex ?? 0) + AVPLAY_LAYER_BELOW_SLOT_OFFSET;
  }
}

export function createAvplaySessionPair(
  index: number,
  host: HTMLElement,
  logger: RingLogger,
  events: VideoSessionEvents,
): AvplaySession {
  const store = window.webapis?.avplaystore;
  if (!store) {
    throw new Error('AVPlayStore API를 찾지 못했습니다. avplay-seamless-still-mode 기준 재생은 삼성 Tizen Signage 실장비의 webapis.avplaystore가 필요합니다.');
  }

  const createLaneObject = (): AvplayLane => {
    const player = store.getPlayer();
    if (!player) {
      throw new Error('AVPlayStore 플레이어를 확보하지 못했습니다.');
    }

    return {
      player,
      objectElement: document.createElement('object'),
    };
  };

  return new AvplaySession(
    index,
    [
      createLaneObject(),
      createLaneObject(),
    ],
    host,
    logger,
    events,
  );
}
