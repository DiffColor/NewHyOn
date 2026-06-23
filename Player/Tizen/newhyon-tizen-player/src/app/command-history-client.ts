import Horizon from '@horizon/client';
import {
  formatRethinkTimestamp,
  parseRethinkEndpoint,
  waitForObservable,
  type HorizonObservable,
} from './horizon-utils';

const COMMAND_HISTORY_TABLE = 'CommandHistory';
const HORIZON_TIMEOUT_MS = 10000;

export const COMMAND_HISTORY_STATUS = {
  queued: 'queued',
  inProgress: 'in_progress',
  done: 'done',
  failed: 'failed',
  cancelled: 'cancelled',
} as const;

export type CommandHistoryStatus = typeof COMMAND_HISTORY_STATUS[keyof typeof COMMAND_HISTORY_STATUS];

interface HorizonCollection {
  upsert(document: unknown): HorizonObservable<unknown>;
  update(document: unknown): HorizonObservable<unknown>;
}

interface HorizonClient {
  (collectionName: string): HorizonCollection;
  disconnect(): void;
}

export class CommandHistoryClient {
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

  async createQueued(
    playerGuid: string,
    playerName: string,
    command: string,
    metadata = '',
    refQueueId = '',
  ): Promise<string> {
    const historyId = buildCommandHistoryId(playerGuid, command, refQueueId);
    const now = formatRethinkTimestamp();
    await this.upsert({
      id: historyId,
      playerId: playerGuid.trim(),
      playerName: playerName.trim(),
      command: command.trim().toLowerCase(),
      refQueueId: refQueueId.trim(),
      status: COMMAND_HISTORY_STATUS.queued,
      errorCode: '',
      errorMessage: '',
      createdAt: now,
      startedAt: '',
      endedAt: '',
      metadata: metadata.trim(),
    });
    return historyId;
  }

  async markInProgress(historyId: string, refQueueId = '', metadata = ''): Promise<void> {
    const id = historyId.trim();
    if (!id) {
      return;
    }

    await this.update({
      id,
      status: COMMAND_HISTORY_STATUS.inProgress,
      startedAt: formatRethinkTimestamp(),
      refQueueId: refQueueId.trim(),
      metadata: metadata.trim(),
    });
  }

  async markDone(
    historyId: string,
    status: CommandHistoryStatus,
    errorCode = '',
    errorMessage = '',
    metadata?: string,
  ): Promise<void> {
    const id = historyId.trim();
    if (!id) {
      return;
    }

    const payload: Record<string, unknown> = {
      id,
      status,
      errorCode: errorCode.trim(),
      errorMessage: errorMessage.trim(),
      endedAt: formatRethinkTimestamp(),
    };
    if (metadata !== undefined) {
      payload.metadata = metadata.trim();
    }

    await this.update(payload);
  }

  private async upsert(document: unknown): Promise<void> {
    await waitForObservable(
      this.horizon(COMMAND_HISTORY_TABLE).upsert(document),
      HORIZON_TIMEOUT_MS,
      'Horizon CommandHistory 저장 시간이 초과되었습니다.',
    );
  }

  private async update(document: unknown): Promise<void> {
    await waitForObservable(
      this.horizon(COMMAND_HISTORY_TABLE).update(document),
      HORIZON_TIMEOUT_MS,
      'Horizon CommandHistory 업데이트 시간이 초과되었습니다.',
    );
  }
}

function buildCommandHistoryId(playerGuid: string, command: string, refQueueId: string): string {
  const owner = playerGuid.trim();
  const normalizedCommand = command.trim().toLowerCase();
  const queueId = refQueueId.trim();
  if (queueId) {
    return queueId;
  }

  if (!owner || !normalizedCommand) {
    return `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }

  return `${owner}:${normalizedCommand}`;
}
