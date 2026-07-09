import { describe, expect, it } from 'vitest';
import { PlaybackImpactGuard } from '../src/app/playback-impact-guard';
import type { RemoteStreamingPlaybackSnapshot } from '../src/app/remote-streaming-protocol';

function snapshot(state: RemoteStreamingPlaybackSnapshot['state']): RemoteStreamingPlaybackSnapshot {
  return {
    state,
    playlistName: 'playlist',
    pageName: 'page',
    pageIndex: 0,
    pageCount: 1,
    width: 1920,
    height: 1080,
  };
}

describe('PlaybackImpactGuard', () => {
  it('재생 중에는 안정 재생 프로파일 한도 안에서 요청값을 적용한다', () => {
    const guard = new PlaybackImpactGuard();
    const profile = guard.resolveProfile(snapshot('playing'), {
      width: 1920,
      height: 1080,
      maxFps: 60,
      maxBitrateKbps: 5000,
    });

    expect(profile).toEqual({
      width: 1280,
      height: 720,
      maxFps: 15,
      maxBitrateKbps: 1000,
      reason: 'stable-playback',
    });
  });

  it('준비/버퍼링 중에는 재생 보호 프로파일을 넘지 않는다', () => {
    const guard = new PlaybackImpactGuard();

    expect(guard.resolveProfile(snapshot('preparing'), {}).reason).toBe('protect-playback');
    expect(guard.resolveProfile(snapshot('buffering'), { maxFps: 15 }).maxFps).toBe(3);
  });

  it('idle 상태에서는 idle 프로파일을 사용한다', () => {
    const guard = new PlaybackImpactGuard();
    const profile = guard.resolveProfile(snapshot('idle'), null);

    expect(profile.reason).toBe('idle');
    expect(profile.maxFps).toBe(8);
  });
});
