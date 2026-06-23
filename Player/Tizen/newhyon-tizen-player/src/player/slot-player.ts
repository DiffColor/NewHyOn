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
    this.videoSession?.stop();
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
  }

  async restartFromBeginning(): Promise<void> {
    if (!this.active || this.slot.items.length === 0) {
      return;
    }

    this.clearTimer();
    this.resetItemClock();
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

    const state = this.videoSession?.state() ?? 'NO_VIDEO_SESSION';
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
        this.videoSession?.stop();
        await this.showImage(item);
      } else {
        this.element.classList.add('slot--video-active');
        this.hideImages();
        this.videoSession = this.getVideoSession();
        await this.videoSession.play(item, this.slot, this.element, this.preserveAspectRatio, () => {
          void this.handleVideoEnded();
        }, {
          waitForFirstFrame: this.waitForVideoFirstFrame,
        });
      }
      this.onContentShown(this.slotIndex, item);
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
    this.videoSession?.stop();
    this.hideImages();
    this.element.classList.remove('slot--video-active');
    this.element.classList.add('slot--empty');
    this.failureMessage = error instanceof Error ? error.message : String(error);
    this.logger.error('slot', `slot ${this.slotIndex + 1} 중단: ${this.failureMessage}`);
  }

  private async showImage(item: SeamlessContentItem): Promise<void> {
    const image = this.standbyImage;
    const previousImage = this.currentImage;
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

    previousImage.style.zIndex = '1';
    image.style.zIndex = '2';
    image.classList.add('slot-image--visible');
    await this.waitForPaint();
    previousImage.classList.remove('slot-image--visible');
    previousImage.style.zIndex = '0';
    image.style.zIndex = '1';
    [this.currentImage, this.standbyImage] = [this.standbyImage, this.currentImage];
    this.logger.info('slot', `slot ${this.slotIndex + 1} image: ${item.name}`);
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

  private waitForPaint(): Promise<void> {
    return new Promise((resolve) => {
      window.requestAnimationFrame(() => {
        window.requestAnimationFrame(() => resolve());
      });
    });
  }
}
