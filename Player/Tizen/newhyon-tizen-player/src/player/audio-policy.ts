import type { RingLogger } from '../core/logger';
import type { SeamlessPagePlan } from '../domain/page-plan';

type AudioLogger = Pick<RingLogger, 'info'>;

export function shouldMutePageAudio(page: SeamlessPagePlan): boolean {
  const hasUnmutedVideo = page.slots.some((slot) => {
    if (slot.isMuted) {
      return false;
    }

    return slot.items.some((item) => item.contentType === 'Video');
  });

  return !hasUnmutedVideo;
}

export class TizenAudioPolicy {
  private lastMuted: boolean | null = null;

  constructor(private readonly logger: AudioLogger) {}

  forgetLastApplied(): void {
    this.lastMuted = null;
  }

  applyForPage(page: SeamlessPagePlan): void {
    const muted = shouldMutePageAudio(page);
    if (this.lastMuted !== muted) {
      this.lastMuted = muted;
      this.logger.info('audio', `page audio stream=${muted ? 'muted' : 'enabled'}`);
    }
  }

  restore(): void {
    this.lastMuted = null;
  }
}
