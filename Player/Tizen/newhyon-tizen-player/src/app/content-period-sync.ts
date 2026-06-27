import Horizon from '@horizon/client';
import type { ContentPeriodPayload } from './content-period';
import {
  parseRethinkEndpoint,
  waitForObservable,
  type HorizonObservable,
} from './horizon-utils';

const PERIOD_TABLE = 'PeriodData';
const HORIZON_TIMEOUT_MS = 10000;
const PERIOD_FETCH_LIMIT = 10000;

interface HorizonCollection {
  limit(size: number): { fetch(): HorizonObservable<unknown> };
}

interface HorizonClient {
  (collectionName: string): HorizonCollection;
  disconnect(): void;
}

export class ContentPeriodSyncClient {
  private readonly horizon: HorizonClient;

  constructor(managerAddress: string) {
    const endpoint = parseRethinkEndpoint(managerAddress);
    this.horizon = Horizon({
      host: endpoint.horizonHost,
      secure: endpoint.secure,
      lazyWrites: false,
    }) as HorizonClient;
  }

  dispose(): void {
    this.horizon.disconnect();
  }

  async fetchByContentGuids(contentGuids: readonly string[]): Promise<ContentPeriodPayload[]> {
    const requested = normalizeGuidSet(contentGuids);
    if (requested.size === 0) {
      return [];
    }

    const rows = await waitForObservable(
      this.horizon(PERIOD_TABLE).limit(PERIOD_FETCH_LIMIT).fetch(),
      HORIZON_TIMEOUT_MS,
      'Horizon PeriodData 조회 시간이 초과되었습니다.',
    );
    const values = Array.isArray(rows) ? rows : rows ? [rows] : [];
    return values
      .filter((row): row is ContentPeriodPayload => Boolean(row) && typeof row === 'object')
      .filter((row) => {
        const contentGuid = readContentGuid(row).toLowerCase();
        return contentGuid.length > 0 && requested.has(contentGuid);
      });
  }
}

function normalizeGuidSet(contentGuids: readonly string[]): Set<string> {
  return new Set(
    contentGuids
      .map((guid) => guid.trim().toLowerCase())
      .filter((guid) => guid.length > 0),
  );
}

function readContentGuid(payload: ContentPeriodPayload): string {
  return (payload.ContentGuid ?? payload.contentGuid ?? payload.Id ?? payload.id ?? '').trim();
}
