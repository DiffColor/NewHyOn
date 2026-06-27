import type { RingLogger } from '../core/logger';
import type { SeamlessContentItem, SeamlessSlotPlan } from '../domain/page-plan';
import type { AvplaySession } from './avplay-session';
import { resolveImageSourceUrl } from './source-resolver';

type ContentShownHandler = (slotIndex: number, item: SeamlessContentItem) => void;
type PageBoundaryContentEndHandler = (slotIndex: number, item: SeamlessContentItem) => void;
type ContentPlayablePredicate = (item: SeamlessContentItem) => boolean;
type VideoTransitionMode = 'timer' | 'event';
const VIDEO_DURATION_MATCH_TOLERANCE_MS = 250;
const PAGE_END_VIDEO_COMPLETION_GRACE_MS = 2000;
const IMAGE_MAIN_THREAD_GAP_WARN_MS = 250;

export interface SlotPlayerTimelineSnapshot {
  readonly slotIndex: number;
  readonly slotName: string;
  readonly itemIndex: number;
  readonly itemCount: number;
  readonly itemName: string;
  readonly contentType: SeamlessContentItem['contentType'] | 'Empty';
  readonly elapsedMs: number;
  readonly durationMs: number;
  readonly remainingMs: number;
  readonly progress: number;
  readonly nextItemIndex: number | null;
  readonly nextItemName: string | null;
  readonly nextTransitionText: string;
  readonly failureMessage: string | null;
}

export class SlotPlayer {
  private readonly imageA = document.createElement('img');
  private readonly imageB = document.createElement('img');
  private readonly videoMask = document.createElement('div');
  private currentImage: HTMLImageElement;
  private standbyImage: HTMLImageElement;
  private itemIndex = 0;
  private itemStartedAt = -1;
  private itemPausedElapsedMs = 0;
  private active = false;
  private switchingItem = false;
  private videoSession: AvplaySession | null = null;
  private failureMessage: string | null = null;
  private preparedItemIndex: number | null = null;
  private preparedItemId: string | null = null;
  private preparePromise: Promise<void> | null = null;
  private preparedImageId: string | null = null;
  private preparedImagePromise: Promise<void> | null = null;
  private preparedImageElement: HTMLImageElement | null = null;
  private contentGeneration = 0;
  private layoutCanvasWidth = 0;
  private layoutCanvasHeight = 0;
  private pageElapsedMs = 0;
  private pageDurationMs = Number.POSITIVE_INFINITY;
  private loopCurrentPageAtPageEnd = false;
  private contentEndReachedAtPageBoundary = false;
  private pageBoundaryVideoWaitStartedAt = -1;
  private readonly videoDurationMsByItemId = new Map<string, number>();
  private currentVideoLoopState: boolean | null = null;
  private currentVideoCompletionCount = 0;

  constructor(
    private readonly slotIndex: number,
    private readonly element: HTMLElement,
    private slot: SeamlessSlotPlan,
    private preserveAspectRatio: boolean,
    private switchOnContentEnd: boolean,
    private readonly getVideoSession: () => AvplaySession,
    private readonly logger: RingLogger,
    private readonly onContentShown: ContentShownHandler = () => undefined,
    private readonly waitForVideoFirstFrame = false,
    private readonly releaseVideoSession: (session: AvplaySession) => void = () => undefined,
    private readonly onPageBoundaryContentEnd: PageBoundaryContentEndHandler = () => undefined,
    private readonly isPageTransitionScheduled: () => boolean = () => false,
    private readonly isContentPlayable: ContentPlayablePredicate = () => true,
  ) {
    this.currentImage = this.imageA;
    this.standbyImage = this.imageB;
    this.imageA.className = 'slot-image slot-image--visible';
    this.imageB.className = 'slot-image';
    this.videoMask.className = 'slot-video-mask';
    this.element.append(this.videoMask, this.imageA, this.imageB);
  }

  updatePlaybackSettings(preserveAspectRatio: boolean, switchOnContentEnd: boolean): void {
    this.preserveAspectRatio = preserveAspectRatio;
    this.switchOnContentEnd = switchOnContentEnd;
    this.applyImageDisplayMode();
    this.videoSession?.applyDisplayMethod(preserveAspectRatio);
  }

  async start(): Promise<boolean> {
    this.applySlotVisibility();
    if (this.slot.items.length === 0) {
      this.failureMessage = null;
      this.element.classList.remove('slot--video-active');
      this.element.classList.add('slot--empty');
      return true;
    }

    this.active = true;
    this.failureMessage = null;
    const firstPlayableIndex = this.firstPlayableIndex();
    if (firstPlayableIndex === null) {
      this.active = false;
      this.element.classList.remove('slot--video-active');
      this.element.classList.add('slot--empty');
      return true;
    }

    this.itemIndex = firstPlayableIndex;
    return this.showCurrentItem();
  }

  async switchToSlotPlan(slot: SeamlessSlotPlan, canvasWidth: number, canvasHeight: number): Promise<boolean> {
    const previousSlot = this.slot;
    const previousItemIndex = this.itemIndex;
    const previousActive = this.active;
    const previousFailureMessage = this.failureMessage;
    const previousItemStartedAt = this.itemStartedAt;
    const previousItemPausedElapsedMs = this.itemPausedElapsedMs;
    const previousCanvasWidth = this.layoutCanvasWidth;
    const previousCanvasHeight = this.layoutCanvasHeight;
    const incomingFirstItemIndex = this.firstPlayableIndex(slot);
    const incomingFirstItem = incomingFirstItemIndex !== null ? slot.items[incomingFirstItemIndex] ?? null : null;
    if (!incomingFirstItem) {
      this.clearPreparedImage();
      this.clearPreparedContent();
    } else if (incomingFirstItem.contentType === 'Image') {
      if (this.preparedImageId !== incomingFirstItem.id) {
        this.clearPreparedImage();
      }
    } else if (this.preparedItemId !== incomingFirstItem.id) {
      this.clearPreparedContent();
    }
    this.slot = slot;
    this.applyLayout(canvasWidth, canvasHeight);
    this.itemIndex = incomingFirstItemIndex ?? 0;
    this.failureMessage = null;
    if (incomingFirstItemIndex === null || slot.width <= 0 || slot.height <= 0) {
      this.active = false;
      this.resetItemClock();
      this.releaseCurrentVideoSession();
      this.hideImages();
      this.currentImage.removeAttribute('src');
      this.standbyImage.removeAttribute('src');
      this.element.classList.remove('slot--video-active');
      this.element.classList.add('slot--empty');
      return true;
    }

    this.active = true;
    this.element.classList.remove('slot--empty');
    const started = await this.showCurrentItemAtElapsed(0, { preserveCurrentOnFailure: true });
    if (!started) {
      this.slot = previousSlot;
      this.itemIndex = previousItemIndex;
      this.active = previousActive;
      this.failureMessage = previousFailureMessage;
      this.itemStartedAt = previousItemStartedAt;
      this.itemPausedElapsedMs = previousItemPausedElapsedMs;
      if (previousCanvasWidth > 0 && previousCanvasHeight > 0) {
        this.applyLayout(previousCanvasWidth, previousCanvasHeight);
      } else {
        this.applySlotVisibility();
      }
      const previousItem = this.currentItem();
      this.element.classList.toggle('slot--video-active', previousItem?.contentType === 'Video' && this.videoSession !== null);
      return false;
    }

    return true;
  }

  applyLayout(canvasWidth: number, canvasHeight: number): void {
    this.layoutCanvasWidth = canvasWidth;
    this.layoutCanvasHeight = canvasHeight;
    this.element.style.left = `${(this.slot.left / canvasWidth) * 100}%`;
    this.element.style.top = `${(this.slot.top / canvasHeight) * 100}%`;
    this.element.style.width = `${(this.slot.width / canvasWidth) * 100}%`;
    this.element.style.height = `${(this.slot.height / canvasHeight) * 100}%`;
    this.element.style.zIndex = String(this.slot.zIndex);
    this.applySlotVisibility();
  }

  setPageTimeline(pageElapsedMs: number, pageDurationMs: number, loopCurrentPageAtPageEnd: boolean): void {
    this.pageElapsedMs = Math.max(0, pageElapsedMs);
    this.pageDurationMs = Math.max(1, pageDurationMs);
    this.loopCurrentPageAtPageEnd = loopCurrentPageAtPageEnd;
    if (!this.isPageTimelineExpired()) {
      this.pageBoundaryVideoWaitStartedAt = -1;
    }
  }

  stop(): void {
    this.nextContentGeneration();
    this.active = false;
    this.resetItemClock();
    this.clearPreparedContent();
    this.clearPreparedImage();
    this.releaseCurrentVideoSession();
    this.currentImage.removeAttribute('src');
    this.standbyImage.removeAttribute('src');
    this.currentImage.classList.remove('slot-image--visible');
    this.standbyImage.classList.remove('slot-image--visible');
    this.currentImage.classList.remove('slot-image--prepared');
    this.standbyImage.classList.remove('slot-image--prepared');
    this.element.classList.remove('slot--video-active');
    this.failureMessage = null;
  }

  pause(): void {
    const item = this.currentItem();
    if (item && this.itemStartedAt >= 0) {
      this.itemPausedElapsedMs = this.currentItemTimelineElapsedMilliseconds(item);
    }
    this.itemStartedAt = -1;
    this.videoSession?.pause();
  }

  resume(): void {
    if (!this.active) {
      return;
    }

    this.videoSession?.resume();
    const item = this.currentItem();
    if (item) {
      this.startPassiveItemClock(item, this.itemPausedElapsedMs);
    }
    void this.prepareNextContent();
  }

  async restartFromBeginning(): Promise<void> {
    if (!this.active || this.slot.items.length === 0) {
      return;
    }

    this.resetItemClock();
    this.clearPreparedContent();
    this.clearPreparedImage();
    if (!this.canAdvanceContent()) {
      return;
    }

    this.itemIndex = this.firstPlayableIndex() ?? 0;
    await this.showCurrentItem();
  }

  applyDisplayRect(): void {
    const item = this.currentItem();
    if (item?.contentType === 'Video') {
      this.videoSession?.applyDisplayRect(this.slot, this.element);
    }
  }

  prepareFirstContentForSlotPlan(slot: SeamlessSlotPlan): Promise<void> | null {
    if (!this.active || slot.items.length === 0 || slot.width <= 0 || slot.height <= 0) {
      return null;
    }

    const firstPlayableIndex = this.firstPlayableIndex(slot);
    const item = firstPlayableIndex !== null ? slot.items[firstPlayableIndex] ?? null : null;
    if (!item) {
      return null;
    }

    if (this.preparedItemId === item.id && this.preparePromise) {
      return this.preparePromise;
    }

    if (item.contentType === 'Image') {
      if (this.preparedImageId !== item.id || !this.preparedImagePromise) {
        this.clearPreparedImage();
        const image = this.imageElementForPreparation(item);
        this.preparedImageId = item.id;
        this.preparedImageElement = image;
        this.preparedImagePromise = this.prepareImageElement(
          image,
          item,
          'page-boundary-first',
          { underVideo: this.currentItem()?.contentType === 'Video' },
        ).catch((error) => {
          this.clearPreparedImage();
          throw error;
        });
      }

      const pageBoundaryPreparePromise = this.preparedImagePromise
        .then(() => undefined)
        .catch((error) => {
          this.logger.warn('slot', `slot ${this.slotIndex + 1} 다음 페이지 첫 콘텐츠 준비 실패: ${String(error)}`);
          throw error;
        });

      return pageBoundaryPreparePromise;
    }

    const pageBoundaryPreparePromise = this.prepareVideoContentForSlotPlan(item, 0, slot)
      .then(() => undefined)
      .catch((error) => {
        this.logger.warn('slot', `slot ${this.slotIndex + 1} 다음 페이지 첫 콘텐츠 준비 실패: ${String(error)}`);
        throw error;
      });

    return pageBoundaryPreparePromise;
  }

  blocksPageTransitionForContentEnd(): boolean {
    const item = this.currentItem();
    if (!this.active || !item) {
      return false;
    }

    if (item.contentType === 'Image') {
      return this.switchOnContentEnd && !this.hasCurrentItemDurationEnded(item);
    }

    if (!this.shouldWaitForVideoEndForPageTransition(item) || this.contentEndReachedAtPageBoundary) {
      this.pageBoundaryVideoWaitStartedAt = -1;
      return false;
    }

    if (!this.isPageTimelineExpired()) {
      return true;
    }

    if (this.pageBoundaryVideoWaitStartedAt < 0) {
      this.pageBoundaryVideoWaitStartedAt = performance.now();
    }

    const waitElapsedMs = performance.now() - this.pageBoundaryVideoWaitStartedAt;
    if (waitElapsedMs > PAGE_END_VIDEO_COMPLETION_GRACE_MS) {
      this.logger.warn('slot', `slot ${this.slotIndex + 1} 영상 종료 이벤트 대기 초과, 페이지 전환 허용: ${item.name}`);
      this.contentEndReachedAtPageBoundary = true;
      return false;
    }

    return true;
  }

  private applySlotVisibility(): void {
    this.element.classList.toggle('slot--empty', !this.hasPlayableItems() || this.slot.width <= 0 || this.slot.height <= 0);
  }

  snapshot(): string {
    const item = this.currentItem();
    if (this.failureMessage) {
      return `${this.slot.elementName || `slot-${this.slotIndex + 1}`}: ${item?.name ?? '-'} (ERROR: ${this.failureMessage})`;
    }

    if (!item) {
      return `${this.slot.elementName || `slot-${this.slotIndex + 1}`}: - (EMPTY)`;
    }

    if (item.contentType === 'Image') {
      return `${this.slot.elementName || `slot-${this.slotIndex + 1}`}: ${item.name} (IMAGE)`;
    }

    const state = this.videoSession?.state() ?? 'VIDEO_SESSION_NOT_READY';
    return `${this.slot.elementName || `slot-${this.slotIndex + 1}`}: ${item?.name ?? '-'} (${state})`;
  }

  timelineSnapshot(): SlotPlayerTimelineSnapshot {
    const slotName = this.slot.elementName || `slot-${this.slotIndex + 1}`;
    const item = this.currentItem();
    if (!item) {
      return {
        slotIndex: this.slotIndex,
        slotName,
        itemIndex: -1,
        itemCount: this.slot.items.length,
        itemName: '-',
        contentType: 'Empty',
        elapsedMs: 0,
        durationMs: 0,
        remainingMs: 0,
        progress: 0,
        nextItemIndex: null,
        nextItemName: null,
        nextTransitionText: '전환 없음',
        failureMessage: this.failureMessage,
      };
    }

    const durationMs = Math.max(1, item.durationSeconds) * 1000;
    const elapsedMs = this.currentItemTimelineElapsedMilliseconds(item);
    const remainingMs = Math.max(0, durationMs - elapsedMs);
    const nextItemIndex = this.canAdvanceContent() ? this.findNextPlayableIndex(this.itemIndex) : null;
    const nextItem = nextItemIndex !== null ? this.slot.items[nextItemIndex] ?? null : null;
    const transitionByTimer = this.shouldScheduleTimer(item);
    const progress = durationMs > 0 ? Math.min(1, Math.max(0, elapsedMs / durationMs)) : 0;

    return {
      slotIndex: this.slotIndex,
      slotName,
      itemIndex: this.itemIndex,
      itemCount: this.slot.items.length,
      itemName: item.name,
      contentType: item.contentType,
      elapsedMs,
      durationMs,
      remainingMs,
      progress,
      nextItemIndex,
      nextItemName: nextItem?.name ?? null,
      nextTransitionText: transitionByTimer
        ? `${this.formatSeconds(remainingMs)} 후 ${nextItem ? nextItem.name : '반복'}`
        : this.canAdvanceContent() ? '영상 종료 이벤트 대기' : `${this.formatSeconds(remainingMs)} 후 표시 타이머 반복`,
      failureMessage: this.failureMessage,
    };
  }

  private currentItem(): SeamlessContentItem | null {
    return this.slot.items[this.itemIndex] ?? null;
  }

  private async showCurrentItem(): Promise<boolean> {
    return this.showCurrentItemAtElapsed(0);
  }

  async syncToPageElapsed(pageElapsedMs: number, pageDurationMs = Number.POSITIVE_INFINITY, loopCurrentPageAtPageEnd = false): Promise<void> {
    this.setPageTimeline(pageElapsedMs, pageDurationMs, loopCurrentPageAtPageEnd);
    let item = this.currentItem();
    if (this.active && !this.switchingItem && (!item || !this.isItemPlayable(item))) {
      const playableIndex = this.firstPlayableIndex();
      if (playableIndex === null) {
        this.active = false;
        this.resetItemClock();
        this.clearPreparedContent();
        this.clearPreparedImage();
        await this.releaseCurrentVideoSession();
        this.hideImages();
        this.element.classList.remove('slot--video-active');
        this.element.classList.add('slot--empty');
        return;
      }

      this.itemIndex = playableIndex;
      this.resetItemClock();
      await this.showCurrentItemAtElapsed(0);
      item = this.currentItem();
    }

    if (item?.contentType === 'Video') {
      this.updateCurrentVideoLoopState(item);
    }
    if (!this.active || this.slot.items.length <= 1 || this.switchingItem || (item && this.shouldWaitForVideoEnd(item))) {
      return;
    }

    const target = this.resolveTimelineItem(pageElapsedMs);
    if (!target || target.itemIndex === this.itemIndex) {
      const item = this.currentItem();
      if (item) {
        this.startPassiveItemClock(item, target?.itemElapsedMs ?? this.currentItemTimelineElapsedMilliseconds(item));
      }
      return;
    }

    this.itemIndex = target.itemIndex;
    this.resetItemClock();
    await this.showCurrentItemAtElapsed(target.itemElapsedMs);
  }

  private async showCurrentItemAtElapsed(
    itemElapsedMs: number,
    options: { readonly preserveCurrentOnFailure?: boolean } = {},
  ): Promise<boolean> {
    const generation = this.nextContentGeneration();
    const item = this.currentItem();
    if (!item || !this.active || !this.isItemPlayable(item)) {
      return false;
    }

    this.switchingItem = true;
    this.resetItemClock();
    this.contentEndReachedAtPageBoundary = false;
    this.pageBoundaryVideoWaitStartedAt = -1;
    this.currentVideoLoopState = null;
    this.currentVideoCompletionCount = 0;
    try {
      let releaseBeforePrepareNext: Promise<void> | null = null;
      if (item.contentType === 'Image') {
        const hasActiveVideoSurface = this.videoSession !== null;
        const keepPreparedVideo = this.hasPreparedUpcomingVideo();
        let visiblePaintPromise: Promise<void> = Promise.resolve();
        await this.showImage(item, {
          onVisiblePaintPromise: (promise) => {
            visiblePaintPromise = promise;
          },
        });
        if (!this.isContentGenerationCurrent(generation)) {
          return false;
        }
        const surfaceDetached = hasActiveVideoSurface ? this.detachCurrentVideoSurfaceForTransition() : false;
        if (hasActiveVideoSurface) {
          releaseBeforePrepareNext = visiblePaintPromise
            .then(() => this.releaseCurrentVideoSession({
              keepPrepared: keepPreparedVideo,
              alreadyHidden: surfaceDetached,
            }))
            .then(() => {
              this.element.classList.remove('slot--video-active');
            });
        } else {
          this.element.classList.remove('slot--video-active');
        }
      } else {
        this.element.classList.add('slot--video-active');
        if (this.preparedItemId === item.id && this.preparePromise) {
          const waitStartedAt = performance.now();
          await this.preparePromise;
          this.logger.info('avplay', `slot ${this.slotIndex + 1} prepared video wait: ${item.name} +${this.elapsed(waitStartedAt)}ms`);
        }
        const shouldWaitForFirstFrame = this.waitForVideoFirstFrame || this.canAdvanceContent() || this.videoSession !== null;
        const nextVideoSession = this.videoSession ?? this.getVideoSession();
        this.prepareNextImageForCurrentVideo(generation);
        const playbackInfo = await nextVideoSession.play(item, this.slot, this.element, this.preserveAspectRatio, () => this.handleVideoEnded(item.id), {
          waitForFirstFrame: shouldWaitForFirstFrame,
        });
        if (!this.isContentGenerationCurrent(generation)) {
          return false;
        }
        this.recordVideoDuration(item, playbackInfo?.durationMs ?? null);
        this.videoSession = nextVideoSession;
        this.currentVideoLoopState = null;
        this.updateCurrentVideoLoopState(item, true);
        this.hideImages();
        if (this.preparedItemIndex === this.itemIndex) {
          this.clearPreparedContent();
        }
      }
      this.onContentShown(this.slotIndex, item);
      if (releaseBeforePrepareNext) {
        void releaseBeforePrepareNext
          .then(() => this.waitForPaint())
          .then(() => this.prepareNextContent(generation));
      } else {
        void this.prepareNextContent(generation);
      }
    } catch (error) {
      if (options.preserveCurrentOnFailure) {
        this.failureMessage = error instanceof Error ? error.message : String(error);
        this.logger.error('slot', `slot ${this.slotIndex + 1} 전환 준비 실패: ${this.failureMessage}`);
        return false;
      }
      this.suspendAfterFailure(error);
      return false;
    } finally {
      this.switchingItem = false;
    }

    this.startPassiveItemClock(item, itemElapsedMs);
    return true;
  }

  private shouldWaitForVideoEnd(item: SeamlessContentItem): boolean {
    return item.contentType === 'Video'
      && this.canAdvanceContent()
      && this.videoTransitionMode(item) === 'event';
  }

  private shouldWaitForVideoEndForPageTransition(item: SeamlessContentItem): boolean {
    if (item.contentType !== 'Video') {
      return false;
    }

    if (this.videoTransitionMode(item) === 'event') {
      return true;
    }

    const durationMs = this.videoDurationMsByItemId.get(item.id);
    if (durationMs === undefined) {
      return false;
    }

    const elapsedMs = this.currentItemRawElapsedMilliseconds();
    const elapsedWithinPlaybackMs = elapsedMs > durationMs
      ? elapsedMs % durationMs
      : elapsedMs;
    const remainingToStreamEndMs = elapsedWithinPlaybackMs === 0 && elapsedMs > 0
      ? 0
      : Math.max(0, durationMs - elapsedWithinPlaybackMs);
    return remainingToStreamEndMs > 0 && remainingToStreamEndMs <= PAGE_END_VIDEO_COMPLETION_GRACE_MS;
  }

  private shouldScheduleTimer(item: SeamlessContentItem): boolean {
    return this.canAdvanceContent() && !this.shouldWaitForVideoEnd(item);
  }

  private async handleVideoEnded(expectedItemId: string): Promise<boolean> {
    const item = this.currentItem();
    if (!this.active || !item || item.contentType !== 'Video') {
      return true;
    }

    if (item.id !== expectedItemId || this.switchingItem) {
      this.logger.info('slot', `slot ${this.slotIndex + 1} 전환 중 영상 종료 이벤트 보류: ${item.name}`);
      return false;
    }

    if (!this.loopCurrentPageAtPageEnd && (this.isPageTimelineExpired() || this.isPageTransitionScheduled())) {
      this.contentEndReachedAtPageBoundary = true;
      this.queuePageBoundaryContentEnd(item);
      return false;
    }

    if (this.videoTransitionMode(item) !== 'event') {
      return false;
    }

    this.currentVideoCompletionCount += 1;
    if (!this.isVideoCompletionEligibleForTransition(item)) {
      this.updateCurrentVideoLoopState(item, true);
      return false;
    }

    if (this.canAdvanceContent()) {
      await this.advance();
    }
    return true;
  }

  private queuePageBoundaryContentEnd(item: SeamlessContentItem): void {
    window.setTimeout(() => {
      if (!this.active || !this.contentEndReachedAtPageBoundary) {
        return;
      }

      this.onPageBoundaryContentEnd(this.slotIndex, item);
    }, 0);
  }

  private suspendAfterFailure(error: unknown): void {
    this.active = false;
    this.resetItemClock();
    this.clearPreparedContent();
    this.clearPreparedImage();
    this.releaseCurrentVideoSession();
    this.hideImages();
    this.element.classList.remove('slot--video-active');
    this.element.classList.add('slot--empty');
    this.failureMessage = error instanceof Error ? error.message : String(error);
    this.logger.error('slot', `slot ${this.slotIndex + 1} 중단: ${this.failureMessage}`);
  }

  private async showImage(
    item: SeamlessContentItem,
    options: {
      readonly waitForVisiblePaint?: boolean;
      readonly onVisibleApplied?: () => void;
      readonly onVisiblePaintPromise?: (promise: Promise<void>) => void;
    } = {},
  ): Promise<void> {
    const showStartedAt = performance.now();
    const image = this.preparedImageId === item.id && this.preparedImageElement
      ? this.preparedImageElement
      : this.standbyImage;
    const previousImage = image === this.currentImage ? this.standbyImage : this.currentImage;
    this.applyImageDisplayMode();

    if (this.preparedImageId === item.id && this.preparedImagePromise) {
      const waitStartedAt = performance.now();
      await this.preparedImagePromise;
      this.logImageTiming(item, 'show prepared wait', waitStartedAt, `total=${this.elapsed(showStartedAt)}ms`);
    } else {
      await this.prepareImageElement(image, item, 'show');
    }

    const visibleStartedAt = performance.now();
    previousImage.style.zIndex = '1';
    image.style.zIndex = '2';
    image.classList.remove('slot-image--prepared');
    image.classList.remove('slot-image--under-video');
    image.classList.add('slot-image--visible');
    previousImage.classList.remove('slot-image--visible');
    previousImage.classList.remove('slot-image--prepared');
    previousImage.classList.remove('slot-image--under-video');
    previousImage.style.zIndex = '0';
    image.style.zIndex = '1';
    this.currentImage = image;
    this.standbyImage = previousImage;
    this.consumePreparedImage(item.id);
    this.logImageTiming(item, 'visible class applied', visibleStartedAt, `total=${this.elapsed(showStartedAt)}ms`);
    options.onVisibleApplied?.();
    const visiblePaintPromise = this.waitForPaint().then(() => {
      this.logImageTiming(item, 'visible paint', visibleStartedAt, `total=${this.elapsed(showStartedAt)}ms`);
    });
    options.onVisiblePaintPromise?.(visiblePaintPromise);
    if (options.waitForVisiblePaint === true) {
      await visiblePaintPromise;
    } else {
      void visiblePaintPromise;
    }
    this.logger.info('slot', `slot ${this.slotIndex + 1} image: ${item.name}`);
  }

  private async prepareImageElement(
    image: HTMLImageElement,
    item: SeamlessContentItem,
    reason: string,
    options: { readonly underVideo?: boolean } = {},
  ): Promise<void> {
    const prepareStartedAt = performance.now();
    const sourceUrl = resolveImageSourceUrl(item.sourceUrl);
    this.applyImageDisplayMode();

    if (image.getAttribute('src') !== sourceUrl || !image.complete) {
      const loadStartedAt = performance.now();
      this.logImageTiming(item, `prepare start ${reason}`, prepareStartedAt, sourceUrl);
      await new Promise<void>((resolve, reject) => {
        image.onload = () => {
          this.logImageTiming(item, `load complete ${reason}`, loadStartedAt, `total=${this.elapsed(prepareStartedAt)}ms`);
          resolve();
        };
        image.onerror = () => reject(new Error(`이미지를 로드하지 못했습니다: ${item.name}`));
        image.src = sourceUrl;
      });
    } else {
      this.logImageTiming(item, `prepare cache-hit ${reason}`, prepareStartedAt, sourceUrl);
    }
    const decodeStartedAt = performance.now();
    this.logImageTiming(item, `decode start ${reason}`, decodeStartedAt, `total=${this.elapsed(prepareStartedAt)}ms`);
    const stopDecodeProbe = this.startImageMainThreadProbe(item, `decode ${reason}`, decodeStartedAt);
    try {
      const decodeCallStartedAt = performance.now();
      const decodePromise = image.decode?.() ?? Promise.resolve();
      this.logImageTiming(
        item,
        `decode promise ${reason}`,
        decodeCallStartedAt,
        `call=${this.elapsed(decodeCallStartedAt)}ms total=${this.elapsed(prepareStartedAt)}ms`,
      );
      await decodePromise;
    } finally {
      stopDecodeProbe();
    }
    this.logImageTiming(item, `decode complete ${reason}`, decodeStartedAt, `total=${this.elapsed(prepareStartedAt)}ms`);
    const preparedClassStartedAt = performance.now();
    image.classList.add('slot-image--prepared');
    if (options.underVideo === true) {
      image.classList.add('slot-image--under-video');
    } else {
      image.classList.remove('slot-image--under-video');
    }
    this.logImageTiming(item, `prepared class applied ${reason}`, preparedClassStartedAt, `total=${this.elapsed(prepareStartedAt)}ms`);
    const stopPreparedPaintProbe = this.startImageMainThreadProbe(item, `prepared paint ${reason}`, preparedClassStartedAt);
    void this.waitForPaint().then(() => {
      this.logImageTiming(item, `prepared paint ${reason}`, preparedClassStartedAt, `total=${this.elapsed(prepareStartedAt)}ms`);
    }).finally(() => {
      stopPreparedPaintProbe();
    });
  }

  private applyImageDisplayMode(): void {
    const objectFit = this.preserveAspectRatio ? 'contain' : 'fill';
    this.imageA.style.objectFit = objectFit;
    this.imageB.style.objectFit = objectFit;
  }

  private hideImages(): void {
    this.imageA.classList.remove('slot-image--visible');
    this.imageB.classList.remove('slot-image--visible');
    this.imageA.classList.remove('slot-image--prepared');
    this.imageB.classList.remove('slot-image--prepared');
    this.imageA.classList.remove('slot-image--under-video');
    this.imageB.classList.remove('slot-image--under-video');
  }

  private startPassiveItemClock(item: SeamlessContentItem, elapsedMs = 0): void {
    const durationMs = Math.max(1, item.durationSeconds) * 1000;
    const safeElapsedMs = Math.min(Math.max(0, elapsedMs), durationMs);
    this.itemPausedElapsedMs = safeElapsedMs;
    this.itemStartedAt = performance.now() - safeElapsedMs;
  }

  private async advance(): Promise<void> {
    if (!this.active || !this.canAdvanceContent()) {
      return;
    }

    const nextIndex = this.findNextPlayableIndex(this.itemIndex);
    if (nextIndex === null) {
      return;
    }

    this.itemIndex = nextIndex;
    this.resetItemClock();
    await this.showCurrentItemAtElapsed(0);
  }

  private async prepareNextContent(generation = this.contentGeneration): Promise<void> {
    if (!this.active || !this.canAdvanceContent() || !this.isContentGenerationCurrent(generation)) {
      return;
    }

    const currentItem = this.currentItem();
    if (!currentItem) {
      return;
    }

    const nextIndex = this.resolvePreparedItemIndex();
    if (nextIndex === null) {
      this.clearPreparedContent();
      return;
    }

    if (this.preparedItemIndex === nextIndex || this.preparePromise) {
      return;
    }

    const nextItem = this.slot.items[nextIndex];
    if (!nextItem) {
      return;
    }

    const preparePromise = nextItem.contentType === 'Image'
      ? this.prepareImageContent(nextItem, { underVideo: currentItem.contentType === 'Video' })
      : this.prepareVideoContentForSlotPlan(nextItem, nextIndex, this.slot);

    try {
      await preparePromise;
      if (!this.isContentGenerationCurrent(generation)) {
        return;
      }
    } catch {
      // The transition path will retry and surface a hard failure if the item still cannot start.
    }
  }

  private prepareImageContent(item: SeamlessContentItem, options: { readonly underVideo?: boolean } = {}): Promise<void> {
    if (this.preparedImageId === item.id && this.preparedImagePromise) {
      return this.preparedImagePromise;
    }

    this.clearPreparedImage();
    const image = this.imageElementForPreparation(item);
    this.preparedImageId = item.id;
    this.preparedImageElement = image;
    this.preparedImagePromise = this.prepareImageElement(image, item, 'next-content', options).catch((error) => {
      this.clearPreparedImage();
      this.logger.warn('slot', `slot ${this.slotIndex + 1} 다음 이미지 준비 실패: ${String(error)}`);
      throw error;
    });
    return this.preparedImagePromise;
  }

  private imageElementForPreparation(item: SeamlessContentItem): HTMLImageElement {
    const sourceUrl = resolveImageSourceUrl(item.sourceUrl);
    if (
      this.currentItem()?.contentType === 'Video'
      && this.currentImage.getAttribute('src') === sourceUrl
      && this.currentImage.complete
    ) {
      return this.currentImage;
    }

    return this.standbyImage;
  }

  private prepareNextImageForCurrentVideo(generation: number): void {
    if (!this.active || !this.isContentGenerationCurrent(generation) || !this.canAdvanceContent()) {
      return;
    }

    const item = this.currentItem();
    if (item?.contentType !== 'Video') {
      return;
    }

    const nextIndex = this.findNextPlayableIndex(this.itemIndex);
    const nextItem = nextIndex !== null ? this.slot.items[nextIndex] ?? null : null;
    if (nextItem?.contentType !== 'Image') {
      return;
    }

    void this.prepareImageContent(nextItem, { underVideo: true }).catch(() => {
      // 전환 경로에서 다시 준비하고 실패를 확정 로그로 남긴다.
    });
  }

  private prepareVideoContentForSlotPlan(item: SeamlessContentItem, itemIndex: number, slot: SeamlessSlotPlan): Promise<void> {
    if (this.preparedItemId === item.id && this.preparePromise) {
      return this.preparePromise;
    }

    this.clearPreparedContent();
    this.preparedItemIndex = itemIndex;
    this.preparedItemId = item.id;
    this.preparePromise = this.prepareVideoContent(item, slot).catch((error) => {
      this.clearPreparedContent();
      this.logger.warn('slot', `slot ${this.slotIndex + 1} 다음 영상 준비 실패: ${String(error)}`);
      throw error;
    });
    return this.preparePromise;
  }

  private async prepareVideoContent(item: SeamlessContentItem, slot: SeamlessSlotPlan): Promise<void> {
    const existingSession = this.videoSession;
    const session = existingSession ?? this.getVideoSession();
    this.videoSession = session;
    try {
      const playbackInfo = await session.prepare(item, slot, this.element, this.preserveAspectRatio);
      this.recordVideoDuration(item, playbackInfo?.durationMs ?? null);
    } catch (error) {
      if (!existingSession && this.videoSession === session) {
        this.releaseCurrentVideoSession();
      }
      throw error;
    }
  }

  private recordVideoDuration(item: SeamlessContentItem, durationMs: number | null): void {
    if (durationMs === null || !Number.isFinite(durationMs) || durationMs <= 0) {
      return;
    }

    this.videoDurationMsByItemId.set(item.id, Math.round(durationMs));
  }

  private videoTransitionMode(item: SeamlessContentItem): VideoTransitionMode {
    const durationMs = this.videoDurationMsByItemId.get(item.id);
    if (durationMs === undefined) {
      return this.switchOnContentEnd || !item.transitionByTimer ? 'event' : 'timer';
    }

    const displayMs = Math.max(1, item.durationSeconds) * 1000;
    if (displayMs < durationMs - VIDEO_DURATION_MATCH_TOLERANCE_MS) {
      return 'timer';
    }

    const remainderMs = displayMs % durationMs;
    return remainderMs <= VIDEO_DURATION_MATCH_TOLERANCE_MS || durationMs - remainderMs <= VIDEO_DURATION_MATCH_TOLERANCE_MS
      ? 'event'
      : 'timer';
  }

  private updateCurrentVideoLoopState(item: SeamlessContentItem, force = false): void {
    if (item.contentType !== 'Video' || !this.videoSession) {
      return;
    }

    const shouldLoop = this.shouldLoopCurrentVideo(item);
    if (!force && this.currentVideoLoopState === shouldLoop) {
      return;
    }

    this.videoSession.setLooping?.(shouldLoop);
    this.currentVideoLoopState = shouldLoop;
  }

  private shouldLoopCurrentVideo(item: SeamlessContentItem): boolean {
    if (this.loopCurrentPageAtPageEnd && !this.canAdvanceContent()) {
      return true;
    }

    const durationMs = this.videoDurationMsByItemId.get(item.id);
    if (durationMs === undefined) {
      return item.shouldLoop;
    }

    const displayMs = Math.max(1, item.durationSeconds) * 1000;
    if (displayMs <= durationMs + VIDEO_DURATION_MATCH_TOLERANCE_MS) {
      return false;
    }

    if (this.videoTransitionMode(item) === 'timer') {
      return true;
    }

    const elapsedMs = this.currentItemTimelineElapsedMilliseconds(item);
    const remainingMs = Math.max(0, displayMs - elapsedMs);
    return remainingMs > durationMs + VIDEO_DURATION_MATCH_TOLERANCE_MS;
  }

  private isVideoCompletionEligibleForTransition(item: SeamlessContentItem): boolean {
    const durationMs = this.videoDurationMsByItemId.get(item.id);
    if (durationMs === undefined) {
      return true;
    }

    const displayMs = Math.max(1, item.durationSeconds) * 1000;
    if (displayMs <= durationMs + VIDEO_DURATION_MATCH_TOLERANCE_MS) {
      return true;
    }

    const expectedCompletionCount = this.expectedVideoCompletionCount(item, durationMs);
    if (expectedCompletionCount > 0 && this.currentVideoCompletionCount >= expectedCompletionCount) {
      return true;
    }

    return this.currentItemTimelineElapsedMilliseconds(item) >= displayMs - VIDEO_DURATION_MATCH_TOLERANCE_MS;
  }

  private expectedVideoCompletionCount(item: SeamlessContentItem, durationMs: number): number {
    if (this.videoTransitionMode(item) !== 'event') {
      return 0;
    }

    const displayMs = Math.max(1, item.durationSeconds) * 1000;
    return Math.max(1, Math.round(displayMs / durationMs));
  }

  private clearPreparedContent(): void {
    this.preparedItemIndex = null;
    this.preparedItemId = null;
    this.preparePromise = null;
    this.videoSession?.clearPrepared?.();
    if (this.currentItem()?.contentType !== 'Video') {
      this.releaseCurrentVideoSession();
    }
  }

  private clearPreparedImage(itemId?: string): void {
    if (itemId && this.preparedImageId !== itemId) {
      return;
    }

    const image = this.preparedImageElement ?? this.standbyImage;
    this.preparedImageId = null;
    this.preparedImagePromise = null;
    this.preparedImageElement = null;
    image.classList.remove('slot-image--visible');
    image.classList.remove('slot-image--prepared');
    image.classList.remove('slot-image--under-video');
    image.style.zIndex = '0';
  }

  private consumePreparedImage(itemId: string): void {
    if (this.preparedImageId !== itemId) {
      return;
    }

    this.preparedImageId = null;
    this.preparedImagePromise = null;
    this.preparedImageElement = null;
  }

  private waitForPaint(): Promise<void> {
    if (/jsdom/i.test(window.navigator.userAgent)) {
      return Promise.resolve();
    }

    return new Promise((resolve) => {
      window.requestAnimationFrame(() => {
        window.requestAnimationFrame(() => resolve());
      });
    });
  }

  private elapsed(startedAt: number): number {
    return Math.round(performance.now() - startedAt);
  }

  private logImageTiming(item: SeamlessContentItem, stage: string, startedAt: number, detail = ''): void {
    const suffix = detail ? ` ${detail}` : '';
    this.logger.info('image-timing', `slot ${this.slotIndex + 1} ${stage}: ${item.name} +${this.elapsed(startedAt)}ms${suffix}`);
  }

  private startImageMainThreadProbe(item: SeamlessContentItem, stage: string, startedAt: number): () => void {
    if (/jsdom/i.test(window.navigator.userAgent)) {
      return () => undefined;
    }

    let active = true;
    let lastFrameAt = performance.now();
    let animationFrameId = 0;
    const tick = (now: number) => {
      if (!active) {
        return;
      }

      const gapMs = now - lastFrameAt;
      if (gapMs >= IMAGE_MAIN_THREAD_GAP_WARN_MS) {
        this.logger.warn(
          'image-timing',
          `slot ${this.slotIndex + 1} ui-thread gap during ${stage}: ${item.name} gap=${Math.round(gapMs)}ms total=${this.elapsed(startedAt)}ms`,
        );
      }
      lastFrameAt = now;
      animationFrameId = window.requestAnimationFrame(tick);
    };

    animationFrameId = window.requestAnimationFrame(tick);
    return () => {
      active = false;
      if (animationFrameId !== 0) {
        window.cancelAnimationFrame(animationFrameId);
      }
    };
  }

  private hasPreparedUpcomingVideo(): boolean {
    if (this.preparedItemId === null) {
      return false;
    }

    const nextIndex = this.canAdvanceContent() ? this.findNextPlayableIndex(this.itemIndex) : null;
    const nextItem = nextIndex !== null ? this.slot.items[nextIndex] ?? null : null;
    return nextItem?.contentType === 'Video' && nextItem.id === this.preparedItemId;
  }

  private hideCurrentVideoSurface(keepPrepared: boolean): void {
    const session = this.videoSession;
    if (!session) {
      return;
    }

    this.currentVideoLoopState = null;
    if (keepPrepared) {
      void (session.hideCurrentKeepPrepared?.() ?? Promise.resolve());
      return;
    }

    session.hide?.();
  }

  private detachCurrentVideoSurfaceForTransition(): boolean {
    const session = this.videoSession;
    if (!session) {
      return false;
    }

    return session.detachCurrentSurfaceForTransition?.() ?? false;
  }

  private releaseCurrentVideoSession(options: {
    readonly deferStopUntilNextFrame?: boolean;
    readonly keepPrepared?: boolean;
    readonly alreadyHidden?: boolean;
  } = {}): Promise<void> {
    if (!this.videoSession) {
      return Promise.resolve();
    }

    const session = this.videoSession;
    if (options.keepPrepared === true) {
      this.currentVideoLoopState = null;
      if (options.alreadyHidden === true) {
        if (options.deferStopUntilNextFrame === true) {
          return this.afterNextFrame(() => session.stopDetachedSurface?.(true));
        }

        session.stopDetachedSurface?.(true);
        return Promise.resolve();
      }

      return session.hideCurrentKeepPrepared?.() ?? Promise.resolve();
    }

    this.videoSession = null;
    this.currentVideoLoopState = null;
    if (options.deferStopUntilNextFrame === true) {
      if (options.alreadyHidden !== true) {
        session.hide?.();
      }
      return this.afterNextFrame(() => {
        session.stop();
        this.releaseVideoSession(session);
      });
    }

    session.stop();
    this.releaseVideoSession(session);
    return Promise.resolve();
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

  private currentItemTimelineElapsedMilliseconds(item: SeamlessContentItem): number {
    const durationMs = Math.max(1, item.durationSeconds) * 1000;
    const elapsedMs = this.currentItemRawElapsedMilliseconds();
    if (!this.canAdvanceContent()) {
      return elapsedMs % durationMs;
    }

    return Math.min(elapsedMs, durationMs);
  }

  private currentItemRawElapsedMilliseconds(): number {
    if (this.itemStartedAt < 0) {
      return Math.max(0, this.itemPausedElapsedMs);
    }

    return Math.max(0, performance.now() - this.itemStartedAt);
  }

  private hasCurrentItemDurationEnded(item: SeamlessContentItem): boolean {
    const durationMs = Math.max(1, item.durationSeconds) * 1000;
    return this.currentItemRawElapsedMilliseconds() >= durationMs;
  }

  private isPageTimelineExpired(): boolean {
    return Number.isFinite(this.pageDurationMs) && this.pageElapsedMs >= this.pageDurationMs;
  }

  private formatSeconds(milliseconds: number): string {
    return `${(Math.max(0, milliseconds) / 1000).toFixed(1)}s`;
  }

  private resetItemClock(): void {
    this.itemStartedAt = -1;
    this.itemPausedElapsedMs = 0;
  }

  private canAdvanceContent(): boolean {
    return this.playableItemCount() > 1;
  }

  private nextContentGeneration(): number {
    this.contentGeneration += 1;
    return this.contentGeneration;
  }

  private isContentGenerationCurrent(generation: number): boolean {
    return this.contentGeneration === generation;
  }

  private resolvePreparedItemIndex(): number | null {
    const item = this.currentItem();
    if (!item) {
      return null;
    }

    if (this.loopCurrentPageAtPageEnd && this.itemIndex === 0) {
      return null;
    }

    const nextIndex = this.findNextPlayableIndex(this.itemIndex);
    if (nextIndex === null) {
      return null;
    }

    const nextItem = this.slot.items[nextIndex] ?? null;
    const shouldPrepareNextImageForEventVideo = item.contentType === 'Video'
      && nextItem?.contentType === 'Image'
      && this.shouldWaitForVideoEnd(item);
    if (!shouldPrepareNextImageForEventVideo) {
      const pageRemainingMs = this.pageDurationMs - this.pageElapsedMs;
      const itemDurationMs = Math.max(1, item.durationSeconds) * 1000;
      const itemRemainingMs = itemDurationMs - this.currentItemTimelineElapsedMilliseconds(item);
      if (Number.isFinite(pageRemainingMs) && pageRemainingMs <= itemRemainingMs) {
        return this.loopCurrentPageAtPageEnd ? 0 : null;
      }
    }

    return nextIndex;
  }

  private resolveTimelineItem(pageElapsedMs: number): { itemIndex: number; itemElapsedMs: number } | null {
    const playableItems = this.slot.items
      .map((item, itemIndex) => ({ item, itemIndex }))
      .filter(({ item }) => this.isItemPlayable(item));
    const durations = playableItems.map(({ item }) => Math.max(1, item.durationSeconds) * 1000);
    const totalDurationMs = durations.reduce((sum, durationMs) => sum + durationMs, 0);
    if (totalDurationMs <= 0) {
      return null;
    }

    const cycleElapsedMs = Math.max(0, pageElapsedMs) % totalDurationMs;
    let cursorMs = 0;
    for (let index = 0; index < durations.length; index += 1) {
      const durationMs = durations[index]!;
      const nextCursorMs = cursorMs + durationMs;
      if (cycleElapsedMs < nextCursorMs) {
        return {
          itemIndex: playableItems[index]!.itemIndex,
          itemElapsedMs: cycleElapsedMs - cursorMs,
        };
      }
      cursorMs = nextCursorMs;
    }

    return {
      itemIndex: playableItems[playableItems.length - 1]!.itemIndex,
      itemElapsedMs: durations[durations.length - 1] ?? 0,
    };
  }

  private isItemPlayable(item: SeamlessContentItem): boolean {
    return this.isContentPlayable(item);
  }

  private hasPlayableItems(slot: SeamlessSlotPlan = this.slot): boolean {
    return this.firstPlayableIndex(slot) !== null;
  }

  private playableItemCount(slot: SeamlessSlotPlan = this.slot): number {
    return slot.items.reduce((count, item) => count + (this.isItemPlayable(item) ? 1 : 0), 0);
  }

  private firstPlayableIndex(slot: SeamlessSlotPlan = this.slot): number | null {
    for (let index = 0; index < slot.items.length; index += 1) {
      const item = slot.items[index];
      if (item && this.isItemPlayable(item)) {
        return index;
      }
    }

    return null;
  }

  private findNextPlayableIndex(currentIndex: number, slot: SeamlessSlotPlan = this.slot): number | null {
    if (slot.items.length === 0) {
      return null;
    }

    for (let offset = 1; offset <= slot.items.length; offset += 1) {
      const index = (currentIndex + offset) % slot.items.length;
      const item = slot.items[index];
      if (item && this.isItemPlayable(item)) {
        return index;
      }
    }

    return null;
  }

}
