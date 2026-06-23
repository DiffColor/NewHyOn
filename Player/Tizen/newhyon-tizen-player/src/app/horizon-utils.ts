export interface RethinkEndpoint {
  readonly horizonHost: string;
  readonly secure: boolean;
}

export interface HorizonObservable<T> {
  subscribe(observer: {
    next?: (value: T) => void;
    error?: (error: unknown) => void;
    complete?: () => void;
  }): { unsubscribe?: () => void };
}

export function parseRethinkEndpoint(value: string): RethinkEndpoint {
  const trimmed = value.trim();
  if (!trimmed) {
    throw new Error('데이터서버 주소가 비어 있습니다.');
  }

  const url = new URL(/^https?:\/\//i.test(trimmed) ? trimmed : `http://${trimmed}`);
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

export function waitForObservable<T>(
  observable: HorizonObservable<T>,
  timeoutMs: number,
  errorMessage: string,
): Promise<T | null> {
  return new Promise((resolve, reject) => {
    let settled = false;
    let lastValue: T | null = null;
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
        lastValue = value ?? null;
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
        resolve(lastValue);
      },
    });
  });
}

export function formatRethinkTimestamp(date = new Date()): string {
  const year = date.getFullYear();
  const month = pad2(date.getMonth() + 1);
  const day = pad2(date.getDate());
  const hour = pad2(date.getHours());
  const minute = pad2(date.getMinutes());
  const second = pad2(date.getSeconds());
  return `${year}-${month}-${day} ${hour}:${minute}:${second}`;
}

function pad2(value: number): string {
  return String(value).padStart(2, '0');
}
