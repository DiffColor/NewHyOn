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
  it('unmuted 영상 슬롯이 없으면 페이지 오디오를 mute 처리한다', () => {
    expect(shouldMutePageAudio(createPage(true))).toBe(true);
    expect(shouldMutePageAudio(createPage(false))).toBe(false);
    expect(resolvePageAudioVolume(createPage(true))).toBe(0);
    expect(resolvePageAudioVolume(createPage(false), 55)).toBe(55);
    expect(resolvePageAudioVolume({ ...createPage(false), volume: 0, hasExplicitVolume: true }, 55)).toBe(55);
  });

  it('페이지 오디오 정책을 TV audio volume에 적용한다', () => {
    let currentVolume = 12;
    let currentMute = true;
    const setMute = vi.fn();
    const setVolume = vi.fn((volume: number) => {
      currentVolume = volume;
    });
    window.tizen = {
      tvaudiocontrol: {
        isMute: vi.fn(() => currentMute),
        setMute: vi.fn((muted: boolean) => {
          currentMute = muted;
          setMute(muted);
        }),
        getVolume: vi.fn(() => currentVolume),
        setVolume,
      },
    };

    const policy = new TizenAudioPolicy(new RingLogger(5));
    policy.applyForPage(createPage(true));
    policy.applyForPage(createPage(true));
    policy.applyForPage(createPage(false), 55);
    policy.restore();

    expect(setVolume).toHaveBeenNthCalledWith(1, 0);
    expect(setVolume).toHaveBeenNthCalledWith(2, 55);
    expect(setVolume).toHaveBeenNthCalledWith(3, 12);
    expect(setMute).toHaveBeenNthCalledWith(1, false);
    expect(setMute).toHaveBeenNthCalledWith(2, true);
  });

  it('restore 시 이전 TV audio volume을 복구한다', () => {
    let currentVolume = 22;
    const setVolume = vi.fn((volume: number) => {
      currentVolume = volume;
    });
    window.tizen = {
      tvaudiocontrol: {
        getVolume: vi.fn(() => currentVolume),
        setVolume,
      },
    };

    const policy = new TizenAudioPolicy(new RingLogger(5));
    policy.applyForPage(createPage(true));
    policy.restore();

    expect(setVolume).toHaveBeenNthCalledWith(1, 0);
    expect(setVolume).toHaveBeenNthCalledWith(2, 22);
  });
});
