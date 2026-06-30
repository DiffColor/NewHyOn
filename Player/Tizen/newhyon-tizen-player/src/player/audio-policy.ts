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
      if (!this.applyAudioMute(targetMute, 'page')) {
        return;
      }
      this.lastAppliedMute = targetMute;
      this.logger.info('audio', `page audio mute policy=${targetMute}`);
    }
  }

  restore(): void {
    if (this.lastAppliedMute === true) {
      this.applyAudioMute(false, 'restore');
    }
    this.lastAppliedMute = null;
  }

  private applyAudioMute(muted: boolean, source: string): boolean {
    const audioControl = window.tizen?.tvaudiocontrol;
    if (typeof audioControl?.setMute !== 'function') {
      this.logger.warn('audio', `tvaudiocontrol.setMute unavailable (${source}, muted=${muted})`);
      return false;
    }

    try {
      audioControl.setMute(muted);
      return true;
    } catch (error) {
      this.logger.warn('audio', `tvaudiocontrol.setMute failed (${source}, muted=${muted}): ${formatAudioError(error)}`);
      return false;
    }
  }
}
