import type { UpdatePayload } from './update-payload';

const CONTENT_PERIOD_STORAGE_KEY = 'newhyon-tizen-player.content-periods.v1';

export interface ContentPeriodPayload {
  readonly ContentGuid?: string;
  readonly contentGuid?: string;
  readonly StartDate?: string;
  readonly startDate?: string;
  readonly EndDate?: string;
  readonly endDate?: string;
  readonly StartTime?: string;
  readonly startTime?: string;
  readonly EndTime?: string;
  readonly endTime?: string;
}

export interface NormalizedContentPeriod {
  readonly contentGuid: string;
  readonly startDate: string;
  readonly endDate: string;
  readonly startTime: string;
  readonly endTime: string;
}

export interface ContentPeriodUpdateResult {
  readonly requested: number;
  readonly upserted: number;
  readonly removed: number;
  readonly total: number;
}

type ContentPeriodStore = Record<string, NormalizedContentPeriod>;

export function saveContentPeriodsFromUpdatePayload(
  payload: UpdatePayload,
  storage: Storage = window.localStorage,
): ContentPeriodUpdateResult {
  const current = loadContentPeriodStore(storage);
  const requestedGuids = normalizeGuidList(payload.ContentPeriodUpdateGuids);
  const periods = extractContentPeriodsFromUpdatePayload(payload);
  const touchedKeys = new Set<string>();
  let upserted = 0;

  periods.forEach((period) => {
    const key = period.contentGuid.toLowerCase();
    current[key] = period;
    touchedKeys.add(key);
    upserted += 1;
  });

  let removed = 0;
  requestedGuids.forEach((guid) => {
    const key = guid.toLowerCase();
    if (touchedKeys.has(key)) {
      return;
    }

    if (current[key]) {
      delete current[key];
      removed += 1;
    }
  });

  persistContentPeriodStore(current, storage);
  return {
    requested: requestedGuids.length,
    upserted,
    removed,
    total: Object.keys(current).length,
  };
}

export function saveContentPeriodsFromSchedule(
  schedule: unknown,
  storage: Storage = window.localStorage,
): ContentPeriodUpdateResult {
  const current = loadContentPeriodStore(storage);
  const periods = extractContentPeriodsFromSchedule(schedule);
  periods.forEach((period) => {
    current[period.contentGuid.toLowerCase()] = period;
  });
  persistContentPeriodStore(current, storage);
  return {
    requested: 0,
    upserted: periods.length,
    removed: 0,
    total: Object.keys(current).length,
  };
}

export function hasContentPeriod(contentGuid: string | undefined, storage: Storage = window.localStorage): boolean {
  return findContentPeriod(contentGuid, storage) !== null;
}

export function isContentPeriodAllowed(
  contentGuid: string | undefined,
  now: Date,
  storage: Storage = window.localStorage,
): boolean {
  const period = findContentPeriod(contentGuid, storage);
  if (!period) {
    return true;
  }

  const today = formatLocalDate(now);
  if (period.startDate && today < period.startDate) {
    return false;
  }

  if (period.endDate && today > period.endDate) {
    return false;
  }

  const startTime = parseMinuteOfDay(period.startTime);
  const endTime = parseMinuteOfDay(period.endTime);
  if (startTime === null || endTime === null) {
    return true;
  }

  const currentMinute = now.getHours() * 60 + now.getMinutes();
  if (endTime >= startTime) {
    return currentMinute >= startTime && currentMinute < endTime;
  }

  return currentMinute >= startTime || currentMinute < endTime;
}

export function extractContentPeriodsFromUpdatePayload(payload: UpdatePayload): NormalizedContentPeriod[] {
  const directPeriods = extractContentPeriodsFromCollection(
    (payload as { readonly ContentPeriods?: unknown; readonly contentPeriods?: unknown }).ContentPeriods
      ?? (payload as { readonly ContentPeriods?: unknown; readonly contentPeriods?: unknown }).contentPeriods,
  );
  const schedulePeriods = extractContentPeriodsFromSchedule(payload.Schedule);
  const byGuid = new Map<string, NormalizedContentPeriod>();

  [...directPeriods, ...schedulePeriods].forEach((period) => {
    byGuid.set(period.contentGuid.toLowerCase(), period);
  });

  return [...byGuid.values()];
}

export function extractContentPeriodsFromSchedule(schedule: unknown): NormalizedContentPeriod[] {
  if (!schedule || typeof schedule !== 'object') {
    return [];
  }

  const scheduleObject = schedule as Record<string, unknown>;
  return extractContentPeriodsFromCollection(scheduleObject.ContentPeriods ?? scheduleObject.contentPeriods);
}

function findContentPeriod(contentGuid: string | undefined, storage: Storage): NormalizedContentPeriod | null {
  const key = contentGuid?.trim().toLowerCase();
  if (!key) {
    return null;
  }

  return loadContentPeriodStore(storage)[key] ?? null;
}

function extractContentPeriodsFromCollection(value: unknown): NormalizedContentPeriod[] {
  return objectCollectionValues(value)
    .map(normalizeContentPeriod)
    .filter((period): period is NormalizedContentPeriod => period !== null);
}

function normalizeContentPeriod(value: unknown): NormalizedContentPeriod | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const payload = value as ContentPeriodPayload;
  const contentGuid = (payload.ContentGuid ?? payload.contentGuid ?? '').trim();
  if (!contentGuid) {
    return null;
  }

  const startDate = normalizeDateText(payload.StartDate ?? payload.startDate ?? '');
  const rawEndDate = normalizeDateText(payload.EndDate ?? payload.endDate ?? '');
  const endDate = startDate && rawEndDate && rawEndDate < startDate ? startDate : rawEndDate;
  return {
    contentGuid,
    startDate,
    endDate,
    startTime: normalizeTimeText(payload.StartTime ?? payload.startTime ?? ''),
    endTime: normalizeTimeText(payload.EndTime ?? payload.endTime ?? ''),
  };
}

function loadContentPeriodStore(storage: Storage): ContentPeriodStore {
  const raw = storage.getItem(CONTENT_PERIOD_STORAGE_KEY);
  if (!raw) {
    return {};
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    if (!parsed || typeof parsed !== 'object') {
      return {};
    }

    const normalized: ContentPeriodStore = {};
    Object.values(parsed as Record<string, unknown>).forEach((value) => {
      const period = normalizeStoredContentPeriod(value);
      if (period) {
        normalized[period.contentGuid.toLowerCase()] = period;
      }
    });
    return normalized;
  } catch {
    storage.removeItem(CONTENT_PERIOD_STORAGE_KEY);
    return {};
  }
}

function normalizeStoredContentPeriod(value: unknown): NormalizedContentPeriod | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const candidate = value as NormalizedContentPeriod;
  const contentGuid = candidate.contentGuid?.trim();
  if (!contentGuid) {
    return null;
  }

  return {
    contentGuid,
    startDate: normalizeDateText(candidate.startDate),
    endDate: normalizeDateText(candidate.endDate),
    startTime: normalizeTimeText(candidate.startTime),
    endTime: normalizeTimeText(candidate.endTime),
  };
}

function persistContentPeriodStore(store: ContentPeriodStore, storage: Storage): void {
  storage.setItem(CONTENT_PERIOD_STORAGE_KEY, JSON.stringify(store));
}

function objectCollectionValues(value: unknown): unknown[] {
  if (Array.isArray(value)) {
    return value;
  }

  if (value && typeof value === 'object') {
    return Object.values(value);
  }

  return [];
}

function normalizeGuidList(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .map((item) => typeof item === 'string' ? item.trim() : '')
    .filter((item) => item.length > 0);
}

function normalizeDateText(value: string | undefined): string {
  const raw = value?.trim();
  if (!raw) {
    return '';
  }

  const match = /^(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})/.exec(raw);
  if (match) {
    return `${match[1]}-${match[2]!.padStart(2, '0')}-${match[3]!.padStart(2, '0')}`;
  }

  const parsed = new Date(raw);
  if (!Number.isNaN(parsed.getTime())) {
    return formatLocalDate(parsed);
  }

  return '';
}

function normalizeTimeText(value: string | undefined): string {
  const raw = value?.trim();
  if (!raw) {
    return '';
  }

  const match = /^(\d{1,2}):(\d{1,2})/.exec(raw);
  if (!match) {
    return '';
  }

  const hour = Number.parseInt(match[1]!, 10);
  const minute = Number.parseInt(match[2]!, 10);
  if (hour < 0 || hour > 23 || minute < 0 || minute > 59) {
    return '';
  }

  return `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`;
}

function parseMinuteOfDay(value: string): number | null {
  const match = /^(\d{2}):(\d{2})$/.exec(value);
  if (!match) {
    return null;
  }

  return Number.parseInt(match[1]!, 10) * 60 + Number.parseInt(match[2]!, 10);
}

function formatLocalDate(value: Date): string {
  return [
    value.getFullYear(),
    String(value.getMonth() + 1).padStart(2, '0'),
    String(value.getDate()).padStart(2, '0'),
  ].join('-');
}
