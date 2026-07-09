import type { RemoteStreamingPlaybackSnapshot, RemoteStreamProfile } from './remote-streaming-protocol';

const PROTECT_PLAYBACK_PROFILE: RemoteStreamProfile = {
  width: 640,
  height: 360,
  maxFps: 3,
  maxBitrateKbps: 250,
  reason: 'protect-playback',
};

const STABLE_PLAYBACK_PROFILE: RemoteStreamProfile = {
  width: 1280,
  height: 720,
  maxFps: 15,
  maxBitrateKbps: 1000,
  reason: 'stable-playback',
};

const IDLE_PROFILE: RemoteStreamProfile = {
  width: 960,
  height: 540,
  maxFps: 8,
  maxBitrateKbps: 650,
  reason: 'idle',
};

function clampProfileValue(value: unknown, min: number, max: number, fallback: number): number {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) {
    return fallback;
  }
  return Math.min(max, Math.max(min, Math.round(numeric)));
}

export class PlaybackImpactGuard {
  resolveProfile(
    snapshot: RemoteStreamingPlaybackSnapshot,
    requestedProfile: Partial<RemoteStreamProfile> | null | undefined,
  ): RemoteStreamProfile {
    const base = snapshot.state === 'preparing' || snapshot.state === 'buffering'
      ? PROTECT_PLAYBACK_PROFILE
      : snapshot.state === 'playing'
        ? STABLE_PLAYBACK_PROFILE
        : IDLE_PROFILE;

    return {
      width: clampProfileValue(requestedProfile?.width, 320, base.width, base.width),
      height: clampProfileValue(requestedProfile?.height, 180, base.height, base.height),
      maxFps: clampProfileValue(requestedProfile?.maxFps, 1, base.maxFps, base.maxFps),
      maxBitrateKbps: clampProfileValue(requestedProfile?.maxBitrateKbps, 100, base.maxBitrateKbps, base.maxBitrateKbps),
      reason: base.reason,
    };
  }
}
