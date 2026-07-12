import Horizon from '@horizon/client';
import { buildLicenseHubAuthMarker, type LicenseAuthState } from './licensehub-auth';
import { readSsspDeviceNetworkInfo } from './sssp-device-info';

const PLAYER_INFO_TABLE = 'PlayerInfoManager';
const HORIZON_WRITE_TIMEOUT_MS = 10000;

export interface PlayerInfoSyncOptions {
  readonly managerAddress: string;
  readonly playerGuid: string;
  readonly playerName: string;
  readonly appVersion: string;
  readonly authState?: LicenseAuthState | null;
}

interface RethinkEndpoint {
  readonly horizonHost: string;
  readonly secure: boolean;
}

interface PlayerInfoRecord {
  readonly id?: string;
  readonly PIF_GUID?: string;
  readonly PIF_PlayerName?: string;
  readonly PIF_CurrentPlayList?: string;
  readonly PIF_DefaultPlayList?: string;
  readonly PIF_IPAddress?: string;
  readonly PIF_OSName?: string;
  readonly PIF_MacAddress?: string;
  readonly PIF_AuthKey?: string;
  readonly command?: string;
}

interface HorizonObservable<T> {
  subscribe(observer: {
    next?: (value: T) => void;
    error?: (error: unknown) => void;
    complete?: () => void;
  }): { unsubscribe?: () => void };
}

interface HorizonCollection {
  find(idOrObject: unknown): { fetch(): HorizonObservable<unknown> };
  update(document: unknown): HorizonObservable<unknown>;
}

interface HorizonClient {
  (collectionName: string): HorizonCollection;
  disconnect(): void;
}

export async function syncPlayerInfoMessage(options: PlayerInfoSyncOptions): Promise<void> {
  const playerGuid = options.playerGuid.trim();
  if (!playerGuid) {
    throw new Error('기기 식별자가 없어 기기 정보를 동기화할 수 없습니다.');
  }

  const endpoint = parseRethinkEndpoint(options.managerAddress);
  const horizon = Horizon({
    host: endpoint.horizonHost,
    secure: endpoint.secure,
    lazyWrites: false,
  }) as HorizonClient;

  try {
    const existing = await fetchPlayerInfo(horizon, playerGuid);
    if (!existing) {
      throw new Error(`등록된 기기 정보를 찾지 못했습니다: ${playerGuid}`);
    }

    const authMarker = buildAuthMarkerForRemote(options.authState);
    const deviceNetwork = readSsspDeviceNetworkInfo();
    const payload = {
      id: playerGuid,
      PIF_PlayerName: options.playerName.trim(),
      PIF_CurrentPlayList: existing.PIF_CurrentPlayList ?? '',
      PIF_DefaultPlayList: existing.PIF_DefaultPlayList ?? '',
      PIF_IPAddress: deviceNetwork.ipAddress,
      PIF_OSName: buildOsName(options.appVersion),
      PIF_MacAddress: deviceNetwork.macAddress,
      PIF_AuthKey: authMarker || (existing.PIF_AuthKey ?? ''),
      command: existing.command ?? '',
    };

    await firstFromObservable(
      horizon(PLAYER_INFO_TABLE).update(payload),
      HORIZON_WRITE_TIMEOUT_MS,
      '기기 정보 업데이트 시간이 초과되었습니다.',
    );
  } finally {
    horizon.disconnect();
  }
}

async function fetchPlayerInfo(horizon: HorizonClient, playerGuid: string): Promise<PlayerInfoRecord | null> {
  const record = await firstFromObservable<unknown>(
    horizon(PLAYER_INFO_TABLE).find(playerGuid).fetch(),
    HORIZON_WRITE_TIMEOUT_MS,
    '기기 정보 조회 시간이 초과되었습니다.',
  );
  return record && typeof record === 'object' ? record as PlayerInfoRecord : null;
}

function firstFromObservable<T>(observable: HorizonObservable<T>, timeoutMs: number, errorMessage: string): Promise<T | null> {
  return new Promise((resolve, reject) => {
    let settled = false;
    let subscription: { unsubscribe?: () => void } | null = null;
    const timer = window.setTimeout(() => {
      if (settled) {
        return;
      }
      settled = true;
      subscription?.unsubscribe?.();
      reject(new Error(errorMessage));
    }, timeoutMs);

    subscription = observable.subscribe({
      next: (value) => {
        if (settled) {
          return;
        }
        settled = true;
        window.clearTimeout(timer);
        subscription?.unsubscribe?.();
        resolve(value ?? null);
      },
      error: (error) => {
        if (settled) {
          return;
        }
        settled = true;
        window.clearTimeout(timer);
        reject(error);
      },
      complete: () => {
        if (settled) {
          return;
        }
        settled = true;
        window.clearTimeout(timer);
        resolve(null);
      },
    });
  });
}

function normalizeHostOrUrl(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return '';
  }

  return /^https?:\/\//i.test(trimmed) ? trimmed : `http://${trimmed}`;
}

function parseRethinkEndpoint(value: string): RethinkEndpoint {
  const trimmed = value.trim();
  if (!trimmed) {
    throw new Error('데이터서버 주소가 비어 있습니다.');
  }

  const url = new URL(normalizeHostOrUrl(trimmed));
  const host = url.hostname.trim();
  if (!host) {
    throw new Error('데이터서버 주소 형식이 올바르지 않습니다.');
  }

  const explicitPort = url.port ? Number.parseInt(url.port, 10) : 0;
  const horizonPort = explicitPort > 0 ? explicitPort : 8181;
  return {
    horizonHost: `${host}:${horizonPort}`,
    secure: url.protocol === 'https:',
  };
}

function buildAuthMarkerForRemote(authState: LicenseAuthState | null | undefined): string {
  if (!authState?.isValid || !authState.deviceId.trim()) {
    return '';
  }

  return buildLicenseHubAuthMarker(authState.deviceId);
}

function buildOsName(appVersion: string): string {
  const version = appVersion.trim();
  return version ? `Tizen ${version}` : 'Tizen';
}
