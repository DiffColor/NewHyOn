import {
  CommandQueueClient,
  readCommand,
  readPayloadBase64,
  type CommandQueueEntry,
} from './command-queue-client';
import {
  COMMAND_HISTORY_STATUS,
  CommandHistoryClient,
  type CommandHistoryStatus,
} from './command-history-client';
import {
  decodeUpdatePayload,
  hasUsableUpdatePayload,
  type UpdatePayload,
} from './update-payload';
import type { SignalRMessage } from './signalr-hub-client';

const DEFAULT_COMMAND_POLL_INTERVAL_MS = 5000;

export interface RemoteCommandServiceOptions {
  readonly managerAddress: string;
  readonly playerGuid: string;
  readonly playerName: string;
  readonly pollIntervalMs?: number;
  readonly onStatus?: (status: string, detail: string) => void;
  readonly onUpdateList: (payload: UpdatePayload, urgent: boolean, commandId: string | null) => Promise<RemoteCommandCallbackResult>;
  readonly onUpdateSchedule?: (payload: UpdatePayload) => Promise<boolean>;
  readonly onUpdateWeekly?: (payload: UpdatePayload) => Promise<boolean>;
  readonly onUpdateContentPeriod?: (payload: UpdatePayload | null) => Promise<boolean>;
  readonly onCheck: () => Promise<boolean>;
  readonly onGetMac: () => Promise<boolean>;
  readonly onClearQueue?: () => Promise<number>;
  readonly onReboot: () => Promise<RemoteCommandCallbackResult>;
  readonly onPowerOff: () => Promise<RemoteCommandCallbackResult>;
}

interface CommandHandleResult {
  readonly handled: boolean;
  readonly historyStatus?: CommandHistoryStatus;
  readonly errorCode?: string;
  readonly errorMessage?: string;
  readonly metadata?: string;
  readonly afterAck?: () => void;
}

export type RemoteCommandCallbackResult = boolean | CommandHandleResult;

export class RemoteCommandService {
  private readonly commandQueueClient: CommandQueueClient;
  private readonly commandHistoryClient: CommandHistoryClient;
  private readonly pollIntervalMs: number;
  private timerId: number | null = null;
  private disposed = false;
  private processing: Promise<void> = Promise.resolve();

  constructor(private readonly options: RemoteCommandServiceOptions) {
    this.pollIntervalMs = Math.max(1, options.pollIntervalMs ?? DEFAULT_COMMAND_POLL_INTERVAL_MS);
    this.commandQueueClient = new CommandQueueClient(options.managerAddress);
    this.commandHistoryClient = new CommandHistoryClient(options.managerAddress);
  }

  start(): void {
    if (this.disposed || this.timerId !== null) {
      return;
    }

    void this.checkNow();
    this.timerId = window.setInterval(() => {
      void this.checkNow();
    }, this.pollIntervalMs);
  }

  async checkNow(): Promise<void> {
    if (this.disposed) {
      return;
    }

    try {
      await this.enqueue(async () => {
        const playerGuid = this.options.playerGuid.trim();
        if (!playerGuid) {
          return;
        }

        const entry = await this.commandQueueClient.fetchNextPending(playerGuid);
        if (!entry?.id) {
          return;
        }

        this.options.onStatus?.('received', `${entry.id}:${readCommand(entry)}`);
        await this.commandQueueClient.markAttempt(entry.id);
        const result = await this.handleCommandEntry(entry, false);
        if (result.handled) {
          await this.commandQueueClient.markAck(entry.id, playerGuid);
          this.options.onStatus?.('ack', entry.id);
          runAfterAck(result.afterAck, this.options.onStatus);
        } else if (shouldRetryCommand(readCommand(entry), result)) {
          await this.commandQueueClient.markRetry(entry.id, playerGuid);
          this.options.onStatus?.('retry', `${entry.id}:${result.errorCode ?? 'COMMAND_FAILED'}`);
        } else {
          await this.commandQueueClient.markFailed(entry.id, playerGuid);
          this.options.onStatus?.('failed', `${entry.id}:${result.errorCode ?? 'COMMAND_FAILED'}`);
        }
      });
    } catch (error) {
      this.options.onStatus?.('connection-failed', formatError(error));
    }
  }

  handleSignalRMessage(message: SignalRMessage): void {
    if (this.disposed) {
      return;
    }

    const dataType = String(message.dataType ?? message.DataType ?? '').toLowerCase();
    if (dataType === 'statemessage') {
      return;
    }

    void this.checkNow();
  }

  dispose(): void {
    this.disposed = true;
    if (this.timerId !== null) {
      window.clearInterval(this.timerId);
      this.timerId = null;
    }
    this.commandQueueClient.dispose();
    this.commandHistoryClient.dispose();
  }

  private async handleCommandEntry(entry: CommandQueueEntry, isUrgent: boolean): Promise<CommandHandleResult> {
    const command = readCommand(entry);
    if (!command) {
      return { handled: false, errorCode: 'EMPTY_COMMAND' };
    }

    const payload = decodeUpdatePayload(readPayloadBase64(entry));
    return this.handleCommand(command, payload, isUrgent, entry.id ?? null);
  }

  private async handleCommand(
    command: string,
    payload: UpdatePayload | null,
    isUrgent: boolean,
    commandId: string | null,
  ): Promise<CommandHandleResult> {
    const normalizedCommand = command.trim().toLowerCase();
    if (!normalizedCommand) {
      return { handled: false, errorCode: 'EMPTY_COMMAND' };
    }

    this.options.onStatus?.('handling', commandId ? `${normalizedCommand}:${commandId}` : normalizedCommand);
    const historyId = await this.commandHistoryClient.createQueued(
      this.options.playerGuid,
      this.options.playerName,
      normalizedCommand,
      commandId ? `commandId=${commandId}` : '',
      commandId ?? '',
    );
    await this.commandHistoryClient.markInProgress(historyId, commandId ?? '');

    let result: CommandHandleResult;
    try {
      result = await this.executeCommand(normalizedCommand, payload, isUrgent, commandId);
    } catch (error) {
      result = {
        handled: false,
        errorCode: 'COMMAND_EXCEPTION',
        errorMessage: formatError(error),
      };
    }
    await this.commandHistoryClient.markDone(
      historyId,
      result.historyStatus ?? (result.handled ? COMMAND_HISTORY_STATUS.done : COMMAND_HISTORY_STATUS.failed),
      result.errorCode ?? '',
      result.errorMessage ?? '',
      result.metadata,
    );

    return result;
  }

  private async executeCommand(
    command: string,
    payload: UpdatePayload | null,
    isUrgent: boolean,
    commandId: string | null,
  ): Promise<CommandHandleResult> {
    switch (command) {
      case 'updatelist':
        if (!hasUsableUpdatePayload(payload)) {
          return {
            handled: false,
            errorCode: 'PAYLOAD_MISSING',
            errorMessage: 'Missing update payload',
          };
        }
        return toResult(await this.options.onUpdateList(payload, isUrgent, commandId));
      case 'updateschedule':
        if (!payload?.Schedule) {
          return {
            handled: false,
            errorCode: 'SCHEDULE_PAYLOAD',
            errorMessage: 'payload missing',
          };
        }
        return toResult(await (this.options.onUpdateSchedule?.(payload) ?? Promise.resolve(true)));
      case 'updateweekly':
        if (!payload?.Schedule) {
          return {
            handled: false,
            errorCode: 'WEEKLY_PAYLOAD',
            errorMessage: 'payload missing',
          };
        }
        return toResult(await (this.options.onUpdateWeekly?.(payload) ?? Promise.resolve(true)));
      case 'updatecontentperiod':
        return toResult(await (this.options.onUpdateContentPeriod?.(payload) ?? Promise.resolve(true)));
      case 'reboot':
        return toResult(await this.options.onReboot());
      case 'poweroff':
        return toResult(await this.options.onPowerOff());
      case 'clearqueue': {
        const cancelled = await (this.options.onClearQueue?.() ?? Promise.resolve(0));
        return {
          handled: true,
          historyStatus: cancelled > 0 ? COMMAND_HISTORY_STATUS.cancelled : COMMAND_HISTORY_STATUS.done,
          metadata: `cancelled=${cancelled}`,
        };
      }
      case 'check':
        return toResult(await this.options.onCheck());
      case 'getmac':
        return toResult(await this.options.onGetMac());
      case 'upgrade':
      case 'sync':
        return {
          handled: false,
          errorCode: 'UNSUPPORTED_COMMAND',
          errorMessage: 'Unsupported command',
        };
      default:
        return {
          handled: false,
          errorCode: 'UNKNOWN_COMMAND',
          errorMessage: 'Unknown command',
        };
    }
  }

  private async enqueue(task: () => Promise<void>): Promise<void> {
    const next = this.processing.then(task, task);
    this.processing = next.catch(() => undefined);
    await next;
  }

}

function toResult(result: RemoteCommandCallbackResult): CommandHandleResult {
  if (typeof result !== 'boolean') {
    return result;
  }

  return result ? { handled: true } : { handled: false, errorCode: 'COMMAND_FAILED' };
}

function runAfterAck(afterAck: (() => void) | undefined, onStatus: RemoteCommandServiceOptions['onStatus']): void {
  if (!afterAck) {
    return;
  }

  try {
    afterAck();
  } catch (error) {
    onStatus?.('post-ack-failed', formatError(error));
  }
}

function shouldRetryCommand(command: string, result: CommandHandleResult): boolean {
  if (result.handled || result.errorCode !== 'COMMAND_EXCEPTION') {
    return false;
  }

  return command.trim().toLowerCase().startsWith('update');
}

function formatError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  return String(error);
}
