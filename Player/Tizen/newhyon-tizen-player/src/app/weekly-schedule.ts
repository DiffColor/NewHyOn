import type { UpdatePayload } from './update-payload';

const STORAGE_KEY = 'newhyon-tizen-player.weekly-schedule.v1';

export type WeeklyDayCode = 'SUN' | 'MON' | 'TUE' | 'WED' | 'THU' | 'FRI' | 'SAT';

export interface WeeklyScheduleRow {
  readonly dayCode: WeeklyDayCode;
  readonly dayLabel: string;
  readonly isOnAir: boolean;
  readonly startHour: number;
  readonly startMinute: number;
  readonly endHour: number;
  readonly endMinute: number;
}

export interface WeeklyScheduleEvaluation {
  readonly isOnAir: boolean;
  readonly dayCode: WeeklyDayCode;
  readonly activeDayCode: WeeklyDayCode | null;
  readonly activeRow: WeeklyScheduleRow | null;
  readonly reason: string;
}

interface ReferenceDaySchedule {
  readonly StartHour?: number;
  readonly startHour?: number;
  readonly StartMinute?: number;
  readonly startMinute?: number;
  readonly EndHour?: number;
  readonly endHour?: number;
  readonly EndMinute?: number;
  readonly endMinute?: number;
  readonly IsOnAir?: boolean;
  readonly isOnAir?: boolean;
}

interface ReferenceWeeklySchedule {
  readonly MonSch?: ReferenceDaySchedule | null;
  readonly monSch?: ReferenceDaySchedule | null;
  readonly TueSch?: ReferenceDaySchedule | null;
  readonly tueSch?: ReferenceDaySchedule | null;
  readonly WedSch?: ReferenceDaySchedule | null;
  readonly wedSch?: ReferenceDaySchedule | null;
  readonly ThuSch?: ReferenceDaySchedule | null;
  readonly thuSch?: ReferenceDaySchedule | null;
  readonly FriSch?: ReferenceDaySchedule | null;
  readonly friSch?: ReferenceDaySchedule | null;
  readonly SatSch?: ReferenceDaySchedule | null;
  readonly satSch?: ReferenceDaySchedule | null;
  readonly SunSch?: ReferenceDaySchedule | null;
  readonly sunSch?: ReferenceDaySchedule | null;
}

const DEFAULT_WEEKLY_SCHEDULE: WeeklyScheduleRow[] = [
  createDefaultRow('MON', '월요일'),
  createDefaultRow('TUE', '화요일'),
  createDefaultRow('WED', '수요일'),
  createDefaultRow('THU', '목요일'),
  createDefaultRow('FRI', '금요일'),
  createDefaultRow('SAT', '토요일'),
  createDefaultRow('SUN', '일요일'),
];

const JS_DAY_CODES: WeeklyDayCode[] = ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT'];

export function getDefaultWeeklySchedule(): WeeklyScheduleRow[] {
  return DEFAULT_WEEKLY_SCHEDULE.map((row) => ({ ...row }));
}

export function loadWeeklySchedule(storage: Storage = window.localStorage): WeeklyScheduleRow[] {
  const raw = storage.getItem(STORAGE_KEY);
  if (!raw) {
    return getDefaultWeeklySchedule();
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    if (Array.isArray(parsed)) {
      return normalizeRows(parsed);
    }
  } catch {
    storage.removeItem(STORAGE_KEY);
  }

  return getDefaultWeeklySchedule();
}

export function saveWeeklySchedule(rows: readonly WeeklyScheduleRow[], storage: Storage = window.localStorage): void {
  storage.setItem(STORAGE_KEY, JSON.stringify(normalizeRows(rows)));
}

export function evaluateWeeklySchedule(
  now: Date = new Date(),
  rows: readonly WeeklyScheduleRow[] = loadWeeklySchedule(),
): WeeklyScheduleEvaluation {
  const normalizedRows = normalizeRows(rows);
  const rowMap = new Map(normalizedRows.map((row) => [row.dayCode, row]));
  const dayCode = JS_DAY_CODES[now.getDay()] ?? 'SUN';
  const minuteOfDay = now.getHours() * 60 + now.getMinutes();
  const todayRow = rowMap.get(dayCode) ?? createDefaultRow(dayCode, dayCode);

  if (isRowActiveToday(todayRow, minuteOfDay)) {
    return {
      isOnAir: true,
      dayCode,
      activeDayCode: todayRow.dayCode,
      activeRow: todayRow,
      reason: formatActiveReason(todayRow),
    };
  }

  const previousDayCode = JS_DAY_CODES[(now.getDay() + 6) % 7] ?? 'SUN';
  const previousRow = rowMap.get(previousDayCode);
  if (previousRow && isPreviousOvernightRowActive(previousRow, minuteOfDay)) {
    return {
      isOnAir: true,
      dayCode,
      activeDayCode: previousRow.dayCode,
      activeRow: previousRow,
      reason: formatActiveReason(previousRow),
    };
  }

  return {
    isOnAir: false,
    dayCode,
    activeDayCode: null,
    activeRow: todayRow,
    reason: `${todayRow.dayLabel} 방송시간 아님 (${formatScheduleTime(todayRow)})`,
  };
}

export function saveWeeklyScheduleFromUpdatePayload(
  payload: UpdatePayload,
  storage: Storage = window.localStorage,
): WeeklyScheduleRow[] {
  const weekly = extractWeeklySchedule(payload);
  if (!weekly) {
    throw new Error('updateweekly payload에 WeeklySchedule이 없습니다.');
  }

  const rows = rowsFromReferenceWeeklySchedule(weekly);
  saveWeeklySchedule(rows, storage);
  return rows;
}

function createDefaultRow(dayCode: WeeklyDayCode, dayLabel: string): WeeklyScheduleRow {
  return {
    dayCode,
    dayLabel,
    isOnAir: true,
    startHour: 0,
    startMinute: 0,
    endHour: 0,
    endMinute: 0,
  };
}

function normalizeRows(value: readonly unknown[]): WeeklyScheduleRow[] {
  const existing = new Map<string, Partial<WeeklyScheduleRow>>();
  value.forEach((row) => {
    if (!row || typeof row !== 'object') {
      return;
    }
    const candidate = row as Partial<WeeklyScheduleRow>;
    const dayCode = normalizeDayCode(candidate.dayCode);
    if (dayCode) {
      existing.set(dayCode, candidate);
    }
  });

  return getDefaultWeeklySchedule().map((defaultRow) => {
    const current = existing.get(defaultRow.dayCode);
    if (!current) {
      return defaultRow;
    }

    return {
      dayCode: defaultRow.dayCode,
      dayLabel: defaultRow.dayLabel,
      isOnAir: typeof current.isOnAir === 'boolean' ? current.isOnAir : defaultRow.isOnAir,
      startHour: clampTimePart(current.startHour, 23),
      startMinute: clampTimePart(current.startMinute, 59),
      endHour: clampTimePart(current.endHour, 23),
      endMinute: clampTimePart(current.endMinute, 59),
    };
  });
}

function normalizeDayCode(value: unknown): WeeklyDayCode | null {
  const normalized = String(value ?? '').trim().toUpperCase();
  if (
    normalized === 'SUN'
    || normalized === 'MON'
    || normalized === 'TUE'
    || normalized === 'WED'
    || normalized === 'THU'
    || normalized === 'FRI'
    || normalized === 'SAT'
  ) {
    return normalized;
  }

  return null;
}

function clampTimePart(value: unknown, max: number): number {
  const parsed = typeof value === 'number' ? value : Number.parseInt(String(value ?? ''), 10);
  if (!Number.isFinite(parsed)) {
    return 0;
  }

  return Math.min(Math.max(0, Math.round(parsed)), max);
}

function rowStartMinute(row: WeeklyScheduleRow): number {
  return row.startHour * 60 + row.startMinute;
}

function rowEndMinute(row: WeeklyScheduleRow): number {
  return row.endHour * 60 + row.endMinute;
}

function isRowActiveToday(row: WeeklyScheduleRow, minuteOfDay: number): boolean {
  if (!row.isOnAir) {
    return false;
  }

  const start = rowStartMinute(row);
  const end = rowEndMinute(row);
  if (start === end) {
    return true;
  }

  if (start < end) {
    return minuteOfDay >= start && minuteOfDay < end;
  }

  return minuteOfDay >= start;
}

function isPreviousOvernightRowActive(row: WeeklyScheduleRow, minuteOfDay: number): boolean {
  if (!row.isOnAir) {
    return false;
  }

  const start = rowStartMinute(row);
  const end = rowEndMinute(row);
  return start > end && minuteOfDay < end;
}

function formatScheduleTime(row: WeeklyScheduleRow): string {
  return `${String(row.startHour).padStart(2, '0')}:${String(row.startMinute).padStart(2, '0')}`
    + `~${String(row.endHour).padStart(2, '0')}:${String(row.endMinute).padStart(2, '0')}`;
}

function formatActiveReason(row: WeeklyScheduleRow): string {
  return `${row.dayLabel} 방송시간 (${formatScheduleTime(row)})`;
}

function extractWeeklySchedule(payload: UpdatePayload): ReferenceWeeklySchedule | null {
  const schedule = payload.Schedule;
  if (!schedule || typeof schedule !== 'object') {
    return null;
  }

  const scheduleObject = schedule as Record<string, unknown>;
  const weekly = scheduleObject.WeeklySchedule ?? scheduleObject.weeklySchedule;
  if (weekly && typeof weekly === 'object') {
    return weekly as ReferenceWeeklySchedule;
  }

  return hasReferenceDaySchedules(scheduleObject) ? scheduleObject as ReferenceWeeklySchedule : null;
}

function hasReferenceDaySchedules(value: Record<string, unknown>): boolean {
  return Boolean(value.MonSch || value.monSch || value.SunSch || value.sunSch);
}

function rowsFromReferenceWeeklySchedule(weekly: ReferenceWeeklySchedule): WeeklyScheduleRow[] {
  return [
    rowFromReferenceDay('MON', '월요일', weekly.MonSch ?? weekly.monSch),
    rowFromReferenceDay('TUE', '화요일', weekly.TueSch ?? weekly.tueSch),
    rowFromReferenceDay('WED', '수요일', weekly.WedSch ?? weekly.wedSch),
    rowFromReferenceDay('THU', '목요일', weekly.ThuSch ?? weekly.thuSch),
    rowFromReferenceDay('FRI', '금요일', weekly.FriSch ?? weekly.friSch),
    rowFromReferenceDay('SAT', '토요일', weekly.SatSch ?? weekly.satSch),
    rowFromReferenceDay('SUN', '일요일', weekly.SunSch ?? weekly.sunSch),
  ];
}

function rowFromReferenceDay(
  dayCode: WeeklyDayCode,
  dayLabel: string,
  schedule: ReferenceDaySchedule | null | undefined,
): WeeklyScheduleRow {
  const defaultRow = createDefaultRow(dayCode, dayLabel);
  if (!schedule) {
    return defaultRow;
  }

  return {
    ...defaultRow,
    isOnAir: typeof schedule.IsOnAir === 'boolean'
      ? schedule.IsOnAir
      : typeof schedule.isOnAir === 'boolean'
        ? schedule.isOnAir
        : true,
    startHour: clampTimePart(schedule.StartHour ?? schedule.startHour, 23),
    startMinute: clampTimePart(schedule.StartMinute ?? schedule.startMinute, 59),
    endHour: clampTimePart(schedule.EndHour ?? schedule.endHour, 23),
    endMinute: clampTimePart(schedule.EndMinute ?? schedule.endMinute, 59),
  };
}
