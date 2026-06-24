import type { RingLogger } from '../core/logger';
import type { SeamlessContentItem, SeamlessSlotPlan } from '../domain/page-plan';
import type { AvplaySession } from './avplay-session';
import { resolveImageSourceUrl } from './source-resolver';

type ContentShownHandler = (slotIndex: number, item: SeamlessContentItem) => void;

export class SlotPlayer {
  private readonly imageA = document.createElement('img');
  private readonly imageB = document.createElement('img');
  private readonly videoMask = document.createElement('div');
  private currentImage: HTMLImageElement;
  private standbyImage: HTMLImageElement;
  private itemIndex = 0;
  private timerId: number | null = null;
  private itemStartedAt = 0;
  private itemPausedElapsedMs = 0;
  private itemTimerRunning = false;
  private active = false;
  private videoSession: AvplaySession | null = null;
  private failureMessage: string | null = null;
  private preparedItemIndex: number | null = null;
  private preparePromise: Promise<void> | null = null;

  constructor(
    private readonly slotIndex: number,
    private readonly element: HTMLElement,
    private readonly slot: SeamlessSlotPlan,
    private readonly preserveAspectRatio: boolean,
    private readonly switchOnContentEnd: boolean,
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

  async start(): Promise<boolean> {
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

  stop(): void {
    this.active = false;
    this.clearTimer();
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
    if (this.itemTimerRunning) {
      this.itemPausedElapsedMs = this.currentItemElapsedMilliseconds();
    }
    this.clearTimer();
    this.videoSession?.pause();
  }

  resume(): void {
    if (!this.active) {
      return;
    }

    this.videoSession?.resume();
    const item = this.currentItem();
    if (item && this.shouldScheduleTimer(item)) {
      this.scheduleAdvance(item.durationSeconds, this.itemPausedElapsedMs);
    }
    void this.prepareNextItem();
  }

  async restartFromBeginning(): Promise<void> {
    if (!this.active || this.slot.items.length === 0) {
      return;
    }

    this.clearTimer();
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

  private currentItem(): SeamlessContentItem | null {
    return this.slot.items[this.itemIndex] ?? null;
  }

  private async showCurrentItem(): Promise<boolean> {
    const item = this.currentItem();
    if (!item || !this.active) {
      return false;
    }

    this.clearTimer();
    this.resetItemClock();
    try {
      if (item.contentType === 'Image') {
        this.element.classList.remove('slot--video-active');
        this.releaseCurrentVideoSession();
        await this.showImage(item);
      } else {
        this.element.classList.add('slot--video-active');
        this.hideImages();
        const shouldWaitForFirstFrame = this.waitForVideoFirstFrame || this.canAdvanceContent();
        this.videoSession = this.getVideoSession();
        await this.videoSession.play(item, this.slot, this.element, this.preserveAspectRatio, () => {
          void this.handleVideoEnded();
        }, {
          waitForFirstFrame: shouldWaitForFirstFrame,
        });
        if (this.preparedItemIndex === this.itemIndex) {
          this.clearPreparedItem();
        }
      }
      this.onContentShown(this.slotIndex, item);
      void this.prepareNextItem();
    } catch (error) {
      this.suspendAfterFailure(error);
      return false;
    }

    if (!this.shouldScheduleTimer(item)) {
      return true;
    }

    this.scheduleAdvance(item.durationSeconds);
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

    this.clearTimer();
    await this.advance();
  }

  private suspendAfterFailure(error: unknown): void {
    this.active = false;
    this.clearTimer();
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
    image.style.objectFit = this.preserveAspectRatio ? 'contain' : 'fill';

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
    image.style.objectFit = this.preserveAspectRatio ? 'contain' : 'fill';

    if (image.getAttribute('src') !== sourceUrl || !image.complete) {
      await new Promise<void>((resolve, reject) => {
        image.onload = () => resolve();
        image.onerror = () => reject(new Error(`이미지를 로드하지 못했습니다: ${item.name}`));
        image.src = sourceUrl;
      });
    }
    await image.decode?.();
  }

  private hideImages(): void {
    this.imageA.classList.remove('slot-image--visible');
    this.imageB.classList.remove('slot-image--visible');
  }

  private scheduleAdvance(durationSeconds: number, elapsedMs = 0): void {
    this.clearTimer();
    const durationMs = Math.max(1, durationSeconds) * 1000;
    const safeElapsedMs = Math.min(Math.max(0, elapsedMs), durationMs);
    const remainingMs = Math.max(0, durationMs - safeElapsedMs);
    this.itemPausedElapsedMs = safeElapsedMs;
    this.itemStartedAt = performance.now() - safeElapsedMs;
    this.itemTimerRunning = true;
    this.timerId = window.setTimeout(() => {
      void this.advance();
    }, remainingMs);
  }

  private async advance(): Promise<void> {
    if (!this.active || !this.canAdvanceContent()) {
      return;
    }

    this.itemIndex = (this.itemIndex + 1) % this.slot.items.length;
    this.resetItemClock();
    await this.showCurrentItem();
  }

  private async prepareNextItem(): Promise<void> {
    if (!this.active || !this.canAdvanceContent()) {
      return;
    }

    const nextIndex = (this.itemIndex + 1) % this.slot.items.length;
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

  private clearTimer(): void {
    if (this.timerId !== null) {
      window.clearTimeout(this.timerId);
      this.timerId = null;
    }
    this.itemTimerRunning = false;
  }

  private currentItemElapsedMilliseconds(): number {
    const item = this.currentItem();
    const durationMs = Math.max(1, item?.durationSeconds ?? 1) * 1000;
    if (!this.itemTimerRunning) {
      return Math.min(this.itemPausedElapsedMs, durationMs);
    }

    return Math.min(Math.max(0, performance.now() - this.itemStartedAt), durationMs);
  }

  private resetItemClock(): void {
    this.itemStartedAt = 0;
    this.itemPausedElapsedMs = 0;
    this.itemTimerRunning = false;
  }

  private canAdvanceContent(): boolean {
    return this.slot.items.length > 1;
  }

}
