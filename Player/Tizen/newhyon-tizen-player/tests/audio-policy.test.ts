import { describe, expect, it, vi } from 'vitest';
import { TizenAudioPolicy, shouldMutePageAudio } from '../src/player/audio-policy';
import type { SeamlessPagePlan } from '../src/domain/page-plan';
import { RingLogger } from '../src/core/logger';

function createPage(isMuted: boolean): SeamlessPagePlan {
  return {
    playlistName: 'playlist',
    pageName: 'page',
    canvasWidth: 1920,
    canvasHeight: 1080,
    durationSeconds: 10,
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
  });

  it('페이지 오디오 정책을 TV audio mute에 적용한다', () => {
    const setMute = vi.fn();
    window.tizen = {
      tvaudiocontrol: {
        isMute: vi.fn(() => false),
        setMute,
      },
    };

    const policy = new TizenAudioPolicy(new RingLogger(5));
    policy.applyForPage(createPage(true));
    policy.applyForPage(createPage(true));
    policy.applyForPage(createPage(false));
    policy.restore();

    expect(setMute).toHaveBeenNthCalledWith(1, true);
    expect(setMute).toHaveBeenNthCalledWith(2, false);
    expect(setMute).toHaveBeenCalledTimes(2);
  });

  it('restore 시 이전 정책이 mute면 TV audio mute를 해제한다', () => {
    const setMute = vi.fn();
    window.tizen = {
      tvaudiocontrol: {
        setMute,
      },
    };

    const policy = new TizenAudioPolicy(new RingLogger(5));
    policy.applyForPage(createPage(true));
    policy.restore();

    expect(setMute).toHaveBeenNthCalledWith(1, true);
    expect(setMute).toHaveBeenNthCalledWith(2, false);
  });
});
