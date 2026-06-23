import Horizon from '@horizon/client';

const GUID_SYNC_STORAGE_KEY = 'newhyon-tizen-player.guid-sync.v1';
const HORIZON_QUERY_TIMEOUT_MS = 10000;

export interface PlayerInfoSnapshot {
  readonly id?: string;
  readonly PIF_GUID?: string;
  readonly pif_GUID?: string;
  readonly pifGuid?: string;
  readonly PIF_PlayerName?: string;
  readonly pif_PlayerName?: string;
  readonly pifPlayerName?: string;
  readonly PIF_CurrentPlayList?: string;
  readonly pif_CurrentPlayList?: string;
  readonly pifCurrentPlayList?: string;
  readonly PIF_DefaultPlayList?: string;
  readonly pif_DefaultPlayList?: string;
  readonly pifDefaultPlayList?: string;
}

export interface PlayerIdentity {
  readonly playerGuid: string;
  readonly playerName: string;
}

export interface PlayerGuidSynchronizerOptions {
  readonly managerAddress: string;
  readonly playerName: string;
  readonly initialIdentity?: PlayerIdentity | null;
  readonly storage?: Storage;
  readonly onStatus?: (status: string, detail: string) => void;
  readonly onGuidChanged?: (identity: PlayerIdentity) => void;
}

interface RethinkEndpoint {
  readonly horizonHost: string;
  readonly secure: boolean;
}

interface GuidSyncStorageSnapshot {
  readonly playerGuid: string;
  readonly playerName: string;
  readonly guidVerified: boolean;
  readonly lastVerifiedAt: number;
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
  findAll(...documents: unknown[]): { fetch(): HorizonObservable<unknown> };
  limit(size: number): { fetch(): HorizonObservable<unknown> };
}

interface HorizonClient {
  (collectionName: string): HorizonCollection;
  disconnect(): void;
}

export class PlayerGuidSynchronizer {
  private readonly storage: Storage;
  private readonly normalizedPlayerName: string;
  private currentIdentity: PlayerIdentity | null;
  private guidVerified = false;
  private lastVerifiedAt = 0;

  constructor(private readonly options: PlayerGuidSynchronizerOptions) {
    this.storage = options.storage ?? window.localStorage;
    this.normalizedPlayerName = normalizePlayerName(options.playerName);
    const stored = readStoredGuidSync(this.storage);
    const usableStored = stored && isSamePlayerName(stored.playerName, this.normalizedPlayerName) ? stored : null;
    this.currentIdentity = options.initialIdentity ?? (usableStored
      ? { playerGuid: usableStored.playerGuid, playerName: usableStored.playerName }
      : null);
    this.guidVerified = Boolean(options.initialIdentity?.playerGuid) || Boolean(usableStored?.guidVerified);
    this.lastVerifiedAt = usableStored?.lastVerifiedAt ?? 0;
  }

  getCurrentIdentity(): PlayerIdentity | null {
    return this.currentIdentity;
  }

  async ensurePlayerIdentity(forceRefresh = false): Promise<PlayerIdentity | null> {
    if (!this.normalizedPlayerName) {
      this.guidVerified = Boolean(this.currentIdentity?.playerGuid);
      return this.currentIdentity;
    }

    if (!forceRefresh) {
      return this.currentIdentity;
    }

    this.options.onStatus?.('guid-syncing', this.normalizedPlayerName);
    const previousGuid = this.currentIdentity?.playerGuid ?? '';
    const now = Date.now();
    try {
      const remote = await this.resolveRemotePlayerInfo(previousGuid);
      this.lastVerifiedAt = now;
      if (!remote) {
        this.guidVerified = false;
        this.currentIdentity = null;
        this.storage.removeItem(GUID_SYNC_STORAGE_KEY);
        this.options.onStatus?.('guid-waiting', this.normalizedPlayerName);
        return null;
      }

      const nextIdentity = toPlayerIdentity(remote, this.normalizedPlayerName);
      if (!nextIdentity) {
        this.guidVerified = false;
        this.options.onStatus?.('guid-empty', this.normalizedPlayerName);
        return this.currentIdentity;
      }

      this.guidVerified = true;
      this.currentIdentity = nextIdentity;
      writeStoredGuidSync(this.storage, {
        ...nextIdentity,
        guidVerified: true,
        lastVerifiedAt: now,
      });
      this.options.onStatus?.('guid-synced', `${nextIdentity.playerName}/${nextIdentity.playerGuid}`);
      if (previousGuid && previousGuid.toLowerCase() !== nextIdentity.playerGuid.toLowerCase()) {
        this.options.onGuidChanged?.(nextIdentity);
      }
      return nextIdentity;
    } catch (error) {
      this.guidVerified = false;
      this.options.onStatus?.('guid-failed', formatError(error));
      return this.currentIdentity;
    }
  }

  private async resolveRemotePlayerInfo(preferredGuid: string): Promise<PlayerInfoSnapshot | null> {
    const endpoint = parseRethinkEndpoint(this.options.managerAddress);
    const horizon = Horizon({
      host: endpoint.horizonHost,
      secure: endpoint.secure,
      lazyWrites: false,
    }) as HorizonClient;

    try {
      if (preferredGuid) {
        const playerByGuid = await fetchPlayerByGuid(horizon, preferredGuid);
        if (playerByGuid && isSamePlayerName(readPlayerName(playerByGuid), this.normalizedPlayerName)) {
          return playerByGuid;
        }
      }

      return await fetchPlayerByName(horizon, this.normalizedPlayerName, preferredGuid);
    } finally {
      horizon.disconnect();
    }
  }
}

export async function resolvePlayerIdentityOnce(
  managerAddress: string,
  playerName: string,
  storage: Storage = window.localStorage,
): Promise<PlayerIdentity> {
  const synchronizer = new PlayerGuidSynchronizer({
    managerAddress,
    playerName,
    storage,
  });
  const identity = await synchronizer.ensurePlayerIdentity(true);
  if (!identity?.playerGuid) {
    throw new Error(`Horizon PlayerInfoManager에서 플레이어 GUID를 동기화하지 못했습니다: ${playerName.trim()}`);
  }

  return identity;
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

async function fetchPlayerByGuid(horizon: HorizonClient, playerGuid: string): Promise<PlayerInfoSnapshot | null> {
  const byId = await firstFromObservable<unknown>(
    horizon('PlayerInfoManager').find(playerGuid).fetch(),
    HORIZON_QUERY_TIMEOUT_MS,
    'Horizon PlayerInfoManager GUID 조회 시간이 초과되었습니다.',
  );
  return byId && typeof byId === 'object' ? byId as PlayerInfoSnapshot : null;
}

async function fetchPlayerByName(
  horizon: HorizonClient,
  playerName: string,
  preferredGuid: string,
): Promise<PlayerInfoSnapshot | null> {
  const rows = await firstFromObservable<unknown>(
    horizon('PlayerInfoManager').findAll({ PIF_PlayerName: playerName }).fetch(),
    HORIZON_QUERY_TIMEOUT_MS,
    'Horizon PlayerInfoManager 이름 조회 시간이 초과되었습니다.',
  );
  if (!Array.isArray(rows)) {
    throw new Error('Horizon PlayerInfoManager 응답 형식이 올바르지 않습니다.');
  }

  const candidates = rows
    .filter((row): row is PlayerInfoSnapshot => Boolean(row) && typeof row === 'object')
    .filter((row) => isSamePlayerName(readPlayerName(row), playerName))
    .filter((row) => Boolean(readPlayerGuid(row)));
  if (candidates.length === 0) {
    return null;
  }

  return candidates.sort((left, right) => comparePlayerCandidate(left, right, preferredGuid))[0] ?? null;
}

function comparePlayerCandidate(left: PlayerInfoSnapshot, right: PlayerInfoSnapshot, preferredGuid: string): number {
  const leftGuid = readPlayerGuid(left);
  const rightGuid = readPlayerGuid(right);
  const leftPreferred = preferredGuid && leftGuid.toLowerCase() === preferredGuid.toLowerCase() ? 1 : 0;
  const rightPreferred = preferredGuid && rightGuid.toLowerCase() === preferredGuid.toLowerCase() ? 1 : 0;
  if (leftPreferred !== rightPreferred) {
    return rightPreferred - leftPreferred;
  }

  const leftPlayable = hasPlayableConfiguration(left) ? 1 : 0;
  const rightPlayable = hasPlayableConfiguration(right) ? 1 : 0;
  if (leftPlayable !== rightPlayable) {
    return rightPlayable - leftPlayable;
  }

  return leftGuid.localeCompare(rightGuid);
}

function hasPlayableConfiguration(player: PlayerInfoSnapshot): boolean {
  return Boolean(
    (player.PIF_CurrentPlayList ?? player.pif_CurrentPlayList ?? player.pifCurrentPlayList ?? '').trim()
    || (player.PIF_DefaultPlayList ?? player.pif_DefaultPlayList ?? player.pifDefaultPlayList ?? '').trim(),
  );
}

function toPlayerIdentity(player: PlayerInfoSnapshot, requestedPlayerName: string): PlayerIdentity | null {
  const playerGuid = readPlayerGuid(player);
  if (!playerGuid) {
    return null;
  }

  return {
    playerGuid,
    playerName: readPlayerName(player) || requestedPlayerName,
  };
}

function readPlayerGuid(player: PlayerInfoSnapshot): string {
  return (player.id ?? player.PIF_GUID ?? player.pif_GUID ?? player.pifGuid ?? '').trim();
}

function readPlayerName(player: PlayerInfoSnapshot): string {
  return (player.PIF_PlayerName ?? player.pif_PlayerName ?? player.pifPlayerName ?? '').trim();
}

function normalizePlayerName(playerName: string): string {
  return playerName.trim();
}

function isSamePlayerName(left: string, right: string): boolean {
  return normalizePlayerName(left).toLowerCase() === normalizePlayerName(right).toLowerCase();
}

function readStoredGuidSync(storage: Storage): GuidSyncStorageSnapshot | null {
  const raw = storage.getItem(GUID_SYNC_STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as Partial<GuidSyncStorageSnapshot>;
    const playerGuid = typeof parsed.playerGuid === 'string' ? parsed.playerGuid.trim() : '';
    const playerName = typeof parsed.playerName === 'string' ? parsed.playerName.trim() : '';
    if (!playerGuid || !playerName) {
      storage.removeItem(GUID_SYNC_STORAGE_KEY);
      return null;
    }

    return {
      playerGuid,
      playerName,
      guidVerified: parsed.guidVerified === true,
      lastVerifiedAt: typeof parsed.lastVerifiedAt === 'number' ? parsed.lastVerifiedAt : 0,
    };
  } catch {
    storage.removeItem(GUID_SYNC_STORAGE_KEY);
    return null;
  }
}

function writeStoredGuidSync(storage: Storage, snapshot: GuidSyncStorageSnapshot): void {
  storage.setItem(GUID_SYNC_STORAGE_KEY, JSON.stringify(snapshot));
}

function formatError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  return String(error);
}
