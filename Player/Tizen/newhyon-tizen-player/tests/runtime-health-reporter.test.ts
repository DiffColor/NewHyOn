import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RuntimeHealthReporter, RUNTIME_HEALTH_FILE_NAME } from '../src/app/runtime-health-reporter';

function createSnapshot(stage: string): RuntimeHealthSnapshot {
  return {
    schemaVersion: 1,
    updatedAt: '2026-06-21T03:00:00.000Z',
    stage,
    state: 'playing',
    playlist: 'Default',
    page: '1/1 page',
    elapsed: '0.0s / 10s',
    lastKey: '-',
    lastAction: '-',
    platform: 'webapis=OK avplay=OK',
    slots: ['slot: video.mp4 (PLAYING)'],
    message: '페이지 재생',
    recentLogs: [],
    diagnostics: {
      pageStartCount: 1,
      contentShowCount: 1,
      lastContent: 'slot 1: video.mp4',
      masterTickDelayMs: 0,
      masterTickIntervalMs: 200,
      renderIntervalMs: 0,
      communicationStatus: 'connected',
      dbStatus: 'connected',
      dbStatusDetail: 'http://10.0.0.10',
      signalrStatus: 'connected',
      signalrStatusDetail: 'http://10.0.0.11:5000/Data',
      ftpStatus: 'connected',
      ftpStatusDetail: '10.0.0.10:10021',
      heartbeatStatus: 'sent',
      heartbeatStatusDetail: 'playing page',
      authStatus: 'authenticated',
      authStatusDetail: 'ONLINE server-valid',
      updateStatus: '대기',
      updatePhase: 'idle',
      updateProgress: 0,
      updateCompleted: 0,
      updateTotal: 0,
      updateCurrentFile: '-',
      updateCommandId: '-',
      playerGuid: 'player-guid-1',
      playerName: 'tizen',
      dataServerAddress: '10.0.0.10',
      messageServerAddress: '10.0.0.11',
      signalrUrl: 'http://10.0.0.11:5000/Data',
      ftpEndpoint: '10.0.0.10:10021/NewHyOnEnt',
    },
    settings: {
      preserveAspectRatio: true,
      switchOnContentEnd: true,
      defaultVolume: 60,
      hudInitiallyVisible: true,
    },
  };
}

describe('RuntimeHealthReporter', () => {
  beforeEach(() => {
    window.NEWHYON_PLAYER_HEALTH = undefined;
    window.tizen = undefined;
  });

  it('Tizen 파일시스템이 없어도 window health snapshot은 기록한다', async () => {
    const reporter = new RuntimeHealthReporter();
    const snapshot = createSnapshot('app-started');

    await reporter.write(snapshot);

    expect(window.NEWHYON_PLAYER_HEALTH).toEqual(snapshot);
  });

  it('documents 루트에 health json을 쓴다', async () => {
    const written: string[] = [];
    const close = vi.fn();
    const stream: TizenFileStream = {
      write: vi.fn((text: string) => written.push(text)),
      close,
    };
    const healthFile: TizenFile = {
      resolve: vi.fn(),
      createFile: vi.fn(),
      openStream: vi.fn((_mode, onsuccess) => onsuccess(stream)),
    };
    const documentsRoot: TizenFile = {
      resolve: vi.fn((name: string) => {
        expect(name).toBe(RUNTIME_HEALTH_FILE_NAME);
        return healthFile;
      }),
      createFile: vi.fn(),
      openStream: vi.fn(),
    };
    const resolve = vi.fn((_location, onsuccess) => onsuccess(documentsRoot));
    window.tizen = {
      filesystem: {
        toURI: vi.fn(),
        resolve,
      },
    };

    const reporter = new RuntimeHealthReporter();
    await reporter.write(createSnapshot('page-started'));

    expect(resolve).toHaveBeenCalledWith('documents', expect.any(Function), expect.any(Function), 'rw');
    expect(healthFile.openStream).toHaveBeenCalledWith('w', expect.any(Function), expect.any(Function), 'UTF-8');
    expect(written.join('')).toContain('"stage": "page-started"');
    expect(close).toHaveBeenCalledTimes(1);
  });
});
