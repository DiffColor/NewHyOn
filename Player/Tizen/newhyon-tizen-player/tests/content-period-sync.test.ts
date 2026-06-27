import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ContentPeriodSyncClient } from '../src/app/content-period-sync';
import { isContentPeriodAllowed, saveContentPeriodSnapshot } from '../src/app/content-period';

const horizonFactory = vi.hoisted(() => vi.fn());

vi.mock('@horizon/client', () => ({
  default: horizonFactory,
}));

function createObservable<T>(value: T) {
  return {
    subscribe(observer: { next?: (value: T) => void; complete?: () => void }) {
      observer.next?.(value);
      observer.complete?.();
      return {
        unsubscribe: vi.fn(),
      };
    },
  };
}

describe('ContentPeriodSyncClient', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    horizonFactory.mockReset();
    window.localStorage.clear();
  });

  it('PeriodData 테이블에서 요청 콘텐츠 GUID에 해당하는 기간만 가져와 캐시에 적용한다', async () => {
    const fetch = vi.fn(() => createObservable([
      {
        id: 'period-1',
        ContentGuid: 'content-a',
        StartDate: '2026-06-20',
        EndDate: '2026-06-21',
        StartTime: '00:00',
        EndTime: '23:59',
      },
      {
        id: 'period-2',
        ContentGuid: 'content-b',
        StartDate: '2026-06-27',
        EndDate: '2026-06-30',
        StartTime: '09:00',
        EndTime: '18:00',
      },
      {
        id: 'period-other',
        ContentGuid: 'other-content',
        StartDate: '2026-06-27',
        EndDate: '2026-06-30',
      },
    ]));
    const collection = vi.fn((name: string) => {
      expect(name).toBe('PeriodData');
      return {
        limit: vi.fn(() => ({ fetch })),
      };
    });
    const disconnect = vi.fn();
    horizonFactory.mockReturnValue(Object.assign(collection, { disconnect }));

    const client = new ContentPeriodSyncClient('turtlesrv.ddns.net');
    const periods = await client.fetchByContentGuids(['content-a', 'CONTENT-B']);
    const result = saveContentPeriodSnapshot(['content-a', 'content-b'], periods);
    client.dispose();

    expect(periods.map((period) => period.ContentGuid)).toEqual(['content-a', 'content-b']);
    expect(result).toMatchObject({ requested: 2, upserted: 2, removed: 0, total: 2 });
    expect(isContentPeriodAllowed('content-a', new Date(2026, 5, 22, 10, 0))).toBe(false);
    expect(isContentPeriodAllowed('content-b', new Date(2026, 5, 27, 10, 0))).toBe(true);
    expect(isContentPeriodAllowed('content-b', new Date(2026, 5, 27, 20, 0))).toBe(false);
    expect(disconnect).toHaveBeenCalledTimes(1);
  });

  it('요청한 GUID가 서버 snapshot에 없으면 기존 기간 캐시를 제거한다', () => {
    saveContentPeriodSnapshot(['content-a'], [{
      ContentGuid: 'content-a',
      StartDate: '2026-06-20',
      EndDate: '2026-06-21',
    }]);

    const result = saveContentPeriodSnapshot(['content-a'], []);

    expect(result).toMatchObject({ requested: 1, upserted: 0, removed: 1, total: 0 });
    expect(isContentPeriodAllowed('content-a', new Date(2026, 5, 22, 10, 0))).toBe(true);
  });
});
