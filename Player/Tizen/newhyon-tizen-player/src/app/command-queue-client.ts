import Horizon from '@horizon/client';
import {
  formatRethinkTimestamp,
  parseRethinkEndpoint,
  waitForObservable,
  type HorizonObservable,
} from './horizon-utils';

const COMMAND_QUEUE_TABLE = 'CommandQueue';
const HORIZON_TIMEOUT_MS = 10000;
const SENT_RETRY_DELAY_MS = 15000;


export interface CommandQueueEntry {
  readonly id?: string;
  readonly PlayerIds?: string[];
  readonly playerIds?: string[];
  readonly Command?: string;
  readonly command?: string;
  readonly payloadJson?: string;
  readonly PayloadBase64?: string;
  readonly Status?: Record<string, string>;
  readonly status?: Record<string, string>;
  readonly CreatedAt?: string;
  readonly createdAt?: string;
  readonly UpdatedAt?: string;
  readonly updatedAt?: string;
  readonly ExpiresAt?: string;
  readonly expiresAt?: string;
  readonly AttemptCount?: number;
  readonly attemptCount?: number;
  readonly LastAttemptAt?: string;
  readonly lastAttemptAt?: string;
  readonly Source?: string;
  readonly source?: string;
  readonly ReplacedBy?: string;
  readonly replacedBy?: string;
}

interface HorizonCollection {
  find(idOrObject: unknown): { fetch(): HorizonObservable<unknown> };
  order(fields: string | string[], direction?: 'ascending' | 'descending'): {
    fetch(): HorizonObservable<unknown>;
  };
  fetch(): HorizonObservable<unknown>;
  update(document: unknown): HorizonObservable<unknown>;
}

interface HorizonClient {
  (collectionName: string): HorizonCollection;
  disconnect(): void;
}

export class CommandQueueClient {
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

  async fetchNextPending(playerGuid: string): Promise<CommandQueueEntry | null> {
    const normalizedPlayerId = normalizePlayerId(playerGuid);
    if (!normalizedPlayerId) {
      return null;
    }

    const oldest = (await this.fetchCurrentCommands(normalizedPlayerId))[0];
    return oldest && isActionableStatus(oldest, normalizedPlayerId) ? oldest : null;
  }

  async fetchPendingCommands(playerGuid: string): Promise<CommandQueueEntry[]> {
    const normalizedPlayerId = normalizePlayerId(playerGuid);
    if (!normalizedPlayerId) {
      return [];
    }

    return (await this.fetchCurrentCommands(normalizedPlayerId))
      .filter((entry) => isActionableStatus(entry, normalizedPlayerId));
  }

  private async fetchCurrentCommands(normalizedPlayerId: string): Promise<CommandQueueEntry[]> {
    const rows = await this.fetchRecentRows();
    return rows
      .filter((entry) =>
        hasPlayer(entry, normalizedPlayerId)
        && isCurrentCommand(entry)
        && isUnsettledStatus(entry, normalizedPlayerId))
      .sort(compareCreatedAtAscending);
  }

  async markAttempt(commandId: string): Promise<void> {
    const id = commandId.trim();
    if (!id) {
      return;
    }

    const existing = await this.fetchById(id);
    if (!existing) {
      return;
    }

    const now = formatRethinkTimestamp();
    const attemptCount = Math.max(0, readNumber(existing.AttemptCount ?? existing.attemptCount)) + 1;
    await this.update({
      id,
      AttemptCount: attemptCount,
      LastAttemptAt: now,
      UpdatedAt: now,
    });
  }

  async markAck(commandId: string, playerGuid: string): Promise<void> {
    await this.updateStatus(commandId, playerGuid, 'ack');
  }

  async markFailed(commandId: string, playerGuid: string): Promise<void> {
    await this.updateStatus(commandId, playerGuid, 'failed');
  }

  async markRetry(commandId: string, playerGuid: string): Promise<void> {
    await this.updateStatus(commandId, playerGuid, 'sent');
  }

  async markSent(commandId: string, playerGuid: string): Promise<void> {
    await this.updateStatus(commandId, playerGuid, 'sent');
  }

  private async updateStatus(commandId: string, playerGuid: string, status: string): Promise<void> {
    const id = commandId.trim();
    const normalizedPlayerId = normalizePlayerId(playerGuid);
    const normalizedStatus = status.trim();
    if (!id || !normalizedPlayerId || !normalizedStatus) {
      return;
    }

    const existing = await this.fetchById(id);
    if (!existing) {
      return;
    }

    const currentStatus = readStatusMap(existing);
    await this.update({
      id,
      Status: {
        ...currentStatus,
        [normalizedPlayerId]: normalizedStatus,
      },
      UpdatedAt: formatRethinkTimestamp(),
    });
  }

  private async fetchRecentRows(): Promise<CommandQueueEntry[]> {
    const collection = this.horizon(COMMAND_QUEUE_TABLE);
    let value: unknown;
    try {
      value = await waitForObservable(
        collection.order(['CreatedAt', 'id'], 'ascending').fetch(),
        HORIZON_TIMEOUT_MS,
        'Horizon CommandQueue 조회 시간이 초과되었습니다.',
      );
    } catch {
      value = await waitForObservable(
        collection.fetch(),
        HORIZON_TIMEOUT_MS,
        'Horizon CommandQueue 조회 시간이 초과되었습니다.',
      );
    }

    if (!Array.isArray(value)) {
      return [];
    }

    return value.filter((row): row is CommandQueueEntry => Boolean(row) && typeof row === 'object');
  }

  private async fetchById(commandId: string): Promise<CommandQueueEntry | null> {
    const value = await waitForObservable(
      this.horizon(COMMAND_QUEUE_TABLE).find(commandId).fetch(),
      HORIZON_TIMEOUT_MS,
      'Horizon CommandQueue 단건 조회 시간이 초과되었습니다.',
    );
    return value && typeof value === 'object' ? value as CommandQueueEntry : null;
  }

  private async update(document: unknown): Promise<void> {
    await waitForObservable(
      this.horizon(COMMAND_QUEUE_TABLE).update(document),
      HORIZON_TIMEOUT_MS,
      'Horizon CommandQueue 업데이트 시간이 초과되었습니다.',
    );
  }
}

export function normalizePlayerId(playerId: string): string {
  return playerId.trim().toLowerCase();
}

export function readCommand(entry: CommandQueueEntry): string {
  return (entry.Command ?? entry.command ?? '').trim().toLowerCase();
}

export function readPayloadBase64(entry: CommandQueueEntry): string {
  return (entry.payloadJson ?? entry.PayloadBase64 ?? '').trim();
}

function hasPlayer(entry: CommandQueueEntry, normalizedPlayerId: string): boolean {
  const playerIds = entry.PlayerIds ?? entry.playerIds ?? [];
  return playerIds.some((id) => normalizePlayerId(id) === normalizedPlayerId);
}

function isActionableStatus(entry: CommandQueueEntry, normalizedPlayerId: string): boolean {
  const status = readStatusMap(entry)[normalizedPlayerId] ?? '';
  if (status.toLowerCase() === 'pending') {
    return true;
  }
  if (status.toLowerCase() !== 'sent') {
    return false;
  }

  return isSentDeliveryStale(entry);
}

function isUnsettledStatus(entry: CommandQueueEntry, normalizedPlayerId: string): boolean {
  const status = (readStatusMap(entry)[normalizedPlayerId] ?? '').toLowerCase();
  return status === 'pending' || status === 'sent';
}

export function isCurrentCommand(entry: CommandQueueEntry): boolean {
  if (readText(entry.ReplacedBy ?? entry.replacedBy)) {
    return false;
  }

  const expiresAt = parseTimestamp(entry.ExpiresAt ?? entry.expiresAt);
  return expiresAt <= 0 || expiresAt > Date.now();
}

function isSentDeliveryStale(entry: CommandQueueEntry): boolean {
  const timestamp = parseTimestamp(entry.UpdatedAt ?? entry.updatedAt)
    || parseTimestamp(entry.CreatedAt ?? entry.createdAt);
  if (timestamp <= 0) {
    return true;
  }

  return Date.now() - timestamp >= SENT_RETRY_DELAY_MS;
}

function readStatusMap(entry: CommandQueueEntry): Record<string, string> {
  const status = entry.Status ?? entry.status ?? {};
  if (!status || typeof status !== 'object' || Array.isArray(status)) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(status)
      .filter(([key, value]) => typeof key === 'string' && typeof value === 'string')
      .map(([key, value]) => [normalizePlayerId(key), value]),
  );
}

function compareCreatedAtAscending(left: CommandQueueEntry, right: CommandQueueEntry): number {
  const leftTime = parseTimestamp(left.CreatedAt ?? left.createdAt);
  const rightTime = parseTimestamp(right.CreatedAt ?? right.createdAt);
  if (leftTime !== rightTime) {
    return leftTime - rightTime;
  }

  return (left.id ?? '').localeCompare(right.id ?? '');
}

function readText(value: string | undefined): string {
  return value?.trim() ?? '';
}

function parseTimestamp(value: string | undefined): number {
  const normalized = value?.trim();
  if (!normalized) {
    return 0;
  }

  const isoCandidate = normalized.includes('T') ? normalized : normalized.replace(' ', 'T');
  const parsed = Date.parse(isoCandidate);
  return Number.isFinite(parsed) ? parsed : 0;
}

function readNumber(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}
