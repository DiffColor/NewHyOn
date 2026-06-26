import type { RingLogger } from '../core/logger';
import type { SeamlessContentItem, SeamlessSlotPlan } from '../domain/page-plan';
import type { AvplaySession } from './avplay-session';
import { resolveImageSourceUrl } from './source-resolver';

type ContentShownHandler = (slotIndex: number, item: SeamlessContentItem) => void;

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
  private preparePromise: Promise<void> | null = null;
  private layoutCanvasWidth = 0;
  private layoutCanvasHeight = 0;
  private pageElapsedMs = 0;
  private pageDurationMs = Number.POSITIVE_INFINITY;
  private loopCurrentPageAtPageEnd = false;
  private contentEndReachedAtPageBoundary = false;

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
    this.itemIndex = 0;
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
    this.slot = slot;
    this.applyLayout(canvasWidth, canvasHeight);
    this.clearPreparedItem();
    this.itemIndex = 0;
    this.failureMessage = null;
    if (slot.items.length === 0 || slot.width <= 0 || slot.height <= 0) {
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
  }

  stop(): void {
    this.active = false;
    this.resetItemClock();
    this.clearPreparedItem();
    this.releaseCurrentVideoSession();
    this.currentImage.removeAttribute('src');
    this.standbyImage.removeAttribute('src');
    this.currentImage.classList.remove('slot-image--visible');
    this.standbyImage.classList.remove('slot-image--visible');
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
    void this.prepareNextItem();
  }

  async restartFromBeginning(): Promise<void> {
    if (!this.active || this.slot.items.length === 0) {
      return;
    }

    this.resetItemClock();
    this.clearPreparedItem();
    if (!this.canAdvanceContent()) {
      return;
    }

    this.itemIndex = 0;
    await this.showCurrentItem();
  }

  applyDisplayRect(): void {
    const item = this.currentItem();
    if (item?.contentType === 'Video') {
      this.videoSession?.applyDisplayRect(this.slot, this.element);
    }
  }

  blocksPageTransitionForContentEnd(): boolean {
    const item = this.currentItem();
    if (!this.active || !item || !this.switchOnContentEnd) {
      return false;
    }

    if (item.contentType === 'Image') {
      return !this.hasCurrentItemDurationEnded(item);
    }

    return this.shouldWaitForVideoEnd(item) && !this.contentEndReachedAtPageBoundary;
  }

  private applySlotVisibility(): void {
    this.element.classList.toggle('slot--empty', this.slot.items.length === 0 || this.slot.width <= 0 || this.slot.height <= 0);
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
    const nextItemIndex = this.canAdvanceContent() ? (this.itemIndex + 1) % this.slot.items.length : null;
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
    if (!this.active || this.slot.items.length <= 1 || this.switchingItem || !this.usesTimerTimeline()) {
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
    const item = this.currentItem();
    if (!item || !this.active) {
      return false;
    }

    this.switchingItem = true;
    this.resetItemClock();
    this.contentEndReachedAtPageBoundary = false;
    try {
      if (item.contentType === 'Image') {
        await this.showImage(item);
        this.element.classList.remove('slot--video-active');
        this.releaseCurrentVideoSession();
      } else {
        this.element.classList.add('slot--video-active');
        const shouldWaitForFirstFrame = this.waitForVideoFirstFrame || this.canAdvanceContent();
        const nextVideoSession = this.getVideoSession();
        await nextVideoSession.play(item, this.slot, this.element, this.preserveAspectRatio, () => {
          void this.handleVideoEnded();
        }, {
          waitForFirstFrame: shouldWaitForFirstFrame,
        });
        this.videoSession = nextVideoSession;
        this.hideImages();
        if (this.preparedItemIndex === this.itemIndex) {
          this.clearPreparedItem();
        }
      }
      this.onContentShown(this.slotIndex, item);
      void this.prepareNextItem();
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
      && (this.switchOnContentEnd || !item.transitionByTimer);
  }

  private shouldScheduleTimer(item: SeamlessContentItem): boolean {
    return this.canAdvanceContent() && !this.shouldWaitForVideoEnd(item);
  }

  private async handleVideoEnded(): Promise<void> {
    const item = this.currentItem();
    if (!this.active || !item || !this.shouldWaitForVideoEnd(item)) {
      return;
    }

    if (this.isPageTimelineExpired() && !this.loopCurrentPageAtPageEnd) {
      this.contentEndReachedAtPageBoundary = true;
      return;
    }

    await this.advance();
  }

  private suspendAfterFailure(error: unknown): void {
    this.active = false;
    this.resetItemClock();
    this.clearPreparedItem();
    this.releaseCurrentVideoSession();
    this.hideImages();
    this.element.classList.remove('slot--video-active');
    this.element.classList.add('slot--empty');
    this.failureMessage = error instanceof Error ? error.message : String(error);
    this.logger.error('slot', `slot ${this.slotIndex + 1} 중단: ${this.failureMessage}`);
  }

  private async showImage(item: SeamlessContentItem): Promise<void> {
    const image = this.standbyImage;
    const previousImage = this.currentImage;
    this.applyImageDisplayMode();

    if (this.preparedItemIndex === this.itemIndex && this.preparePromise) {
      await this.preparePromise;
    } else {
      await this.prepareImageElement(image, item);
    }

    previousImage.style.zIndex = '1';
    image.style.zIndex = '2';
    image.classList.add('slot-image--visible');
    previousImage.classList.remove('slot-image--visible');
    previousImage.style.zIndex = '0';
    image.style.zIndex = '1';
    [this.currentImage, this.standbyImage] = [this.standbyImage, this.currentImage];
    this.clearPreparedItem();
    this.logger.info('slot', `slot ${this.slotIndex + 1} image: ${item.name}`);
  }

  private async prepareImageElement(image: HTMLImageElement, item: SeamlessContentItem): Promise<void> {
    const sourceUrl = resolveImageSourceUrl(item.sourceUrl);
    this.applyImageDisplayMode();

    if (image.getAttribute('src') !== sourceUrl || !image.complete) {
      await new Promise<void>((resolve, reject) => {
        image.onload = () => resolve();
        image.onerror = () => reject(new Error(`이미지를 로드하지 못했습니다: ${item.name}`));
        image.src = sourceUrl;
      });
    }
    await image.decode?.();
  }

  private applyImageDisplayMode(): void {
    const objectFit = this.preserveAspectRatio ? 'contain' : 'fill';
    this.imageA.style.objectFit = objectFit;
    this.imageB.style.objectFit = objectFit;
  }

  private hideImages(): void {
    this.imageA.classList.remove('slot-image--visible');
    this.imageB.classList.remove('slot-image--visible');
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

    this.itemIndex = (this.itemIndex + 1) % this.slot.items.length;
    this.resetItemClock();
    await this.showCurrentItemAtElapsed(0);
  }

  private async prepareNextItem(): Promise<void> {
    if (!this.active || !this.canAdvanceContent()) {
      return;
    }

    const nextIndex = this.resolvePreparedItemIndex();
    if (nextIndex === null) {
      this.clearPreparedItem();
      return;
    }

    if (this.preparedItemIndex === nextIndex || this.preparePromise) {
      return;
    }

    const nextItem = this.slot.items[nextIndex];
    if (!nextItem || nextItem.contentType !== 'Image') {
      return;
    }

    this.preparedItemIndex = nextIndex;
    this.preparePromise = this.prepareImageElement(this.standbyImage, nextItem).catch((error) => {
      this.clearPreparedItem();
      this.logger.warn('slot', `slot ${this.slotIndex + 1} 다음 콘텐츠 준비 실패: ${String(error)}`);
      throw error;
    });

    try {
      await this.preparePromise;
    } catch {
      // The transition path will retry and surface a hard failure if the item still cannot start.
    }
  }

  private clearPreparedItem(): void {
    this.preparedItemIndex = null;
    this.preparePromise = null;
  }

  private releaseCurrentVideoSession(): void {
    if (!this.videoSession) {
      return;
    }

    const session = this.videoSession;
    session.stop();
    this.videoSession = null;
    this.releaseVideoSession(session);
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
    return this.slot.items.length > 1;
  }

  private resolvePreparedItemIndex(): number | null {
    const item = this.currentItem();
    if (!item) {
      return null;
    }

    const pageRemainingMs = this.pageDurationMs - this.pageElapsedMs;
    const itemDurationMs = Math.max(1, item.durationSeconds) * 1000;
    const itemRemainingMs = itemDurationMs - this.currentItemTimelineElapsedMilliseconds(item);
    if (Number.isFinite(pageRemainingMs) && pageRemainingMs <= itemRemainingMs) {
      return this.loopCurrentPageAtPageEnd ? 0 : null;
    }

    return (this.itemIndex + 1) % this.slot.items.length;
  }

  private usesTimerTimeline(): boolean {
    return this.slot.items.every((item) => !this.shouldWaitForVideoEnd(item));
  }

  private resolveTimelineItem(pageElapsedMs: number): { itemIndex: number; itemElapsedMs: number } | null {
    const durations = this.slot.items.map((item) => Math.max(1, item.durationSeconds) * 1000);
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
          itemIndex: index,
          itemElapsedMs: cycleElapsedMs - cursorMs,
        };
      }
      cursorMs = nextCursorMs;
    }

    return {
      itemIndex: this.slot.items.length - 1,
      itemElapsedMs: durations[durations.length - 1] ?? 0,
    };
  }

}
