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

export function resolvePageAudioVolume(page: SeamlessPagePlan): number {
  return shouldMutePageAudio(page) ? 0 : page.volume;
}

export class TizenAudioPolicy {
  private lastAppliedVolume: number | null = null;
  private originalVolume: number | null = null;
  private originalMute: boolean | null = null;

  constructor(private readonly logger: AudioLogger) {}

  applyForPage(page: SeamlessPagePlan): void {
    const targetVolume = resolvePageAudioVolume(page);
    if (this.lastAppliedVolume !== targetVolume) {
      if (!this.applyAudioVolume(targetVolume, 'page')) {
        return;
      }
      this.lastAppliedVolume = targetVolume;
      this.logger.info('audio', `page audio volume=${targetVolume}`);
    }
  }

  restore(): void {
    if (this.originalVolume !== null) {
      this.applyAudioVolume(this.originalVolume, 'restore', { restoreMute: true });
    }
    this.lastAppliedVolume = null;
    this.originalVolume = null;
    this.originalMute = null;
  }

  private applyAudioVolume(volume: number, source: string, options: { readonly restoreMute?: boolean } = {}): boolean {
    const audioControl = window.tizen?.tvaudiocontrol;
    if (typeof audioControl?.setVolume !== 'function') {
      this.logger.warn('audio', `tvaudiocontrol.setVolume unavailable (${source}, volume=${volume})`);
      return false;
    }

    try {
      if (this.originalVolume === null && typeof audioControl.getVolume === 'function') {
        this.originalVolume = audioControl.getVolume();
      }
      if (this.originalMute === null && typeof audioControl.isMute === 'function') {
        this.originalMute = audioControl.isMute();
      }
      if (volume > 0 && options.restoreMute !== true && typeof audioControl.setMute === 'function') {
        audioControl.setMute(false);
      }
      audioControl.setVolume(volume);
      if (options.restoreMute === true && this.originalMute !== null && typeof audioControl.setMute === 'function') {
        audioControl.setMute(this.originalMute);
      }
      return true;
    } catch (error) {
      this.logger.warn('audio', `tvaudiocontrol.setVolume failed (${source}, volume=${volume}): ${formatAudioError(error)}`);
      return false;
    }
  }
}
