import { beforeEach, describe, expect, it } from 'vitest';
import {
  isContentPeriodAllowed,
  saveContentPeriodsFromSchedule,
  saveContentPeriodsFromUpdatePayload,
} from '../src/app/content-period';

describe('content period schedule', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('updateschedule의 ContentPeriods를 저장하고 날짜/시간 범위로 재생 가능 여부를 판정한다', () => {
    const result = saveContentPeriodsFromSchedule({
      ContentPeriods: [
        {
          ContentGuid: 'content-a',
          StartDate: '2026-06-27',
          EndDate: '2026-06-30',
          StartTime: '09:00',
          EndTime: '18:00',
        },
      ],
    });

    expect(result).toMatchObject({ upserted: 1, total: 1 });
    expect(isContentPeriodAllowed('content-a', new Date(2026, 5, 27, 10, 0))).toBe(true);
    expect(isContentPeriodAllowed('content-a', new Date(2026, 5, 27, 18, 0))).toBe(false);
    expect(isContentPeriodAllowed('content-a', new Date(2026, 6, 1, 10, 0))).toBe(false);
    expect(isContentPeriodAllowed('content-without-period', new Date(2026, 5, 27, 10, 0))).toBe(true);
  });

  it('종료 시간이 시작 시간보다 이른 야간 시간대를 윈도우 플레이어와 동일하게 허용한다', () => {
    saveContentPeriodsFromSchedule({
      contentPeriods: [
        {
          contentGuid: 'overnight',
          startDate: '2026-06-27',
          endDate: '2026-06-30',
          startTime: '22:00',
          endTime: '06:00',
        },
      ],
    });

    expect(isContentPeriodAllowed('overnight', new Date(2026, 5, 27, 23, 30))).toBe(true);
    expect(isContentPeriodAllowed('overnight', new Date(2026, 5, 28, 5, 30))).toBe(true);
    expect(isContentPeriodAllowed('overnight', new Date(2026, 5, 28, 12, 0))).toBe(false);
  });

  it('updatecontentperiod에 요청 GUID만 오고 기간 데이터가 없으면 기존 캐시를 제거한다', () => {
    saveContentPeriodsFromSchedule({
      ContentPeriods: [
        {
          ContentGuid: 'content-a',
          StartDate: '2026-06-27',
          EndDate: '2026-06-30',
        },
      ],
    });

    const result = saveContentPeriodsFromUpdatePayload({
      ContentPeriodUpdateGuids: ['content-a'],
    });

    expect(result).toMatchObject({ requested: 1, upserted: 0, removed: 1, total: 0 });
    expect(isContentPeriodAllowed('content-a', new Date(2026, 6, 1, 10, 0))).toBe(true);
  });
});
