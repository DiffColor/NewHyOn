import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  enableDurableLocalStorageMirror,
  flushDurableLocalStorage,
  restoreDurableLocalStorage,
} from '../src/app/durable-local-storage';

class MemoryStorage implements Storage {
  private readonly values = new Map<string, string>();

  get length(): number {
    return this.values.size;
  }

  clear(): void {
    this.values.clear();
  }

  getItem(key: string): string | null {
    return this.values.get(key) ?? null;
  }

  key(index: number): string | null {
    return Array.from(this.values.keys())[index] ?? null;
  }

  removeItem(key: string): void {
    this.values.delete(key);
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value);
  }
}

function createTizenFile(textToRead: string, onWrite: (text: string) => void): TizenFile {
  return {
    resolve: vi.fn(),
    createFile: vi.fn(),
    openStream: vi.fn((mode, onsuccess) => {
      onsuccess({
        bytesAvailable: textToRead.length,
        read: vi.fn(() => textToRead),
        write: vi.fn(onWrite),
        close: vi.fn(),
      });
    }),
  };
}

describe('durable-local-storage', () => {
  afterEach(() => {
    vi.useRealTimers();
    window.tizen = undefined;
  });

  it('documents durable 파일에서 NewHyOn localStorage 값을 복원한다', async () => {
    const storage = new MemoryStorage();
    const file = createTizenFile(JSON.stringify({
      schemaVersion: 1,
      updatedAt: '2026-06-26T00:00:00.000Z',
      values: {
        'newhyon-tizen-player.settings.v1': '{"playerId":"restored"}',
        'unrelated': 'ignored',
      },
    }), () => undefined);
    const root = {
      resolve: vi.fn(() => file),
      createFile: vi.fn(),
      openStream: vi.fn(),
    };
    window.tizen = {
      filesystem: {
        toURI: (path) => path,
        resolve: vi.fn((_location, onsuccess) => onsuccess(root)),
      },
    };

    await expect(restoreDurableLocalStorage(storage)).resolves.toBe(true);

    expect(storage.getItem('newhyon-tizen-player.settings.v1')).toBe('{"playerId":"restored"}');
    expect(storage.getItem('unrelated')).toBeNull();
  });

  it('NewHyOn localStorage 변경을 documents durable 파일로 쓴다', async () => {
    vi.useFakeTimers();
    const storage = new MemoryStorage();
    let writtenText = '';
    const file = createTizenFile('', (text) => {
      writtenText = text;
    });
    const root = {
      resolve: vi.fn(() => {
        throw new Error('not found');
      }),
      createFile: vi.fn(() => file),
      openStream: vi.fn(),
    };
    window.tizen = {
      filesystem: {
        toURI: (path) => path,
        resolve: vi.fn((_location, onsuccess) => onsuccess(root)),
      },
    };

    enableDurableLocalStorageMirror(storage);
    storage.setItem('newhyon-tizen-player.settings.v1', '{"playerId":"saved"}');
    await flushDurableLocalStorage();

    expect(writtenText).toContain('"schemaVersion": 1');
    expect(writtenText).toContain('"newhyon-tizen-player.settings.v1": "{\\"playerId\\":\\"saved\\"}"');
    expect(root.createFile).toHaveBeenCalledWith('newhyon-tizen-player-data.json');
  });
});
