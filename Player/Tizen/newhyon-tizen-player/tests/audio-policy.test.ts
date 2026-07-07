import { describe, expect, it, vi } from 'vitest';
import { TizenAudioPolicy, resolvePageAudioVolume, shouldMutePageAudio } from '../src/player/audio-policy';
import type { SeamlessPagePlan } from '../src/domain/page-plan';
import { RingLogger } from '../src/core/logger';

function createPage(isMuted: boolean): SeamlessPagePlan {
  return {
    playlistName: 'playlist',
    pageName: 'page',
    canvasWidth: 1920,
    canvasHeight: 1080,
    durationSeconds: 10,
    volume: 35,
    hasExplicitVolume: false,
    slots: [
      {
        elementName: 'video',
        isMuted,
        width: 1920,
        height: 1080,
        left: 0,
        top: 0,
        zIndex: 0,
        items: [
          {
            source: {
              CIF_FileName: 'video.mp4',
              CIF_ContentType: 'Video',
            },
            id: 'video.mp4',
            name: 'video.mp4',
            sourceUrl: 'video.mp4',
            contentType: 'Video',
            durationSeconds: 10,
            actualDurationSeconds: 10,
            shouldLoop: false,
            transitionByTimer: true,
            loopDisableAfterEndCount: 0,
            transitionEndEventCount: 0,
          },
        ],
      },
    ],
  };
}

describe('TizenAudioPolicy', () => {
  it('unmuted 영상 슬롯 여부만 판별하고 전역 볼륨값은 재생 제어에 쓰지 않는다', () => {
    expect(shouldMutePageAudio(createPage(true))).toBe(true);
    expect(shouldMutePageAudio(createPage(false))).toBe(false);
    expect(resolvePageAudioVolume(createPage(true))).toBe(0);
    expect(resolvePageAudioVolume(createPage(false))).toBe(100);
    expect(resolvePageAudioVolume({ ...createPage(false), volume: 0, hasExplicitVolume: true })).toBe(100);
  });

  it('페이지 오디오 정책은 TV audio volume을 변경하지 않는다', () => {
    const setMute = vi.fn();
    const setVolume = vi.fn();
    window.tizen = {
      tvaudiocontrol: {
        isMute: vi.fn(() => true),
        setMute,
        getVolume: vi.fn(() => 12),
        setVolume,
      },
    };

    const policy = new TizenAudioPolicy(new RingLogger(5));
    policy.applyForPage(createPage(true));
    policy.applyForPage(createPage(true));
    policy.applyForPage(createPage(false));
    policy.restore();

    expect(setVolume).not.toHaveBeenCalled();
    expect(setMute).not.toHaveBeenCalled();
  });
});
