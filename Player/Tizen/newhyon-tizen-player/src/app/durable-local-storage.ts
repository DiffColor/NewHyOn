const DURABLE_STORAGE_FILE_NAME = 'newhyon-tizen-player-data.json';
const DURABLE_STORAGE_KEY_PREFIX = 'newhyon-tizen-player';

interface DurableStorageSnapshot {
  readonly schemaVersion: 1;
  readonly updatedAt: string;
  readonly values: Record<string, string>;
}

type StorageMethodSetItem = Storage['setItem'];
type StorageMethodRemoveItem = Storage['removeItem'];
type StorageMethodClear = Storage['clear'];

let mirrorEnabled = false;
let suppressMirrorWrite = false;
let writeTimerId: number | null = null;
let writeInProgress = false;
let writeRequested = false;
let mirroredStorage: Storage | null = null;

function isDurableStorageKey(key: string): boolean {
  return key === DURABLE_STORAGE_KEY_PREFIX || key.startsWith(DURABLE_STORAGE_KEY_PREFIX);
}

function collectDurableValues(storage: Storage): Record<string, string> {
  const values: Record<string, string> = {};
  for (let index = 0; index < storage.length; index += 1) {
    const key = storage.key(index);
    if (!key || !isDurableStorageKey(key)) {
      continue;
    }

    const value = storage.getItem(key);
    if (value !== null) {
      values[key] = value;
    }
  }

  return values;
}

function parseSnapshot(text: string): DurableStorageSnapshot | null {
  if (!text.trim()) {
    return null;
  }

  try {
    const parsed = JSON.parse(text) as Partial<DurableStorageSnapshot>;
    if (parsed.schemaVersion === 1 && parsed.values && typeof parsed.values === 'object') {
      return {
        schemaVersion: 1,
        updatedAt: typeof parsed.updatedAt === 'string' ? parsed.updatedAt : '',
        values: Object.fromEntries(
          Object.entries(parsed.values).filter(([key, value]) => isDurableStorageKey(key) && typeof value === 'string'),
        ),
      };
    }
  } catch {
  }

  return null;
}

function resolveDocumentsRoot(): Promise<TizenFile | null> {
  const filesystem = window.tizen?.filesystem;
  if (!filesystem?.resolve) {
    return Promise.resolve(null);
  }

  return new Promise((resolve) => {
    filesystem.resolve?.('documents', resolve, () => resolve(null), 'rw');
  });
}

function resolveDurableFile(root: TizenFile): TizenFile | null {
  try {
    return root.resolve(DURABLE_STORAGE_FILE_NAME);
  } catch {
    return null;
  }
}

function resolveOrCreateDurableFile(root: TizenFile): TizenFile {
  return resolveDurableFile(root) ?? root.createFile(DURABLE_STORAGE_FILE_NAME);
}

function readTextFile(file: TizenFile): Promise<string> {
  return new Promise((resolve, reject) => {
    file.openStream(
      'r',
      (stream) => {
        try {
          const readableStream = stream as TizenFileStream & {
            read?: (count: number) => string;
            readonly bytesAvailable?: number;
          };
          const text = readableStream.read?.(readableStream.bytesAvailable ?? 1024 * 1024) ?? '';
          stream.close();
          resolve(text);
        } catch (error) {
          reject(error);
        }
      },
      reject,
      'UTF-8',
    );
  });
}

function writeTextFile(file: TizenFile, text: string): Promise<void> {
  return new Promise((resolve, reject) => {
    file.openStream(
      'w',
      (stream) => {
        try {
          stream.write(text);
          stream.close();
          resolve();
        } catch (error) {
          reject(error);
        }
      },
      reject,
      'UTF-8',
    );
  });
}

async function writeDurableSnapshot(storage: Storage): Promise<void> {
  const root = await resolveDocumentsRoot();
  if (!root) {
    return;
  }

  const snapshot: DurableStorageSnapshot = {
    schemaVersion: 1,
    updatedAt: new Date().toISOString(),
    values: collectDurableValues(storage),
  };
  await writeTextFile(resolveOrCreateDurableFile(root), `${JSON.stringify(snapshot, null, 2)}\n`);
}

function scheduleDurableSnapshotWrite(storage: Storage): void {
  if (suppressMirrorWrite) {
    return;
  }

  mirroredStorage = storage;
  writeRequested = true;
  if (writeTimerId !== null) {
    window.clearTimeout(writeTimerId);
  }

  writeTimerId = window.setTimeout(() => {
    writeTimerId = null;
    void flushDurableLocalStorage();
  }, 100);
}

export async function restoreDurableLocalStorage(storage: Storage = window.localStorage): Promise<boolean> {
  const root = await resolveDocumentsRoot();
  const file = root ? resolveDurableFile(root) : null;
  if (!file) {
    return false;
  }

  const snapshot = parseSnapshot(await readTextFile(file));
  if (!snapshot) {
    return false;
  }

  suppressMirrorWrite = true;
  try {
    Object.entries(snapshot.values).forEach(([key, value]) => {
      storage.setItem(key, value);
    });
  } finally {
    suppressMirrorWrite = false;
  }

  return true;
}

export function enableDurableLocalStorageMirror(storage: Storage = window.localStorage): void {
  if (mirrorEnabled) {
    return;
  }

  mirrorEnabled = true;
  mirroredStorage = storage;
  const prototype = Object.getPrototypeOf(storage) as Storage;
  const originalSetItem: StorageMethodSetItem = prototype.setItem;
  const originalRemoveItem: StorageMethodRemoveItem = prototype.removeItem;
  const originalClear: StorageMethodClear = prototype.clear;

  prototype.setItem = function setItem(key: string, value: string): void {
    originalSetItem.call(this, key, value);
    if (this === storage && isDurableStorageKey(key)) {
      scheduleDurableSnapshotWrite(storage);
    }
  };

  prototype.removeItem = function removeItem(key: string): void {
    originalRemoveItem.call(this, key);
    if (this === storage && isDurableStorageKey(key)) {
      scheduleDurableSnapshotWrite(storage);
    }
  };

  prototype.clear = function clear(): void {
    const hadDurableValues = collectDurableValues(storage);
    originalClear.call(this);
    if (this === storage && Object.keys(hadDurableValues).length > 0) {
      scheduleDurableSnapshotWrite(storage);
    }
  };

  scheduleDurableSnapshotWrite(storage);
}

export async function flushDurableLocalStorage(): Promise<void> {
  if (!mirroredStorage) {
    return;
  }

  if (writeTimerId !== null) {
    window.clearTimeout(writeTimerId);
    writeTimerId = null;
  }

  writeRequested = true;
  if (writeInProgress) {
    return;
  }

  writeInProgress = true;
  try {
    while (writeRequested && mirroredStorage) {
      writeRequested = false;
      await writeDurableSnapshot(mirroredStorage);
    }
  } finally {
    writeInProgress = false;
  }
}
