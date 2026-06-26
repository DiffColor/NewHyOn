import type { UpdatePayload } from './update-payload';
import type { PageInfoClass, PlayerManifest } from '../domain/models';

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

export interface RemoteScheduleDecision {
  readonly playlistName: string;
  readonly scheduleId: string;
  readonly isFromSchedule: boolean;
  readonly nextPlaylistName: string;
  readonly nextScheduleId: string;
  readonly nextSwitchAtMs: number;
}

interface SpecialSchedulePayload {
  readonly Id?: string;
  readonly id?: string;
  readonly PageListName?: string;
  readonly pageListName?: string;
  readonly DayOfWeek1?: boolean;
  readonly DayOfWeek2?: boolean;
  readonly DayOfWeek3?: boolean;
  readonly DayOfWeek4?: boolean;
  readonly DayOfWeek5?: boolean;
  readonly DayOfWeek6?: boolean;
  readonly DayOfWeek7?: boolean;
  readonly dayOfWeek1?: boolean;
  readonly dayOfWeek2?: boolean;
  readonly dayOfWeek3?: boolean;
  readonly dayOfWeek4?: boolean;
  readonly dayOfWeek5?: boolean;
  readonly dayOfWeek6?: boolean;
  readonly dayOfWeek7?: boolean;
  readonly IsPeriodEnable?: boolean;
  readonly isPeriodEnable?: boolean;
  readonly DisplayStartH?: number;
  readonly DisplayStartM?: number;
  readonly DisplayEndH?: number;
  readonly DisplayEndM?: number;
  readonly displayStartH?: number;
  readonly displayStartM?: number;
  readonly displayEndH?: number;
  readonly displayEndM?: number;
  readonly PeriodStartYear?: number;
  readonly PeriodStartMonth?: number;
  readonly PeriodStartDay?: number;
  readonly PeriodEndYear?: number;
  readonly PeriodEndMonth?: number;
  readonly PeriodEndDay?: number;
  readonly periodStartYear?: number;
  readonly periodStartMonth?: number;
  readonly periodStartDay?: number;
  readonly periodEndYear?: number;
  readonly periodEndMonth?: number;
  readonly periodEndDay?: number;
}

interface SchedulePlaylistPayload {
  readonly PlaylistName?: string;
  readonly playlistName?: string;
  readonly PageList?: { readonly PLI_PageListName?: string };
  readonly pageList?: { readonly PLI_PageListName?: string };
  readonly Pages?: PageInfoClass[];
  readonly pages?: PageInfoClass[];
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

export function saveRemoteScheduleSnapshot(
  snapshot: RemoteScheduleSnapshot,
  storage: Storage = window.localStorage,
): void {
  storage.setItem(REMOTE_SCHEDULE_STORAGE_KEY, JSON.stringify(snapshot));
}

export function evaluateRemoteSchedule(
  snapshot: RemoteScheduleSnapshot,
  now: Date,
  fallbackPlaylistName: string,
): RemoteScheduleDecision {
  const schedules = extractSpecialSchedules(snapshot.schedule);
  const safeFallback = fallbackPlaylistName.trim();
  const active = selectActiveSchedule(schedules, now);
  const playlistName = active ? schedulePlaylistName(active) : safeFallback;
  const scheduleId = active ? scheduleIdText(active) : '';
  const candidateTimes = collectCandidateTimes(schedules, now);
  let nextPlaylistName = '';
  let nextScheduleId = '';
  let nextSwitchAtMs = -1;

  for (const candidate of candidateTimes) {
    if (candidate.getTime() <= now.getTime()) {
      continue;
    }

    const nextDecisionTime = new Date(candidate.getTime() + 1000);
    const nextActive = selectActiveSchedule(schedules, nextDecisionTime);
    const resolvedNextPlaylist = nextActive ? schedulePlaylistName(nextActive) : safeFallback;
    if (sameText(playlistName, resolvedNextPlaylist)) {
      continue;
    }

    nextPlaylistName = resolvedNextPlaylist;
    nextScheduleId = nextActive ? scheduleIdText(nextActive) : '';
    nextSwitchAtMs = candidate.getTime();
    break;
  }

  return {
    playlistName,
    scheduleId,
    isFromSchedule: active !== null,
    nextPlaylistName,
    nextScheduleId,
    nextSwitchAtMs,
  };
}

export function buildManifestFromRemoteSchedulePlaylist(
  snapshot: RemoteScheduleSnapshot,
  playlistName: string,
  preserveAspectRatio: boolean,
): PlayerManifest | null {
  const targetName = playlistName.trim();
  if (!targetName) {
    return null;
  }

  const playlist = extractSchedulePlaylists(snapshot.schedule)
    .find((candidate) => sameText(schedulePlaylistPayloadName(candidate), targetName));
  const pageList = playlist?.PageList ?? playlist?.pageList;
  const pages = playlist?.Pages ?? playlist?.pages;
  if (!playlist || !pageList || !Array.isArray(pages) || pages.length === 0) {
    return null;
  }

  const resolvedName = schedulePlaylistPayloadName(playlist) || targetName;
  return {
    playlistName: resolvedName,
    preserveAspectRatio,
    pages,
  };
}

export function buildManifestsFromRemoteSchedulePlaylists(
  snapshot: RemoteScheduleSnapshot,
  preserveAspectRatio: boolean,
): PlayerManifest[] {
  return extractSchedulePlaylists(snapshot.schedule)
    .map((playlist) => {
      const pageList = playlist.PageList ?? playlist.pageList;
      const pages = playlist.Pages ?? playlist.pages;
      const playlistName = schedulePlaylistPayloadName(playlist);
      if (!playlistName || !pageList || !Array.isArray(pages) || pages.length === 0) {
        return null;
      }

      return {
        playlistName,
        preserveAspectRatio,
        pages,
      };
    })
    .filter((manifest): manifest is PlayerManifest => manifest !== null);
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

function extractSpecialSchedules(schedule: unknown): SpecialSchedulePayload[] {
  if (!schedule || typeof schedule !== 'object') {
    return [];
  }

  const scheduleObject = schedule as Record<string, unknown>;
  const value = scheduleObject.SpecialSchedules ?? scheduleObject.specialSchedules;
  return Array.isArray(value)
    ? value.filter((item): item is SpecialSchedulePayload => Boolean(item) && typeof item === 'object')
    : [];
}

function extractSchedulePlaylists(schedule: unknown): SchedulePlaylistPayload[] {
  if (!schedule || typeof schedule !== 'object') {
    return [];
  }

  const scheduleObject = schedule as Record<string, unknown>;
  const value = scheduleObject.Playlists ?? scheduleObject.playlists;
  return Array.isArray(value)
    ? value.filter((item): item is SchedulePlaylistPayload => Boolean(item) && typeof item === 'object')
    : [];
}

function selectActiveSchedule(schedules: readonly SpecialSchedulePayload[], now: Date): SpecialSchedulePayload | null {
  const active = schedules
    .filter((schedule) => schedulePlaylistName(schedule) && isScheduleActive(schedule, now))
    .sort((left, right) => scheduleIdText(left).localeCompare(scheduleIdText(right), undefined, { sensitivity: 'accent' }));
  return active[0] ?? null;
}

function isScheduleActive(schedule: SpecialSchedulePayload, now: Date): boolean {
  if (!schedulePlaylistName(schedule)) {
    return false;
  }

  const crosses = crossesMidnight(schedule);
  const currentMinutes = now.getHours() * 60 + now.getMinutes();
  const endMinutes = readNumber(schedule.DisplayEndH ?? schedule.displayEndH) * 60
    + readNumber(schedule.DisplayEndM ?? schedule.displayEndM);
  const usePreviousDay = crosses && currentMinutes < endMinutes;
  const effectiveDate = usePreviousDay
    ? new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1)
    : new Date(now.getFullYear(), now.getMonth(), now.getDate());

  return isPeriodValid(schedule, effectiveDate)
    && isDayEnabled(schedule, effectiveDate.getDay())
    && isTimeValid(schedule, now);
}

function isPeriodValid(schedule: SpecialSchedulePayload, date: Date): boolean {
  const enabled = schedule.IsPeriodEnable ?? schedule.isPeriodEnable ?? false;
  if (!enabled) {
    return true;
  }

  const startYear = readNumber(schedule.PeriodStartYear ?? schedule.periodStartYear);
  const endYear = readNumber(schedule.PeriodEndYear ?? schedule.periodEndYear);
  if (startYear <= 0 || endYear <= 0) {
    return true;
  }

  const start = new Date(
    startYear,
    Math.max(0, readNumber(schedule.PeriodStartMonth ?? schedule.periodStartMonth) - 1),
    readNumber(schedule.PeriodStartDay ?? schedule.periodStartDay),
  );
  const end = new Date(
    endYear,
    Math.max(0, readNumber(schedule.PeriodEndMonth ?? schedule.periodEndMonth) - 1),
    readNumber(schedule.PeriodEndDay ?? schedule.periodEndDay),
    23,
    59,
    59,
    999,
  );
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
    return true;
  }

  return date.getTime() >= start.getTime() && date.getTime() <= end.getTime();
}

function isDayEnabled(schedule: SpecialSchedulePayload, dayOfWeek: number): boolean {
  switch (dayOfWeek) {
    case 0: return schedule.DayOfWeek1 ?? schedule.dayOfWeek1 ?? false;
    case 1: return schedule.DayOfWeek2 ?? schedule.dayOfWeek2 ?? false;
    case 2: return schedule.DayOfWeek3 ?? schedule.dayOfWeek3 ?? false;
    case 3: return schedule.DayOfWeek4 ?? schedule.dayOfWeek4 ?? false;
    case 4: return schedule.DayOfWeek5 ?? schedule.dayOfWeek5 ?? false;
    case 5: return schedule.DayOfWeek6 ?? schedule.dayOfWeek6 ?? false;
    case 6: return schedule.DayOfWeek7 ?? schedule.dayOfWeek7 ?? false;
    default: return false;
  }
}

function isTimeValid(schedule: SpecialSchedulePayload, now: Date): boolean {
  const start = readNumber(schedule.DisplayStartH ?? schedule.displayStartH) * 60
    + readNumber(schedule.DisplayStartM ?? schedule.displayStartM);
  const end = readNumber(schedule.DisplayEndH ?? schedule.displayEndH) * 60
    + readNumber(schedule.DisplayEndM ?? schedule.displayEndM);
  const current = now.getHours() * 60 + now.getMinutes();
  if (start === end) {
    return true;
  }

  return end > start
    ? current >= start && current < end
    : current >= start || current < end;
}

function collectCandidateTimes(schedules: readonly SpecialSchedulePayload[], now: Date): Date[] {
  return schedules
    .flatMap((schedule) => [findNextStartTime(schedule, now), findActiveEndTime(schedule, now)])
    .filter((date): date is Date => date !== null)
    .filter((date, index, list) => list.findIndex((candidate) => candidate.getTime() === date.getTime()) === index)
    .sort((left, right) => left.getTime() - right.getTime());
}

function findNextStartTime(schedule: SpecialSchedulePayload, now: Date): Date | null {
  if (!schedulePlaylistName(schedule)) {
    return null;
  }

  const baseDay = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  for (let offset = 0; offset <= 7; offset += 1) {
    const candidateDay = new Date(baseDay.getFullYear(), baseDay.getMonth(), baseDay.getDate() + offset);
    if (!isPeriodValid(schedule, candidateDay) || !isDayEnabled(schedule, candidateDay.getDay())) {
      continue;
    }

    const start = new Date(
      candidateDay.getFullYear(),
      candidateDay.getMonth(),
      candidateDay.getDate(),
      readNumber(schedule.DisplayStartH ?? schedule.displayStartH),
      readNumber(schedule.DisplayStartM ?? schedule.displayStartM),
    );
    if (start.getTime() > now.getTime()) {
      return start;
    }
  }

  return null;
}

function findActiveEndTime(schedule: SpecialSchedulePayload, now: Date): Date | null {
  if (!isScheduleActive(schedule, now)) {
    return null;
  }

  const startHour = readNumber(schedule.DisplayStartH ?? schedule.displayStartH);
  const startMinute = readNumber(schedule.DisplayStartM ?? schedule.displayStartM);
  const endHour = readNumber(schedule.DisplayEndH ?? schedule.displayEndH);
  const endMinute = readNumber(schedule.DisplayEndM ?? schedule.displayEndM);
  if (startHour === endHour && startMinute === endMinute) {
    return null;
  }

  const crosses = crossesMidnight(schedule);
  const currentMinutes = now.getHours() * 60 + now.getMinutes();
  const endMinutes = endHour * 60 + endMinute;
  const baseDate = crosses && currentMinutes < endMinutes
    ? new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1)
    : new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const end = new Date(baseDate.getFullYear(), baseDate.getMonth(), baseDate.getDate(), endHour, endMinute);
  if (crosses) {
    end.setDate(end.getDate() + 1);
  }

  return end;
}

function crossesMidnight(schedule: SpecialSchedulePayload): boolean {
  const startHour = readNumber(schedule.DisplayStartH ?? schedule.displayStartH);
  const startMinute = readNumber(schedule.DisplayStartM ?? schedule.displayStartM);
  const endHour = readNumber(schedule.DisplayEndH ?? schedule.displayEndH);
  const endMinute = readNumber(schedule.DisplayEndM ?? schedule.displayEndM);
  return endHour < startHour || (endHour === startHour && endMinute < startMinute);
}

function schedulePlaylistName(schedule: SpecialSchedulePayload): string {
  return readString(schedule.PageListName ?? schedule.pageListName);
}

function schedulePlaylistPayloadName(playlist: SchedulePlaylistPayload): string {
  return readString(playlist.PlaylistName ?? playlist.playlistName)
    || readString((playlist.PageList ?? playlist.pageList)?.PLI_PageListName);
}

function scheduleIdText(schedule: SpecialSchedulePayload): string {
  return readString(schedule.Id ?? schedule.id);
}

function readNumber(value: unknown): number {
  const number = typeof value === 'number' ? value : Number.parseInt(String(value ?? '0'), 10);
  return Number.isFinite(number) ? number : 0;
}

function sameText(left: string, right: string): boolean {
  return left.trim().localeCompare(right.trim(), undefined, { sensitivity: 'accent' }) === 0;
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
