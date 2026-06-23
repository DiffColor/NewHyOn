export const RUNTIME_HEALTH_FILE_NAME = 'newhyon-tizen-player-health.json';

function resolveDocumentsRoot(): Promise<TizenFile | null> {
  const filesystem = window.tizen?.filesystem;
  if (!filesystem?.resolve) {
    return Promise.resolve(null);
  }

  return new Promise((resolve, reject) => {
    filesystem.resolve?.('documents', resolve, reject, 'rw');
  });
}

function resolveOrCreateHealthFile(root: TizenFile): TizenFile {
  try {
    return root.resolve(RUNTIME_HEALTH_FILE_NAME);
  } catch {
    return root.createFile(RUNTIME_HEALTH_FILE_NAME);
  }
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

export class RuntimeHealthReporter {
  private writing = false;
  private pendingSnapshot: RuntimeHealthSnapshot | null = null;

  async write(snapshot: RuntimeHealthSnapshot): Promise<void> {
    window.NEWHYON_PLAYER_HEALTH = snapshot;
    this.pendingSnapshot = snapshot;
    if (this.writing) {
      return;
    }

    this.writing = true;
    try {
      while (this.pendingSnapshot) {
        const nextSnapshot = this.pendingSnapshot;
        this.pendingSnapshot = null;
        const root = await resolveDocumentsRoot();
        if (!root) {
          continue;
        }

        const file = resolveOrCreateHealthFile(root);
        await writeTextFile(file, `${JSON.stringify(nextSnapshot, null, 2)}\n`);
      }
    } finally {
      this.writing = false;
    }
  }
}
