import type { RingLogger } from '../core/logger';
import type { SeamlessContentItem, SeamlessPagePlan, SeamlessSlotPlan } from '../domain/page-plan';
import type {
  PairSchedulerLayerRole,
  PairSchedulerPlayer,
  PairSchedulerPlayerListener,
} from './pair-scheduler';
import { resolveAvplaySourceUrl, resolveImageSourceUrl } from './source-resolver';

const AVPLAY_BASE_WIDTH = 1920;
const AVPLAY_BASE_HEIGHT = 1080;
const STOPPABLE_STATES = new Set(['READY', 'PLAYING', 'PAUSED']);

export interface PairSchedulerMediaItem {
  readonly id: string;
  readonly title: string;
  readonly url: string;
  readonly pagePlan: SeamlessPagePlan;
  readonly pageIndex: number;
  readonly slotPlan: SeamlessSlotPlan;
  readonly slotIndex: number;
  readonly item: SeamlessContentItem;
  readonly itemIndex: number;
}

export class PairSchedulerMediaPlayer implements PairSchedulerPlayer {
  private readonly objectElement = document.createElement('object');
  private readonly imageElement = document.createElement('img');
  private listener: PairSchedulerPlayerListener | null = null;
  private currentItem: PairSchedulerMediaItem | null = null;
  private openedImageUrl = '';
  private displayMethod = 'PLAYER_DISPLAY_MODE_FULL_SCREEN';
  private role: PairSchedulerLayerRole = 'hidden';

  constructor(
    readonly key: string,
    private readonly player: AVPlayApi,
    private readonly host: HTMLElement,
    private registry: ReadonlyMap<string, PairSchedulerMediaItem>,
    private readonly logger: RingLogger,
  ) {
    this.objectElement.type = 'application/avplayer';
    this.objectElement.className = 'avplay-object pair-scheduler-avplay';
    this.objectElement.setAttribute('aria-hidden', 'true');
    this.objectElement.style.visibility = 'hidden';

    this.imageElement.className = 'pair-scheduler-image';
    this.imageElement.setAttribute('aria-hidden', 'true');
    this.imageElement.style.visibility = 'hidden';

    this.host.append(this.objectElement, this.imageElement);
  }

  setRegistry(registry: ReadonlyMap<string, PairSchedulerMediaItem>): void {
    this.registry = registry;
  }

  open(url: string): void {
    const item = this.registry.get(url);
    if (!item) {
      throw new Error(`pair-scheduler 미디어 아이템을 찾지 못했습니다: ${url}`);
    }

    this.currentItem = item;
    if (item.item.contentType === 'Image') {
      this.openedImageUrl = resolveImageSourceUrl(item.item.sourceUrl);
      this.imageElement.src = this.openedImageUrl;
      this.applyImageDisplayMethod();
      return;
    }

    this.player.open(resolveAvplaySourceUrl(item.item.sourceUrl));
  }

  close(): void {
    if (this.currentItem?.item.contentType === 'Video' && this.getState() !== 'NONE') {
      this.callVideoSafe('close', () => {
        this.player.close();
      });
    }
    this.currentItem = null;
    this.openedImageUrl = '';
    this.hideSurfaces();
  }

  play(): void {
    const item = this.requireCurrentItem();
    if (item.item.contentType === 'Image') {
      this.showImage();
      this.listener?.oncurrentplaytime(0);
      return;
    }

    this.player.play();
  }

  stop(): void {
    if (this.currentItem?.item.contentType !== 'Video') {
      this.hideSurfaces();
      return;
    }

    const state = this.getState();
    if (!STOPPABLE_STATES.has(state)) {
      return;
    }

    this.callVideoSafe('stop', () => {
      this.player.stop();
    });
  }

  prepareAsync(onSuccess: () => void, onError: (error: unknown) => void): void {
    const item = this.requireCurrentItem();
    if (item.item.contentType === 'Image') {
      this.prepareImage(onSuccess, onError);
      return;
    }

    try {
      this.player.prepareAsync(onSuccess, onError as (error: AVPlayErrorLike) => void);
    } catch (error) {
      onError(error);
    }
  }

  setDisplayRect(x: number, y: number, width: number, height: number): void {
    this.applySurfaceRect(x, y, width, height);
    if (this.currentItem?.item.contentType === 'Video') {
      this.callVideoSafe('setDisplayRect', () => {
        this.player.setDisplayRect(x, y, width, height);
      });
    }
  }

  setListener(listener: PairSchedulerPlayerListener): void {
    this.listener = listener;
    this.player.setListener({
      onbufferingstart: () => listener.onbufferingstart(),
      onbufferingcomplete: () => listener.onbufferingcomplete(),
      oncurrentplaytime: (currentTime) => listener.oncurrentplaytime(currentTime),
      onstreamcompleted: () => listener.onstreamcompleted(),
      onerror: (error) => listener.onerror(this.formatAvplayError(error, '')),
      onerrormsg: (error, message) => listener.onerrormsg(this.formatAvplayError(error, message), message),
    });
  }

  getState(): string {
    return (this.player.getState?.() ?? 'IDLE').toUpperCase();
  }

  setDisplayMethod(method: string): void {
    this.displayMethod = method;
    this.applyImageDisplayMethod();
    this.callVideoSafe('setDisplayMethod', () => {
      this.player.setDisplayMethod?.(method);
    });
  }

  setTimeoutForBuffering(seconds: number): void {
    this.callVideoSafe('setTimeoutForBuffering', () => {
      this.player.setTimeoutForBuffering?.(seconds);
    });
  }

  setStreamingProperty(property: string, value: string): void {
    this.callVideoSafe('setStreamingProperty', () => {
      this.player.setStreamingProperty?.(property, value);
    });
  }

  setVideoStillMode(enabled: string): void {
    this.callVideoSafe('setVideoStillMode', () => {
      this.player.setVideoStillMode?.(enabled);
    });
  }

  setLayerRole(role: PairSchedulerLayerRole): void {
    this.role = role;
    if (role === 'hidden') {
      this.hideSurfaces();
      return;
    }

    if (this.currentItem?.item.contentType === 'Image') {
      this.showImage();
    } else if (this.currentItem?.item.contentType === 'Video') {
      this.objectElement.style.visibility = 'visible';
      this.objectElement.setAttribute('aria-hidden', 'false');
      this.imageElement.style.visibility = 'hidden';
      this.imageElement.setAttribute('aria-hidden', 'true');
    }

    const zIndexByRole: Record<PairSchedulerLayerRole, string> = {
      hidden: '0',
      warming: '2',
      held: '3',
      current: '4',
    };
    this.objectElement.style.zIndex = zIndexByRole[role];
    this.imageElement.style.zIndex = zIndexByRole[role];
  }

  dispose(): void {
    this.stop();
    this.close();
    this.objectElement.remove();
    this.imageElement.remove();
  }

  private requireCurrentItem(): PairSchedulerMediaItem {
    if (!this.currentItem) {
      throw new Error(`pair-scheduler 플레이어가 열리지 않았습니다: ${this.key}`);
    }

    return this.currentItem;
  }

  private prepareImage(onSuccess: () => void, onError: (error: unknown) => void): void {
    if (this.imageElement.complete && this.imageElement.naturalWidth > 0) {
      onSuccess();
      return;
    }

    const previousOnLoad = this.imageElement.onload;
    const previousOnError = this.imageElement.onerror;
    let settled = false;
    const success = () => {
      if (settled) {
        return;
      }
      settled = true;
      if (typeof previousOnLoad === 'function') {
        previousOnLoad.call(this.imageElement, new Event('load'));
      }
      cleanup();
      onSuccess();
    };
    const failure = () => {
      if (settled) {
        return;
      }
      settled = true;
      if (typeof previousOnError === 'function') {
        previousOnError.call(this.imageElement, new Event('error'));
      }
      cleanup();
      onError(new Error(`이미지 로드 실패: ${this.openedImageUrl}`));
    };
    const cleanup = () => {
      this.imageElement.removeEventListener('load', success);
      this.imageElement.removeEventListener('error', failure);
      this.imageElement.onload = previousOnLoad;
      this.imageElement.onerror = previousOnError;
    };

    this.imageElement.onload = success;
    this.imageElement.onerror = failure;
    this.imageElement.addEventListener('load', success, { once: true });
    this.imageElement.addEventListener('error', failure, { once: true });
  }

  private showImage(): void {
    this.objectElement.style.visibility = 'hidden';
    this.objectElement.setAttribute('aria-hidden', 'true');
    this.imageElement.style.visibility = this.role === 'hidden' ? 'hidden' : 'visible';
    this.imageElement.setAttribute('aria-hidden', this.role === 'hidden' ? 'true' : 'false');
  }

  private hideSurfaces(): void {
    this.objectElement.style.visibility = 'hidden';
    this.objectElement.setAttribute('aria-hidden', 'true');
    this.imageElement.style.visibility = 'hidden';
    this.imageElement.setAttribute('aria-hidden', 'true');
  }

  private applySurfaceRect(x: number, y: number, width: number, height: number): void {
    const viewportWidth = Math.max(document.documentElement.clientWidth || window.innerWidth || AVPLAY_BASE_WIDTH, 1);
    const viewportHeight = Math.max(document.documentElement.clientHeight || window.innerHeight || AVPLAY_BASE_HEIGHT, 1);
    const leftPx = (x / AVPLAY_BASE_WIDTH) * viewportWidth;
    const topPx = (y / AVPLAY_BASE_HEIGHT) * viewportHeight;
    const widthPx = (width / AVPLAY_BASE_WIDTH) * viewportWidth;
    const heightPx = (height / AVPLAY_BASE_HEIGHT) * viewportHeight;
    for (const element of [this.objectElement, this.imageElement]) {
      element.style.left = `${leftPx}px`;
      element.style.top = `${topPx}px`;
      element.style.width = `${widthPx}px`;
      element.style.height = `${heightPx}px`;
    }
  }

  private applyImageDisplayMethod(): void {
    this.imageElement.style.objectFit = this.displayMethod === 'PLAYER_DISPLAY_MODE_LETTER_BOX' ? 'contain' : 'fill';
  }

  private callVideoSafe(operation: string, callback: () => void): void {
    try {
      callback();
    } catch (error) {
      this.logger.warn('pair-scheduler', `${this.key} ${operation} 실패: ${String(error)}`);
    }
  }

  private formatAvplayError(error: AVPlayErrorLike | undefined, message: string): string {
    return String(message || error?.message || error?.code || error?.name || 'unknown');
  }
}
