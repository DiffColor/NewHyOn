import type { RingLogger } from '../core/logger';
import type { SeamlessPagePlan } from '../domain/page-plan';

type AudioLogger = Pick<RingLogger, 'info' | 'warn'>;

function formatAudioError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  return String(error);
}

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
  private lastAppliedMute: boolean | null = null;

  constructor(private readonly logger: AudioLogger) {}

  applyForPage(page: SeamlessPagePlan): void {
    const targetMute = shouldMutePageAudio(page);
    if (this.lastAppliedMute !== targetMute) {
      this.lastAppliedMute = targetMute;
      this.logger.info('audio', `page audio mute policy=${targetMute} (system mute unchanged)`);
    }
  }

  restore(): void {
    this.lastAppliedMute = null;
  }
}
