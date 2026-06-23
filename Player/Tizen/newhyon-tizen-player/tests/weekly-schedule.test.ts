import { beforeEach, describe, expect, it } from 'vitest';
import {
  evaluateWeeklySchedule,
  getDefaultWeeklySchedule,
  loadWeeklySchedule,
  saveWeeklySchedule,
  saveWeeklyScheduleFromUpdatePayload,
} from '../src/app/weekly-schedule';

describe('weekly schedule', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('월요일부터 일요일까지 7개 요일을 생성한다', () => {
    expect(getDefaultWeeklySchedule().map((row) => row.dayCode)).toEqual([
      'MON',
      'TUE',
      'WED',
      'THU',
      'FRI',
      'SAT',
      'SUN',
    ]);
    expect(getDefaultWeeklySchedule().every((row) => row.isOnAir)).toBe(true);
  });

  it('수정한 주간 스케줄을 저장하고 다시 읽는다', () => {
    const rows = getDefaultWeeklySchedule();
    rows[0] = {
      ...rows[0]!,
      isOnAir: false,
      startHour: 9,
      startMinute: 30,
      endHour: 18,
      endMinute: 5,
    };

    saveWeeklySchedule(rows);

    expect(loadWeeklySchedule()[0]).toMatchObject({
      dayCode: 'MON',
      isOnAir: false,
      startHour: 9,
      startMinute: 30,
      endHour: 18,
      endMinute: 5,
    });
  });

  it('updateweekly payload의 Schedule.WeeklySchedule을 레퍼런스 요일 구조로 저장한다', () => {
    const rows = saveWeeklyScheduleFromUpdatePayload({
      Schedule: {
        WeeklySchedule: {
          MonSch: {
            IsOnAir: false,
            StartHour: 8,
            StartMinute: 10,
            EndHour: 22,
            EndMinute: 20,
          },
          SunSch: {
            IsOnAir: true,
            StartHour: 25,
            StartMinute: -1,
            EndHour: 23,
            EndMinute: 99,
          },
        },
      },
    });

    expect(rows[0]).toMatchObject({
      dayCode: 'MON',
      isOnAir: false,
      startHour: 8,
      startMinute: 10,
      endHour: 22,
      endMinute: 20,
    });
    expect(loadWeeklySchedule()[6]).toMatchObject({
      dayCode: 'SUN',
      startHour: 23,
      startMinute: 0,
      endHour: 23,
      endMinute: 59,
    });
  });

  it('방송시간을 현재 요일과 시간 기준으로 판정한다', () => {
    const rows = getDefaultWeeklySchedule();
    rows[0] = {
      ...rows[0]!,
      isOnAir: true,
      startHour: 9,
      startMinute: 0,
      endHour: 18,
      endMinute: 0,
    };

    expect(evaluateWeeklySchedule(new Date(2026, 5, 22, 8, 59), rows).isOnAir).toBe(false);
    expect(evaluateWeeklySchedule(new Date(2026, 5, 22, 9, 0), rows).isOnAir).toBe(true);
    expect(evaluateWeeklySchedule(new Date(2026, 5, 22, 17, 59), rows).isOnAir).toBe(true);
    expect(evaluateWeeklySchedule(new Date(2026, 5, 22, 18, 0), rows).isOnAir).toBe(false);
  });

  it('자정을 넘기는 방송시간은 다음날 종료 전까지 방송 중으로 판정한다', () => {
    const rows = getDefaultWeeklySchedule();
    rows[0] = {
      ...rows[0]!,
      isOnAir: true,
      startHour: 22,
      startMinute: 0,
      endHour: 6,
      endMinute: 0,
    };
    rows[1] = {
      ...rows[1]!,
      isOnAir: false,
    };

    expect(evaluateWeeklySchedule(new Date(2026, 5, 22, 21, 59), rows).isOnAir).toBe(false);
    expect(evaluateWeeklySchedule(new Date(2026, 5, 22, 22, 0), rows).isOnAir).toBe(true);
    expect(evaluateWeeklySchedule(new Date(2026, 5, 23, 5, 59), rows)).toMatchObject({
      isOnAir: true,
      activeDayCode: 'MON',
    });
    expect(evaluateWeeklySchedule(new Date(2026, 5, 23, 6, 0), rows).isOnAir).toBe(false);
  });
});
