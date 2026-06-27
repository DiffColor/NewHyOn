export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

export interface LogEntry {
  readonly timestamp: string;
  readonly level: LogLevel;
  readonly scope: string;
  readonly message: string;
}

export class RingLogger {
  private readonly entries: LogEntry[] = [];
  private readonly subscribers = new Set<(entries: readonly LogEntry[]) => void>();

  constructor(private readonly capacity = 100) {}

  subscribe(listener: (entries: readonly LogEntry[]) => void): () => void {
    this.subscribers.add(listener);
    listener(this.entries);
    return () => {
      this.subscribers.delete(listener);
    };
  }

  snapshot(limit = this.capacity): readonly LogEntry[] {
    return this.entries.slice(Math.max(0, this.entries.length - limit));
  }

  debug(scope: string, message: string): void {
    this.push('debug', scope, message);
  }

  info(scope: string, message: string): void {
    this.push('info', scope, message);
  }

  warn(scope: string, message: string): void {
    this.push('warn', scope, message);
  }

  error(scope: string, message: string): void {
    this.push('error', scope, message);
  }

  private push(level: LogLevel, scope: string, message: string): void {
    const entry = {
      timestamp: new Date().toISOString(),
      level,
      scope,
      message,
    };

    this.entries.push(entry);
    if (this.entries.length > this.capacity) {
      this.entries.splice(0, this.entries.length - this.capacity);
    }

    const output = `[${entry.timestamp}] [${entry.level}] [${entry.scope}] ${entry.message}`;
    if (level === 'error') {
      console.error(output);
    } else if (level === 'warn') {
      console.warn(output);
    } else {
      console.log(output);
    }

    for (const subscriber of this.subscribers) {
      subscriber(this.entries);
    }
  }
}
