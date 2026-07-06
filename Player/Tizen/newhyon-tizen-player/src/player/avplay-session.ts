import type { RingLogger } from '../core/logger';
import type { SeamlessContentItem, SeamlessSlotPlan } from '../domain/page-plan';
import { resolveAvplaySourceUrl } from './source-resolver';

const DISPLAY_METHOD_FILL = 'PLAYER_DISPLAY_MODE_FULL_SCREEN';
const DISPLAY_METHOD_CONTAIN = 'PLAYER_DISPLAY_MODE_LETTER_BOX';
const STOPPABLE_STATES = new Set(['READY', 'PLAYING', 'PAUSED']);
const DISPLAY_METHOD_STATES = new Set(['IDLE', 'READY', 'PLAYING', 'PAUSED']);
const AVPLAY_LAYER_BELOW_SLOT_OFFSET = -1;
const AVPLAY_LAYER_CURRENT_OFFSET = 2;

export interface VideoSessionEvents {
  readonly onEnded: () => void;
  readonly onError: (message: string) => void;
}

type StreamEndedHandler = () => boolean | Promise<boolean> | void | Promise<void>;

interface AvplayPairSession {
  readonly player: AVPlayApi;
  readonly objectElement: HTMLObjectElement;
}

interface AvplayLaneRuntimeState {
  itemName: string | null;
  callbackCurrentTimeMs: number | null;
  callbackCurrentTimeAtMs: number | null;
  buffering: boolean;
  lastPlayAt: string | null;
  lastPrepareCompletedAt: string | null;
  lastBufferingStartAt: string | null;
  lastBufferingCompleteAt: string | null;
  lastStreamCompletedAt: string | null;
  lastError: string | null;
}

interface DisplayContext {
  readonly slot: SeamlessSlotPlan;
  readonly slotElement: HTMLElement;
}

export type AvplayPreparedRole = 'next-content' | 'next-transition-content';

interface PreparedLaneMetadata {
  readonly role: AvplayPreparedRole;
  readonly laneIndex: number;
  readonly itemId: string;
  readonly itemName: string;
}

export interface AvplayPlayOptions {
  readonly waitForFirstFrame?: boolean;
}

export interface AvplayPlaybackInfo {
  readonly durationMs: number | null;
}

function formatAvplayError(error: AVPlayErrorLike | undefined, message: string): string {
  return String(message || error?.message || error?.code || error?.name || 'unknown');
}

function getPlayerState(player: AVPlayApi): string {
  return (player.getState?.() ?? 'UNKNOWN').toUpperCase();
}

export class AvplaySessionPair {
  private currentItem: SeamlessContentItem | null = null;
  private currentEndedHandler: StreamEndedHandler | null = null;
  private currentLaneIndex: number | null = null;
  private heldLaneIndex: number | null = null;
  private lastPlaybackLaneIndex: number | null = null;
  private readonly preparedLanes = new Map<AvplayPreparedRole, PreparedLaneMetadata>();
  private displayContext: DisplayContext | null = null;
  private traceSeq = 0;
  private operationSeq = 0;
  private readonly laneRuntimeStates: AvplayLaneRuntimeState[] = [
    this.createLaneRuntimeState(),
    this.createLaneRuntimeState(),
  ];

  constructor(
    readonly index: number,
    private readonly lanes: readonly AvplayPairSession[],
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
    const preparedLane = this.resolvePreparedLane(item);
    const nextLaneIndex = preparedLane !== null
      ? preparedLane.laneIndex
      : this.currentLaneIndex !== null
      ? this.currentLaneIndex === 0 ? 1 : 0
      : this.lastPlaybackLaneIndex === 0 ? 1 : 0;
    const lane = this.lanes[nextLaneIndex];
    void options;
    try {
      const usePreparedLane = preparedLane?.laneIndex === nextLaneIndex;
      const laneToStopBeforeOpen = usePreparedLane
        ? null
        : this.currentLaneIndex;
      const laneToStopAfterPlay = usePreparedLane ? this.currentLaneIndex : null;
      if (laneToStopBeforeOpen !== null && laneToStopBeforeOpen !== nextLaneIndex) {
        this.stopLane(laneToStopBeforeOpen);
        if (laneToStopBeforeOpen === this.currentLaneIndex) {
          this.currentLaneIndex = null;
        }
      }
      if (usePreparedLane) {
        this.logger.info('avplay-trace', `slot ${this.index} lane ${nextLaneIndex + 1} use prepared lane role=${preparedLane?.role ?? '-'} ${this.traceContext()}${this.formatTraceDetail(item.name)}`);
      } else {
        const sourceUrl = resolveAvplaySourceUrl(item.sourceUrl);
        this.logger.info('avplay', `slot ${this.index} lane ${nextLaneIndex + 1} open: ${item.name}`);
        this.resetLaneForPlayback(nextLaneIndex);
        this.configureLaneForItem(nextLaneIndex, item, sourceUrl);
        this.prepareLane(nextLaneIndex, item.name);
        this.assertOperationCurrent(operationId, nextLaneIndex, 'play.prepare', item.name);
      }
      this.setLaneDisplayMethod(nextLaneIndex, preserveAspectRatio ? DISPLAY_METHOD_CONTAIN : DISPLAY_METHOD_FILL);
      this.applyDisplayRectToLane(nextLaneIndex, slot, slotElement);
      this.assertOperationCurrent(operationId, nextLaneIndex, 'play.beforePlay', item.name);
      this.callLane(nextLaneIndex, 'play', () => {
        lane.player.play();
      }, item.name);
      this.laneRuntimeStates[nextLaneIndex].lastPlayAt = new Date().toISOString();

      this.currentItem = item;
      this.currentEndedHandler = onStreamEnded;
      this.currentLaneIndex = nextLaneIndex;
      this.lastPlaybackLaneIndex = nextLaneIndex;
      this.clearPreparedMetadataForLane(nextLaneIndex);
      this.heldLaneIndex = null;
      if (laneToStopAfterPlay !== null && laneToStopAfterPlay !== nextLaneIndex) {
        this.stopLane(laneToStopAfterPlay);
      }
      if (preparedLane?.role === 'next-transition-content') {
        this.clearPreparedRole('next-content');
      }
      this.updateObjectVisibility();
      return { durationMs: null };
    } catch (error) {
      this.resetFailedLane(nextLaneIndex, `play ${item.name}`, error);
      throw error;
    }
  }

  clearPrepared(): void {
    this.nextOperationId();
    this.logger.info('avplay-trace', `session ${this.index} clearPrepared ${this.traceContext()}`);
    this.clearAllPreparedRoles();
  }

  hide(): void {
    this.nextOperationId();
    this.logger.info('avplay-trace', `session ${this.index} hide ${this.traceContext()}`);
    const lanesToHide: Array<{ readonly laneIndex: number; readonly reason: string }> = [];
    if (this.currentLaneIndex !== null) {
      lanesToHide.push({ laneIndex: this.currentLaneIndex, reason: 'hide' });
    }
    this.hideLaneSurfaces(lanesToHide);
    this.currentItem = null;
    this.currentEndedHandler = null;
    this.currentLaneIndex = null;
    this.heldLaneIndex = null;
    this.clearAllPreparedRoles();
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
    this.clearAllPreparedRoles();
    this.updateObjectVisibility();
    if (laneToStop !== null) {
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
    this.visibleLaneIndexes().forEach((laneIndex) => {
      this.applyDisplayRectToLane(laneIndex, slot, slotElement);
    });
    this.updateObjectVisibility();
  }

  applyDisplayMethod(preserveAspectRatio: boolean): void {
    const displayMethod = preserveAspectRatio ? DISPLAY_METHOD_CONTAIN : DISPLAY_METHOD_FILL;
    const visibleLaneIndexes = new Set(this.visibleLaneIndexes());
    this.lanes.forEach((_lane, laneIndex) => {
      try {
        if (this.displayContext && visibleLaneIndexes.has(laneIndex)) {
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

  appliesLoopingDuringPlay(): boolean {
    return true;
  }

  pause(): void {
    if (this.currentLaneIndex === null) {
      return;
    }

    const lane = this.currentLane();
    if (lane && this.laneState(this.currentLaneIndex) === 'PLAYING') {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${this.currentLaneIndex + 1} pause requested ${this.traceContext()}`);
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
      this.logger.info('avplay-trace', `slot ${this.index} lane ${this.currentLaneIndex + 1} resume requested ${this.traceContext()}`);
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
    this.clearAllPreparedMetadata();
    this.updateObjectVisibility();
  }

  state(): string {
    const lane = this.currentLane();
    if (!lane) {
      return 'IDLE';
    }

    return getPlayerState(lane.player);
  }

  stopCurrentForCompletedStream(): void {
    if (this.currentLaneIndex === null) {
      return;
    }

    const laneIndex = this.currentLaneIndex;
    this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} completed stream will switch on next lane ${this.traceContext()}`);
  }

  prepareNextVideo(item: SeamlessContentItem, role: AvplayPreparedRole = 'next-content'): void {
    if (item.contentType !== 'Video' || this.currentItem?.id === item.id) {
      return;
    }

    const existing = this.preparedLanes.get(role);
    if (existing && existing.itemId === item.id && this.laneState(existing.laneIndex) === 'READY') {
      this.logger.info('avplay-trace', `session ${this.index} prepareNextVideo cache-hit role=${role} lane ${existing.laneIndex + 1} ${this.traceContext()}${this.formatTraceDetail(item.name)}`);
      return;
    }

    const laneIndex = this.resolvePrepareLaneIndex(role);
    if (laneIndex === null) {
      this.logger.info('avplay-trace', `session ${this.index} prepareNextVideo skipped no idle lane role=${role} ${this.traceContext()}${this.formatTraceDetail(item.name)}`);
      return;
    }

    const sourceUrl = resolveAvplaySourceUrl(item.sourceUrl);
    this.logger.info('avplay', `slot ${this.index} lane ${laneIndex + 1} prepare-next role=${role}: ${item.name}`);
    try {
      this.resetLaneForPlayback(laneIndex);
      this.configureLaneForItem(laneIndex, item, sourceUrl);
      this.applyOffscreenRectToLane(laneIndex, item.name);
      this.prepareLane(laneIndex, item.name);
      if (this.laneState(laneIndex) !== 'READY') {
        const state = this.laneState(laneIndex);
        this.logger.warn('avplay-trace', `session ${this.index} prepareNextVideo not ready role=${role} lane ${laneIndex + 1} state=${state} ${this.traceContext()}${this.formatTraceDetail(item.name)}`);
        this.resetFailedLane(laneIndex, `prepare-next not-ready role=${role} ${item.name}`);
        return;
      }
    } catch (error) {
      this.resetFailedLane(laneIndex, `prepare-next role=${role} ${item.name}`, error);
      throw error;
    }
    this.clearPreparedMetadataForLane(laneIndex);
    this.preparedLanes.set(role, {
      role,
      laneIndex,
      itemId: item.id,
      itemName: item.name,
    });
    this.logger.info('avplay-trace', `session ${this.index} prepareNextVideo ready role=${role} lane ${laneIndex + 1} ${this.traceContext()}${this.formatTraceDetail(item.name)}`);
    this.updateObjectVisibility();
  }

  debugSnapshot(): RuntimeAvplaySessionSnapshot {
    return {
      sessionIndex: this.index,
      currentLaneIndex: this.currentLaneIndex,
      heldLaneIndex: this.heldLaneIndex,
      currentItemName: this.currentItem?.name ?? null,
      lanes: this.lanes.map((lane, laneIndex) => {
        const runtime = this.laneRuntimeStates[laneIndex] ?? this.createLaneRuntimeState();
        const callbackAgeMs = runtime.callbackCurrentTimeAtMs === null
          ? null
          : Math.max(0, Math.round(performance.now() - runtime.callbackCurrentTimeAtMs));
        return {
          laneIndex,
          role: this.resolveLaneRole(laneIndex),
          itemName: runtime.itemName,
          state: this.laneState(laneIndex),
          queriedCurrentTimeMs: this.readLaneNumber(laneIndex, 'getCurrentTime'),
          queriedDurationMs: this.readLaneNumber(laneIndex, 'getDuration'),
          callbackCurrentTimeMs: runtime.callbackCurrentTimeMs,
          callbackAgeMs,
          buffering: runtime.buffering,
          lastPlayAt: runtime.lastPlayAt,
          lastPrepareCompletedAt: runtime.lastPrepareCompletedAt,
          lastBufferingStartAt: runtime.lastBufferingStartAt,
          lastBufferingCompleteAt: runtime.lastBufferingCompleteAt,
          lastStreamCompletedAt: runtime.lastStreamCompletedAt,
          lastError: runtime.lastError,
          visibility: lane.objectElement.style.visibility,
          zIndex: lane.objectElement.style.zIndex,
          rect: `${lane.objectElement.style.left || '-'},${lane.objectElement.style.top || '-'},${lane.objectElement.style.width || '-'}x${lane.objectElement.style.height || '-'}`,
        };
      }),
    };
  }

  private createLaneListener(laneIndex: number): AVPlayListener {
    return {
      onbufferingstart: () => {
        const runtime = this.laneRuntimeStates[laneIndex];
        runtime.buffering = true;
        runtime.lastBufferingStartAt = new Date().toISOString();
        this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} buffering start`);
      },
      onbufferingcomplete: () => {
        const runtime = this.laneRuntimeStates[laneIndex];
        runtime.buffering = false;
        runtime.lastBufferingCompleteAt = new Date().toISOString();
        this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} buffering complete`);
      },
      oncurrentplaytime: (currentTime) => {
        const runtime = this.laneRuntimeStates[laneIndex];
        runtime.callbackCurrentTimeMs = Math.round(currentTime);
        runtime.callbackCurrentTimeAtMs = performance.now();
      },
      onstreamcompleted: () => {
        this.laneRuntimeStates[laneIndex].lastStreamCompletedAt = new Date().toISOString();
        this.logger.info('avplay-trace', `event onstreamcompleted slot ${this.index} lane ${laneIndex + 1} state=${this.laneState(laneIndex)} ${this.traceContext()}`);
        void this.handleStreamCompleted(laneIndex);
      },
      onerror: (error) => {
        const message = `slot ${this.index} lane ${laneIndex + 1} AVPlay 오류: ${formatAvplayError(error, '')}`;
        this.laneRuntimeStates[laneIndex].lastError = message;
        this.logger.error('avplay-trace', `event onerror slot ${this.index} lane ${laneIndex + 1} state=${this.laneState(laneIndex)} ${this.traceContext()} error=${formatAvplayError(error, '')}`);
        this.events.onError(message);
      },
      onerrormsg: (error, message) => {
        const errorMessage = `slot ${this.index} lane ${laneIndex + 1} AVPlay 오류: ${formatAvplayError(error, message)}`;
        this.laneRuntimeStates[laneIndex].lastError = errorMessage;
        this.logger.error('avplay-trace', `event onerrormsg slot ${this.index} lane ${laneIndex + 1} state=${this.laneState(laneIndex)} ${this.traceContext()} error=${formatAvplayError(error, message)}`);
        this.events.onError(errorMessage);
      },
    };
  }

  private async handleStreamCompleted(laneIndex: number): Promise<void> {
    if (laneIndex !== this.currentLaneIndex) {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} stale stream completed ignored ${this.traceContext()}`);
      return;
    }

    const completedItem = this.currentItem;
    this.logger.info('avplay', `slot ${this.index} lane ${laneIndex + 1} stream completed: ${completedItem?.name ?? '-'}`);
    const shouldComplete = await this.currentEndedHandler?.();
    if (shouldComplete === false) {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} stream completed deferred ${this.traceContext()}`);
      return;
    }

    if (this.currentLaneIndex !== laneIndex || this.currentItem?.id !== completedItem?.id) {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} stream completed 후 이미 전환됨 ${this.traceContext()}`);
      return;
    }

    this.stopLane(laneIndex);
    this.heldLaneIndex = null;
    this.currentLaneIndex = null;
    this.currentItem = null;
    this.currentEndedHandler = null;
    this.clearPreparedMetadataForLane(laneIndex);
    this.updateObjectVisibility();
    this.events.onEnded();
  }

  private currentLane(): AvplayPairSession | null {
    if (this.currentLaneIndex === null) {
      return null;
    }

    return this.lanes[this.currentLaneIndex];
  }

  private resetLaneForPlayback(laneIndex: number): void {
    this.logger.info('avplay-trace', `session ${this.index} resetLane lane ${laneIndex + 1} state=${this.laneState(laneIndex)} ${this.traceContext()}`);
    this.stopLane(laneIndex);
    if (this.heldLaneIndex === laneIndex) {
      this.heldLaneIndex = null;
    }
  }

  private resetFailedLane(laneIndex: number, reason: string, error?: unknown): void {
    const message = error === undefined ? reason : `${reason}: ${formatAvplayError(error as AVPlayErrorLike, String(error))}`;
    this.logger.warn('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} reset failed lane ${this.traceContext()}${this.formatTraceDetail(message)}`);
    this.laneRuntimeStates[laneIndex].lastError = message;
    this.clearPreparedMetadataForLane(laneIndex);
    if (this.currentLaneIndex === laneIndex) {
      this.currentLaneIndex = null;
    }
    if (this.heldLaneIndex === laneIndex) {
      this.heldLaneIndex = null;
    }
    this.stopLane(laneIndex);
    this.closeLaneForReset(laneIndex, reason);
    this.laneRuntimeStates[laneIndex] = this.createLaneRuntimeState();
    this.hideLaneSurface(laneIndex, `reset-failed ${reason}`);
    this.updateObjectVisibility();
  }

  private prepareLane(laneIndex: number, itemName: string): void {
    const lane = this.lanes[laneIndex];
    const traceId = this.traceBegin(laneIndex, 'prepare', itemName);
    try {
      lane.player.prepare();
      this.laneRuntimeStates[laneIndex].lastPrepareCompletedAt = new Date().toISOString();
      this.traceEnd(traceId, laneIndex, 'prepare', itemName);
      this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} prepared: ${itemName}`);
    } catch (error) {
      this.traceFail(traceId, laneIndex, 'prepare', error, itemName);
      throw new Error(`slot ${this.index} lane ${laneIndex + 1} AVPlay prepare 오류: ${formatAvplayError(error as AVPlayErrorLike, String(error))}`);
    }
  }

  private configureLaneForItem(laneIndex: number, item: SeamlessContentItem, sourceUrl: string): void {
    const lane = this.lanes[laneIndex];
    this.laneRuntimeStates[laneIndex] = this.createLaneRuntimeState(item.name);
    this.callLane(laneIndex, 'open', () => {
      lane.player.open(sourceUrl);
    }, `${item.name} ${sourceUrl}`);
    this.callLaneSafe(laneIndex, 'setListener', () => {
      lane.player.setListener(this.createLaneListener(laneIndex));
    }, item.name);
  }

  private setLaneDisplayMethod(laneIndex: number, displayMethod: string): void {
    const lane = this.lanes[laneIndex];
    const state = this.laneState(laneIndex);
    if (!DISPLAY_METHOD_STATES.has(state)) {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} setDisplayMethod skipped state=${state} ${this.traceContext()}`);
      return;
    }

    this.callLaneSafe(laneIndex, 'setDisplayMethod', () => {
      lane.player.setDisplayMethod?.(displayMethod);
    }, displayMethod);
  }

  private applyOffscreenRectToLane(laneIndex: number, itemName: string): void {
    const lane = this.lanes[laneIndex];
    const viewportWidth = Math.max(window.visualViewport?.width ?? 0, document.documentElement.clientWidth, window.innerWidth, 1);
    const viewportHeight = Math.max(window.visualViewport?.height ?? 0, document.documentElement.clientHeight, window.innerHeight, 1);
    const targetWidth = Math.max(window.screen?.width ?? 0, Math.round(viewportWidth * (window.devicePixelRatio || 1)), viewportWidth);
    const targetHeight = Math.max(window.screen?.height ?? 0, Math.round(viewportHeight * (window.devicePixelRatio || 1)), viewportHeight);
    const left = Math.max(1, Math.round(targetWidth + 1));
    const top = Math.max(1, Math.round(targetHeight + 1));

    lane.objectElement.style.left = `${Math.round(viewportWidth + 1)}px`;
    lane.objectElement.style.top = `${Math.round(viewportHeight + 1)}px`;
    lane.objectElement.style.width = '1px';
    lane.objectElement.style.height = '1px';
    this.callLaneSafe(laneIndex, 'setDisplayRect.offscreen', () => {
      lane.player.setDisplayRect(left, top, 1, 1);
    }, `${itemName} ${left},${top},1x1`);
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

  private closeLaneForReset(laneIndex: number, reason: string): void {
    const lane = this.lanes[laneIndex];
    const state = this.laneState(laneIndex);
    const runtime = this.laneRuntimeStates[laneIndex] ?? this.createLaneRuntimeState();
    if (state === 'IDLE' && runtime.itemName === null && runtime.lastError === null) {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} close.reset skipped clean IDLE ${this.traceContext()}${this.formatTraceDetail(reason)}`);
      return;
    }

    this.callLaneSafe(laneIndex, 'close.reset', () => {
      lane.player.close?.();
    }, reason);
  }

  private applyDisplayRectToLane(laneIndex: number, slot: SeamlessSlotPlan, slotElement: HTMLElement): void {
    const lane = this.lanes[laneIndex];
    const state = this.laneState(laneIndex);
    if (state !== 'IDLE' && state !== 'READY' && state !== 'PLAYING' && state !== 'PAUSED') {
      this.logger.info('avplay-trace', `slot ${this.index} lane ${laneIndex + 1} setDisplayRect skipped state=${state} ${this.traceContext()}`);
      return;
    }

    const viewportWidth = Math.max(window.visualViewport?.width ?? 0, document.documentElement.clientWidth, window.innerWidth, 1);
    const viewportHeight = Math.max(window.visualViewport?.height ?? 0, document.documentElement.clientHeight, window.innerHeight, 1);
    const rect = this.resolveSlotViewportRect(slot, slotElement, viewportWidth, viewportHeight);
    const targetWidth = Math.max(window.screen?.width ?? 0, Math.round(viewportWidth * (window.devicePixelRatio || 1)), viewportWidth);
    const targetHeight = Math.max(window.screen?.height ?? 0, Math.round(viewportHeight * (window.devicePixelRatio || 1)), viewportHeight);
    const left = Math.round((rect.left / viewportWidth) * targetWidth);
    const top = Math.round((rect.top / viewportHeight) * targetHeight);
    const width = Math.max(1, Math.round((rect.width / viewportWidth) * targetWidth));
    const height = Math.max(1, Math.round((rect.height / viewportHeight) * targetHeight));

    lane.objectElement.style.left = `${rect.left}px`;
    lane.objectElement.style.top = `${rect.top}px`;
    lane.objectElement.style.width = `${rect.width}px`;
    lane.objectElement.style.height = `${rect.height}px`;
    this.callLaneSafe(laneIndex, 'setDisplayRect', () => {
      lane.player.setDisplayRect(left, top, width, height);
    }, `${left},${top},${width}x${height}`);
    this.logger.debug('avplay', `slot ${this.index} lane ${laneIndex + 1} rect ${slot.left},${slot.top},${slot.width}x${slot.height}`);
  }

  private resolveSlotViewportRect(
    slot: SeamlessSlotPlan,
    slotElement: HTMLElement,
    viewportWidth: number,
    viewportHeight: number,
  ): { readonly left: number; readonly top: number; readonly width: number; readonly height: number } {
    const rect = slotElement.getBoundingClientRect();
    if (rect.width > 0 && rect.height > 0) {
      return rect;
    }

    const stage = slotElement.parentElement as HTMLElement | null;
    const stageRect = stage?.getBoundingClientRect();
    const stageWidth = stageRect && stageRect.width > 0 ? stageRect.width : viewportWidth;
    const stageHeight = stageRect && stageRect.height > 0 ? stageRect.height : viewportHeight;
    const stageLeft = stageRect && stageRect.width > 0 ? stageRect.left : 0;
    const stageTop = stageRect && stageRect.height > 0 ? stageRect.top : 0;
    const canvasWidth = this.readStageCanvasSize(stage, '--canvas-width', Math.max(slot.left + slot.width, 1));
    const canvasHeight = this.readStageCanvasSize(stage, '--canvas-height', Math.max(slot.top + slot.height, 1));

    return {
      left: stageLeft + (slot.left / canvasWidth) * stageWidth,
      top: stageTop + (slot.top / canvasHeight) * stageHeight,
      width: (slot.width / canvasWidth) * stageWidth,
      height: (slot.height / canvasHeight) * stageHeight,
    };
  }

  private readStageCanvasSize(stage: HTMLElement | null, propertyName: '--canvas-width' | '--canvas-height', fallback: number): number {
    const value = Number.parseFloat(stage?.style.getPropertyValue(propertyName) ?? '');
    return Number.isFinite(value) && value > 0 ? value : fallback;
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

  private resolvePreparedLane(item: SeamlessContentItem): PreparedLaneMetadata | null {
    const roles: AvplayPreparedRole[] = ['next-transition-content', 'next-content'];
    for (const role of roles) {
      const prepared = this.preparedLanes.get(role);
      if (
        prepared
        && prepared.laneIndex !== this.currentLaneIndex
        && prepared.itemId === item.id
        && this.laneState(prepared.laneIndex) === 'READY'
      ) {
        return prepared;
      }
    }

    return null;
  }

  private visibleLaneIndexes(): number[] {
    const laneIndexes: number[] = [];
    if (this.currentLaneIndex !== null) {
      laneIndexes.push(this.currentLaneIndex);
    }
    if (this.heldLaneIndex !== null && this.heldLaneIndex !== this.currentLaneIndex) {
      laneIndexes.push(this.heldLaneIndex);
    }

    return laneIndexes;
  }

  private resolvePrepareLaneIndex(role: AvplayPreparedRole): number | null {
    const existing = this.preparedLanes.get(role);
    if (existing && existing.laneIndex !== this.currentLaneIndex) {
      return existing.laneIndex;
    }

    for (let laneIndex = 0; laneIndex < this.lanes.length; laneIndex += 1) {
      if (
        laneIndex !== this.currentLaneIndex
        && laneIndex !== this.heldLaneIndex
        && !this.findPreparedMetadataByLane(laneIndex)
      ) {
        return laneIndex;
      }
    }

    return null;
  }

  private clearPreparedRole(role: AvplayPreparedRole): void {
    const prepared = this.preparedLanes.get(role);
    if (!prepared) {
      return;
    }

    if (prepared.laneIndex !== this.currentLaneIndex) {
      this.stopLane(prepared.laneIndex);
    }
    this.preparedLanes.delete(role);
  }

  private clearAllPreparedRoles(): void {
    const prepared = [...this.preparedLanes.values()];
    prepared.forEach((metadata) => {
      if (metadata.laneIndex !== this.currentLaneIndex) {
        this.stopLane(metadata.laneIndex);
      }
    });
    this.clearAllPreparedMetadata();
  }

  private clearAllPreparedMetadata(): void {
    this.preparedLanes.clear();
  }

  private clearPreparedMetadataForLane(laneIndex: number): void {
    [...this.preparedLanes.entries()].forEach(([role, metadata]) => {
      if (metadata.laneIndex === laneIndex) {
        this.preparedLanes.delete(role);
      }
    });
  }

  private findPreparedMetadataByLane(laneIndex: number): PreparedLaneMetadata | null {
    for (const metadata of this.preparedLanes.values()) {
      if (metadata.laneIndex === laneIndex) {
        return metadata;
      }
    }

    return null;
  }

  private resolveLaneRole(laneIndex: number): RuntimeAvplayLaneSnapshot['role'] {
    if (laneIndex === this.currentLaneIndex) {
      return 'current';
    }
    if (laneIndex === this.heldLaneIndex) {
      return 'held';
    }

    return this.findPreparedMetadataByLane(laneIndex)?.role ?? 'idle';
  }

  private readLaneNumber(laneIndex: number, operation: 'getCurrentTime' | 'getDuration'): number | null {
    try {
      const value = this.lanes[laneIndex].player[operation]?.();
      return typeof value === 'number' && Number.isFinite(value) ? Math.round(value) : null;
    } catch {
      return null;
    }
  }

  private createLaneRuntimeState(itemName: string | null = null): AvplayLaneRuntimeState {
    return {
      itemName,
      callbackCurrentTimeMs: null,
      callbackCurrentTimeAtMs: null,
      buffering: false,
      lastPlayAt: null,
      lastPrepareCompletedAt: null,
      lastBufferingStartAt: null,
      lastBufferingCompleteAt: null,
      lastStreamCompletedAt: null,
      lastError: null,
    };
  }

  private traceContext(): string {
    const prepared = [...this.preparedLanes.values()]
      .map((metadata) => `${metadata.role}=lane${metadata.laneIndex + 1}:${metadata.itemName}`)
      .join(',');
    return `current=${this.currentLaneIndex !== null ? this.currentLaneIndex + 1 : '-'} held=${this.heldLaneIndex !== null ? this.heldLaneIndex + 1 : '-'} prepared=${prepared || '-'} item=${this.currentItem?.name ?? '-'}`;
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
        lane.objectElement.style.zIndex = String(slotZIndex + AVPLAY_LAYER_CURRENT_OFFSET);
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
): AvplaySessionPair {
  const store = window.webapis?.avplaystore;
  if (!store) {
    throw new Error('AVPlayStore API를 찾지 못했습니다. avplay-seamless-still-mode 기준 재생은 삼성 Tizen Signage 실장비의 webapis.avplaystore가 필요합니다.');
  }

  const createPairSession = (): AvplayPairSession => {
    const player = store.getPlayer();
    if (!player) {
      throw new Error('AVPlayStore 플레이어를 확보하지 못했습니다.');
    }

    return {
      player,
      objectElement: document.createElement('object'),
    };
  };

  return new AvplaySessionPair(
    index,
    [
      createPairSession(),
      createPairSession(),
      createPairSession(),
      createPairSession(),
    ],
    host,
    logger,
    events,
  );
}

export { AvplaySessionPair as AvplaySession };
