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
      this.holdCurrentFrameForTransition();
      let durationMs = this.preparedLane?.itemId === item.id ? this.preparedLane.durationMs : null;
      if (preparedLaneIndex === null) {
        this.clearPreparedLane();
        this.resetLaneForPlayback(nextLaneIndex);
        this.configureLaneForItem(nextLaneIndex, item, sourceUrl, slot, slotElement, preserveAspectRatio, firstFrameReady);
        await this.prepareLaneAsync(nextLaneIndex, item.name);
        durationMs = this.readDurationMs(nextLaneIndex, item.name);
      } else {
        lane.player.setListener(this.createLaneListener(nextLaneIndex, firstFrameReady));
        this.applyDisplayRectToLane(nextLaneIndex, slot, slotElement);
        lane.player.setDisplayMethod?.(preserveAspectRatio ? DISPLAY_METHOD_CONTAIN : DISPLAY_METHOD_FILL);
      }
      lane.player.setLooping?.(this.shouldLoopForDuration(item, durationMs));
      this.preparedLane = null;
      lane.player.setVideoStillMode?.('false');
      lane.player.play();

      this.currentItem = item;
      this.currentEndedHandler = onStreamEnded;
      this.currentLaneIndex = nextLaneIndex;
      this.heldLaneIndex = previousLaneIndex;
      this.updateObjectVisibility();
      await firstFrameReady?.promise;
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
    const durationMs = this.readDurationMs(laneIndex, item.name);
    this.lanes[laneIndex].player.setLooping?.(this.shouldLoopForDuration(item, durationMs));
    this.preparedLane = { laneIndex, itemId: item.id, durationMs };
    this.updateObjectVisibility();
    this.logger.info('avplay', `slot ${this.index} lane ${laneIndex + 1} prepared next: ${item.name}`);
    return { durationMs };
  }

  clearPrepared(): void {
    this.clearPreparedLane();
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
        lane.player.setDisplayMethod?.(displayMethod);
      } catch (error) {
        this.logger.warn('avplay', `slot ${this.index} lane ${laneIndex + 1} display method 적용 실패: ${String(error)}`);
      }
    });
  }

  setLooping(shouldLoop: boolean): void {
    const lane = this.currentLane();
    if (!lane) {
      return;
    }

    try {
      lane.player.setLooping?.(shouldLoop);
    } catch (error) {
      this.logger.warn('avplay', `slot ${this.index} loop 상태 적용 실패: ${String(error)}`);
    }
  }

  pause(): void {
    const lane = this.currentLane();
    if (lane && getPlayerState(lane.player) === 'PLAYING') {
      lane.player.pause();
    }
  }

  resume(): void {
    const lane = this.currentLane();
    if (lane && getPlayerState(lane.player) === 'PAUSED') {
      lane.player.play();
    }
  }

  stop(): void {
    this.lanes.forEach((_lane, laneIndex) => {
      this.stopLane(laneIndex);
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
        void this.handleStreamCompleted(laneIndex);
      },
      onerror: (error) => {
        const message = `slot ${this.index} lane ${laneIndex + 1} AVPlay 오류: ${formatAvplayError(error, '')}`;
        firstFrameReady?.fail(message);
        this.events.onError(message);
      },
      onerrormsg: (error, message) => {
        const errorMessage = `slot ${this.index} lane ${laneIndex + 1} AVPlay 오류: ${formatAvplayError(error, message)}`;
        firstFrameReady?.fail(errorMessage);
        this.events.onError(errorMessage);
      },
    };
  }

  private async handleStreamCompleted(laneIndex: number): Promise<void> {
    this.logger.info('avplay', `slot ${this.index} lane ${laneIndex + 1} stream completed: ${this.currentItem?.name ?? '-'}`);
    const shouldComplete = await this.currentEndedHandler?.();
    if (shouldComplete === false) {
      return;
    }

    this.freezeAndStopLane(laneIndex);
    this.heldLaneIndex = laneIndex;
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
    this.stopLane(laneIndex);
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
    if (laneIndex !== this.currentLaneIndex && laneIndex !== this.heldLaneIndex) {
      this.stopLane(laneIndex);
    }
  }

  private prepareLaneAsync(laneIndex: number, itemName: string): Promise<void> {
    const lane = this.lanes[laneIndex];
    return new Promise<void>((resolve, reject) => {
      let settled = false;
      const resolveOnce = () => {
        if (settled) {
          return;
        }
        settled = true;
        this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} prepared: ${itemName}`);
        resolve();
      };
      const rejectOnce = (error: AVPlayErrorLike) => {
        if (settled) {
          return;
        }
        settled = true;
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
    lane.player.open(sourceUrl);
    lane.player.setListener(this.createLaneListener(laneIndex, firstFrameReady));
    this.applyDisplayRectToLane(laneIndex, slot, slotElement);
    lane.player.setDisplayMethod?.(preserveAspectRatio ? DISPLAY_METHOD_CONTAIN : DISPLAY_METHOD_FILL);
    lane.player.setTimeoutForBuffering?.(30);
    lane.player.setLooping?.(item.shouldLoop);
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

  private holdCurrentFrameForTransition(): void {
    if (this.currentLaneIndex === null) {
      return;
    }

    const lane = this.lanes[this.currentLaneIndex];
    try {
      lane.player.setVideoStillMode?.('true');
    } catch (error) {
      this.logger.warn('avplay', `slot ${this.index} lane ${this.currentLaneIndex + 1} transition still mode 실패: ${String(error)}`);
    }
  }

  private freezeAndStopLane(laneIndex: number): void {
    const lane = this.lanes[laneIndex];
    try {
      lane.player.setVideoStillMode?.('true');
    } catch (error) {
      this.logger.warn('avplay', `slot ${this.index} lane ${laneIndex + 1} still mode 실패: ${String(error)}`);
    }
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
    const state = getPlayerState(lane.player);
    try {
      if (STOPPABLE_STATES.has(state)) {
        lane.player.stop();
      }
    } catch (error) {
      this.logger.warn('avplay', `slot ${this.index} lane ${laneIndex + 1} stop 실패: ${String(error)}`);
    }
  }

  private applyDisplayRectToLane(laneIndex: number, slot: SeamlessSlotPlan, slotElement: HTMLElement): void {
    const lane = this.lanes[laneIndex];
    const state = getPlayerState(lane.player);
    if (state !== 'IDLE' && state !== 'READY' && state !== 'PLAYING' && state !== 'PAUSED') {
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
    lane.player.setDisplayRect(left, top, width, height);
    this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} rect ${slot.left},${slot.top},${slot.width}x${slot.height}`);
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
