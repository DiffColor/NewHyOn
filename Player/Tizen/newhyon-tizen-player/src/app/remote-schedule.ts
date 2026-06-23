import type { UpdatePayload } from './update-payload';

const REMOTE_SCHEDULE_STORAGE_KEY = 'newhyon-tizen-player.remote-schedule.v1';

export interface RemoteScheduleSnapshot {
  readonly savedAt: string;
  readonly generatedAt: string;
  readonly playerId: string;
  readonly playerName: string;
  readonly specialScheduleCount: number;
  readonly playlistScheduleCount: number;
  readonly contentPeriodCount: number;
  readonly schedule: unknown;
}

export function saveRemoteScheduleFromUpdatePayload(
  payload: UpdatePayload,
  storage: Storage = window.localStorage,
): RemoteScheduleSnapshot {
  const schedule = payload.Schedule;
  if (!schedule || typeof schedule !== 'object') {
    throw new Error('updateschedule payload에 Schedule이 없습니다.');
  }

  const scheduleObject = schedule as Record<string, unknown>;
  const snapshot: RemoteScheduleSnapshot = {
    savedAt: new Date().toISOString(),
    generatedAt: readString(scheduleObject.GeneratedAt ?? scheduleObject.generatedAt),
    playerId: readString(scheduleObject.PlayerId ?? scheduleObject.playerId),
    playerName: readString(scheduleObject.PlayerName ?? scheduleObject.playerName),
    specialScheduleCount: countCollection(scheduleObject.SpecialSchedules ?? scheduleObject.specialSchedules),
    playlistScheduleCount: countCollection(scheduleObject.Playlists ?? scheduleObject.playlists),
    contentPeriodCount: countCollection(scheduleObject.ContentPeriods ?? scheduleObject.contentPeriods),
    schedule,
  };

  storage.setItem(REMOTE_SCHEDULE_STORAGE_KEY, JSON.stringify(snapshot));
  return snapshot;
}

export function loadRemoteSchedule(storage: Storage = window.localStorage): RemoteScheduleSnapshot | null {
  const raw = storage.getItem(REMOTE_SCHEDULE_STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    if (isRemoteScheduleSnapshot(parsed)) {
      return parsed;
    }
  } catch {
  }

  storage.removeItem(REMOTE_SCHEDULE_STORAGE_KEY);
  return null;
}

function readString(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}

function countCollection(value: unknown): number {
  if (Array.isArray(value)) {
    return value.length;
  }

  if (value && typeof value === 'object') {
    return Object.keys(value).length;
  }

  return 0;
}

function isRemoteScheduleSnapshot(value: unknown): value is RemoteScheduleSnapshot {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as RemoteScheduleSnapshot;
  return typeof candidate.savedAt === 'string'
    && typeof candidate.generatedAt === 'string'
    && typeof candidate.playerId === 'string'
    && typeof candidate.playerName === 'string'
    && typeof candidate.specialScheduleCount === 'number'
    && typeof candidate.playlistScheduleCount === 'number'
    && typeof candidate.contentPeriodCount === 'number'
    && typeof candidate.schedule === 'object'
    && candidate.schedule !== null;
}
