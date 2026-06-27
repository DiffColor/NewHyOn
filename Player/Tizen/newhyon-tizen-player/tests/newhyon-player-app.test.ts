import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { NewHyOnPlayerApp } from '../src/app/newhyon-player-app';
import { hasContentPeriod, saveContentPeriodsFromSchedule } from '../src/app/content-period';
import { DEFAULT_PLAYER_SETTINGS } from '../src/app/player-settings';
import { loadRemoteManifest, type UpdatePayload } from '../src/app/update-payload';
import { loadRemoteSchedule, saveRemoteScheduleFromUpdatePayload } from '../src/app/remote-schedule';
import { getDefaultWeeklySchedule, saveWeeklySchedule } from '../src/app/weekly-schedule';
import type { LicenseAuthState, LicenseHubAuthService } from '../src/app/licensehub-auth';
import type { PlayerManifest } from '../src/domain/models';

function renderAppShell(): void {
  document.body.innerHTML = `
    <main id="stage"></main>
    <aside id="broadcast-standby" class="broadcast-standby--hidden" aria-hidden="true"></aside>
    <aside id="loading-overlay">
      <strong id="loading-title"></strong>
      <span id="loading-message"></span>
      <dd id="loading-db-status"></dd>
      <dd id="loading-signalr-status"></dd>
      <dd id="loading-ftp-status"></dd>
      <dd id="loading-auth-status"></dd>
    </aside>
    <aside id="debug-hud" class="debug-hud--hidden">
      <dd id="status-state"></dd>
      <dd id="status-playlist"></dd>
      <dd id="status-page"></dd>
      <dd id="status-elapsed"></dd>
      <dd id="status-last-key"></dd>
      <dd id="status-last-action"></dd>
      <dd id="status-platform"></dd>
      <dd id="status-communication"></dd>
      <dd id="status-auth"></dd>
      <dd id="status-update"></dd>
      <dd id="status-slots"></dd>
      <dd id="status-timeline"></dd>
      <dd id="status-message"></dd>
      <pre id="log-output"></pre>
    </aside>
  `;
}

function createPlayer(play: () => void, onListener?: (listener: AVPlayListener) => void): AVPlayApi {
  let listener: AVPlayListener | null = null;
  return {
    open: vi.fn(),
    prepare: vi.fn(),
    prepareAsync: vi.fn((successCallback: () => void) => successCallback()),
    play: vi.fn(() => {
      play();
      listener?.oncurrentplaytime?.(0);
    }),
    pause: vi.fn(),
    stop: vi.fn(),
    close: vi.fn(),
    setListener: vi.fn((nextListener) => {
      listener = nextListener;
      onListener?.(nextListener);
    }),
    setDisplayRect: vi.fn(),
    setDisplayMethod: vi.fn(),
    setVideoStillMode: vi.fn(),
    setTimeoutForBuffering: vi.fn(),
    setLooping: vi.fn(),
    getState: vi.fn(() => 'IDLE'),
  };
}

function createWebApis(
  avplay: AVPlayApi,
  setPanelMute = vi.fn(),
  avplaystore: AVPlayStoreManager | null | undefined = { getPlayer: () => avplay },
): WebApisGlobal {
  let panelMute: 'ON' | 'OFF' = 'OFF';
  let messageDisplay: 'ON' | 'OFF' = 'ON';
  let remoteConfiguration: 'ON' | 'OFF' = 'OFF';

  return {
    avplay,
    avplaystore: avplaystore ?? undefined,
    systemcontrol: {
      setPanelMute: (state) => {
        panelMute = state;
        setPanelMute(state);
      },
      getPanelMute: () => panelMute,
      setMessageDisplay: (state) => {
        messageDisplay = state;
      },
      getMessageDisplay: () => messageDisplay,
      rebootDevice: vi.fn(),
    },
    remotepower: {
      powerOff: vi.fn(),
      setRemoteConfiguration: (state) => {
        remoteConfiguration = state;
      },
      getRemoteConfiguration: () => remoteConfiguration,
    },
  };
}

function createManifest(): PlayerManifest {
  return {
    playlistName: 'playlist',
    preserveAspectRatio: false,
    pages: [
      {
        PIC_PageName: 'page',
        PIC_PlaytimeSecond: 10,
        PIC_CanvasWidth: 1920,
        PIC_CanvasHeight: 1080,
        PIC_Elements: [
          {
            EIF_Name: 'video',
            EIF_Type: 'Media',
            EIF_Width: 1920,
            EIF_Height: 1080,
            EIF_PosLeft: 0,
            EIF_PosTop: 0,
            EIF_IsMuted: true,
            EIF_ContentsInfoClassList: [
              {
                CIF_FileName: 'video.mp4',
                CIF_FileFullPath: 'https://example.com/video.mp4',
                CIF_ContentType: 'Video',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '10',
              },
            ],
          },
        ],
      },
    ],
  };
}

function createTwoPageManifest(): PlayerManifest {
  const first = createManifest().pages[0]!;
  return {
    playlistName: 'playlist',
    preserveAspectRatio: false,
    pages: [
      {
        ...first,
        PIC_PageName: 'first-page',
      },
      {
        ...first,
        PIC_PageName: 'second-page',
        PIC_Elements: first.PIC_Elements?.map((element) => ({
          ...element,
          EIF_Name: 'second-video',
          EIF_ContentsInfoClassList: element.EIF_ContentsInfoClassList?.map((content) => ({
            ...content,
            CIF_FileName: 'second.mp4',
            CIF_FileFullPath: 'https://example.com/second.mp4',
          })),
        })),
      },
    ],
  };
}

function createTwoPageNextImageManifest(): PlayerManifest {
  const manifest = createTwoPageManifest();
  manifest.pages[1] = {
    ...manifest.pages[1]!,
    PIC_Elements: [
      {
        EIF_Name: 'second-image',
        EIF_Type: 'Media',
        EIF_Width: 1920,
        EIF_Height: 1080,
        EIF_PosLeft: 0,
        EIF_PosTop: 0,
        EIF_IsMuted: true,
        EIF_ContentsInfoClassList: [
          {
            CIF_FileName: 'second.png',
            CIF_FileFullPath: 'https://example.com/second.png',
            CIF_ContentType: 'Image',
            CIF_PlayMinute: '00',
            CIF_PlaySec: '10',
          },
        ],
      },
    ],
  };
  return manifest;
}

function createPagePriorityManifest(): PlayerManifest {
  const manifest = createTwoPageManifest();
  const firstPage = manifest.pages[0]!;
  firstPage.PIC_PlaytimeSecond = 10;
  firstPage.PIC_Elements = firstPage.PIC_Elements?.map((element) => ({
    ...element,
    EIF_ContentsInfoClassList: [
      {
        CIF_FileName: 'first-a.mp4',
        CIF_FileFullPath: 'https://example.com/first-a.mp4',
        CIF_ContentType: 'Video',
        CIF_PlayMinute: '00',
        CIF_PlaySec: '10',
      },
      {
        CIF_FileName: 'first-b.mp4',
        CIF_FileFullPath: 'https://example.com/first-b.mp4',
        CIF_ContentType: 'Video',
        CIF_PlayMinute: '00',
        CIF_PlaySec: '10',
      },
    ],
  }));
  return manifest;
}

function createSinglePageImageManifest(): PlayerManifest {
  return {
    playlistName: 'playlist',
    preserveAspectRatio: false,
    pages: [
      {
        PIC_PageName: 'image-page',
        PIC_PlaytimeSecond: 10,
        PIC_CanvasWidth: 1920,
        PIC_CanvasHeight: 1080,
        PIC_Elements: [
          {
            EIF_Name: 'images',
            EIF_Type: 'Media',
            EIF_Width: 1920,
            EIF_Height: 1080,
            EIF_PosLeft: 0,
            EIF_PosTop: 0,
            EIF_IsMuted: true,
            EIF_ContentsInfoClassList: [
              {
                CIF_FileName: 'first.png',
                CIF_FileFullPath: 'https://example.com/first.png',
                CIF_ContentType: 'Image',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '03',
              },
              {
                CIF_FileName: 'second.png',
                CIF_FileFullPath: 'https://example.com/second.png',
                CIF_ContentType: 'Image',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '03',
              },
            ],
          },
        ],
      },
    ],
  };
}

function validAuthState(mode: 'ONLINE' | 'OFFLINE' = 'ONLINE'): LicenseAuthState {
  return {
    isValid: true,
    mode,
    status: mode === 'ONLINE' ? 'server-valid' : 'offline-verified',
    reason: '',
    deviceFingerprint: 'fingerprint-1',
    deviceId: 'device-1',
    licenseToken: mode === 'ONLINE' ? 'token-1' : '',
    serverChecked: mode === 'ONLINE',
    usedOfflineFallback: mode === 'OFFLINE',
  };
}

describe('NewHyOnPlayerApp', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
    window.localStorage.clear();
  });

  beforeEach(() => {
    renderAppShell();
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue(undefined);
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => undefined);
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => undefined);
    window.webapis = createWebApis(createPlayer(() => undefined));
    window.tizen = undefined;
  });

  it('재생 가능한 콘텐츠가 없으면 Tizen 인트로 영상을 재생한다', async () => {
    const open = vi.fn();
    const avplayPlay = vi.fn();
    window.webapis = createWebApis({
      ...createPlayer(avplayPlay),
      open,
    });

    const app = new NewHyOnPlayerApp({
      manifest: {
        playlistName: 'empty',
        preserveAspectRatio: false,
        pages: [],
      },
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();

    expect(document.querySelector('#status-playlist')?.textContent).toBe('Tizen Intro');
    const introVideo = document.querySelector<HTMLVideoElement>('.empty-intro-video');
    expect(introVideo).not.toBeNull();
    expect(introVideo?.getAttribute('src')).toBe('media/intro.mp4');
    expect(introVideo?.loop).toBe(true);
    expect(introVideo?.muted).toBe(true);
    expect(open).not.toHaveBeenCalled();
    expect(avplayPlay).not.toHaveBeenCalled();
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledTimes(1);
    app.destroy();
  });

  it('콘텐츠 데이터는 있지만 현재 콘텐츠 기간 밖이면 인트로가 아니라 검은 화면을 유지한다', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 5, 22, 9, 1, 0));
    const play = vi.fn();
    window.webapis = createWebApis(createPlayer(play));
    saveContentPeriodsFromSchedule({
      ContentPeriods: [
        {
          ContentGuid: 'default-period-content',
          StartDate: '2026-06-20',
          EndDate: '2026-06-21',
          StartTime: '00:00',
          EndTime: '23:59',
        },
      ],
    });
    const manifest = createManifest();
    manifest.pages[0]!.PIC_Elements![0]!.EIF_ContentsInfoClassList![0] = {
      ...manifest.pages[0]!.PIC_Elements![0]!.EIF_ContentsInfoClassList![0]!,
      CIF_StrGUID: 'default-period-content',
    };

    const app = new NewHyOnPlayerApp({
      manifest,
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();

    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');
    expect(document.querySelector('#status-page')?.textContent).toContain('No active content');
    expect(document.querySelector('.empty-intro-video')).toBeNull();
    expect(document.querySelectorAll('.slot')).toHaveLength(0);
    expect(play).not.toHaveBeenCalled();
    app.destroy();
  });

  it('오버레이에는 서버 주소, URL, 계정 정보를 표시하지 않는다', () => {
    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });
    const mutable = app as unknown as {
      dbStatus: string;
      dbStatusDetail: string;
      signalrStatus: string;
      signalrStatusDetail: string;
      ftpStatus: string;
      ftpStatusDetail: string;
      heartbeatStatus: string;
      heartbeatStatusDetail: string;
      updateOverlayState: {
        phase: string;
        playlistName: string;
        commandId: string;
        completed: number;
        total: number;
        progress: number;
        currentFile: string;
        detail: string;
      };
      bindUi(): void;
      render(): void;
    };
    mutable.bindUi();
    mutable.dbStatus = 'connected';
    mutable.dbStatusDetail = '10.0.0.10:8181';
    mutable.signalrStatus = 'connected';
    mutable.signalrStatusDetail = 'http://10.0.0.30:5000/Data?playerGuid=guid-1';
    mutable.ftpStatus = 'connected';
    mutable.ftpStatusDetail = '10.0.0.20:10022/MediaRoot';
    mutable.heartbeatStatus = 'failed';
    mutable.heartbeatStatusDetail = 'ws://10.0.0.30:5000/Data?id=token-1';
    mutable.updateOverlayState = {
      phase: 'failed',
      playlistName: 'playlist',
      commandId: 'command-1',
      completed: 0,
      total: 1,
      progress: 0,
      currentFile: 'video.mp4',
      detail: 'ftp://ftp-user:ftp-pass@10.0.0.20:21/MediaRoot/video.mp4 password=ftp-pass',
    };

    mutable.render();

    const communicationText = document.querySelector('#status-communication')?.textContent ?? '';
    expect(communicationText).toContain('DB: 연결됨');
    expect(communicationText).toContain('SignalR: 연결됨');
    expect(communicationText).toContain('FTP: 연결됨');
    expect(communicationText).not.toMatch(/10\.0\.0|http:\/\/|ws:\/\//);

    const updateText = document.querySelector('#status-update')?.textContent ?? '';
    expect(updateText).toContain('[endpoint]');
    expect(updateText).not.toMatch(/10\.0\.0|ftp:\/\/|ftp-user|ftp-pass|MediaRoot/);

    (app as unknown as {
      logger: { info(scope: string, message: string): void };
    }).logger.info('heartbeat', 'http://10.0.0.30:5000/Data password=ftp-pass');
    const logText = document.querySelector('#log-output')?.textContent ?? '';
    expect(logText).toContain('[endpoint]');
    expect(logText).not.toMatch(/10\.0\.0|http:\/\/|ftp-pass/);
    app.destroy();
  });

  it('콘텐츠 없음 인트로 영상은 HTML video loop로 같은 요소를 계속 재생한다', async () => {
    vi.useFakeTimers();
    const open = vi.fn();
    const avplayPlay = vi.fn();
    window.webapis = createWebApis({
      ...createPlayer(avplayPlay),
      open,
    });

    const app = new NewHyOnPlayerApp({
      manifest: {
        playlistName: 'empty',
        preserveAspectRatio: false,
        pages: [],
      },
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    const introVideo = document.querySelector<HTMLVideoElement>('.empty-intro-video');
    expect(introVideo).not.toBeNull();
    expect(introVideo?.loop).toBe(true);
    expect(open).not.toHaveBeenCalled();
    expect(avplayPlay).not.toHaveBeenCalled();
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(15000);
    await Promise.resolve();

    expect(document.querySelector('.empty-intro-video')).toBe(introVideo);
    expect(open).not.toHaveBeenCalled();
    expect(avplayPlay).not.toHaveBeenCalled();
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledTimes(1);
    app.destroy();
  });

  it('정지 후 Play/Pause를 누르면 현재 페이지를 다시 시작한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn();
    window.webapis = createWebApis(createPlayer(play));

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    expect(play).toHaveBeenCalledTimes(1);

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'MediaStop' }));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'MediaPlayPause' }));
    await Promise.resolve();
    await Promise.resolve();

    expect(play).toHaveBeenCalledTimes(2);
    expect(document.querySelector('#status-message')?.textContent).toBe('페이지 재생: page');
    app.destroy();
  });

  it('미인증 상태로 전환되면 기존 콘텐츠를 정리하고 Tizen 인트로만 반복 재생한다', async () => {
    const play = vi.fn();
    const open = vi.fn();
    window.webapis = createWebApis({
      ...createPlayer(play),
      open,
    });

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');
    expect(document.querySelector('.slot')).not.toBeNull();
    expect(play).toHaveBeenCalledTimes(1);

    await (app as unknown as {
      enterUnauthenticatedIntroPlayback(detail: string, startIfOnAir: boolean): Promise<void>;
    }).enterUnauthenticatedIntroPlayback('사용자가 LicenseHub 인증을 취소했습니다.', true);

    const introVideo = document.querySelector<HTMLVideoElement>('.empty-intro-video');
    expect(document.querySelector('#status-playlist')?.textContent).toBe('Tizen Intro');
    expect(document.querySelector('#status-auth')?.textContent).toContain('unauthenticated');
    expect(document.querySelector('.slot')).toBeNull();
    expect(introVideo).not.toBeNull();
    expect(introVideo?.loop).toBe(true);
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledTimes(1);
    expect(open).toHaveBeenCalledTimes(1);
    app.destroy();
  });

  it('인증창을 취소해도 현재 기기 인증이 유효하면 인트로가 아니라 콘텐츠를 재생한다', async () => {
    const play = vi.fn();
    const open = vi.fn();
    window.webapis = createWebApis({
      ...createPlayer(play),
      open,
    });

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    await (app as unknown as {
      enterUnauthenticatedIntroPlayback(detail: string, startIfOnAir: boolean): Promise<void>;
    }).enterUnauthenticatedIntroPlayback('LicenseHub 인증이 완료되지 않았습니다.', true);
    expect(document.querySelector('#status-playlist')?.textContent).toBe('Tizen Intro');
    expect(document.querySelector('.empty-intro-video')).not.toBeNull();

    const validateStoredOrBootstrap = vi.fn(async () => validAuthState('ONLINE'));
    const appInternals = app as unknown as {
      authService: LicenseHubAuthService;
      handleAuthenticationCancellation(detail: string, startIfOnAir: boolean): Promise<void>;
    };
    appInternals.authService = {
      validateStoredOrBootstrap,
    } as unknown as LicenseHubAuthService;

    await appInternals.handleAuthenticationCancellation('사용자가 LicenseHub 인증을 취소했습니다.', true);

    expect(validateStoredOrBootstrap).toHaveBeenCalledTimes(1);
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');
    expect(document.querySelector('#status-auth')?.textContent).toContain('authenticated');
    expect(document.querySelector('.empty-intro-video')).toBeNull();
    expect(document.querySelector('.slot')).not.toBeNull();
    expect(open).toHaveBeenCalledTimes(2);
    expect(play).toHaveBeenCalledTimes(2);
    app.destroy();
  });

  it('HUD 로그창은 최근 입력 로그를 위에 표시한다', async () => {
    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'B' }));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: '0' }));

    const logLines = document.querySelector('#log-output')?.textContent?.split('\n') ?? [];
    const openSettingsIndex = logLines.findIndex((line) => line.includes('0 -> open-settings'));
    const toggleHudIndex = logLines.findIndex((line) => line.includes('B -> toggle-hud'));

    expect(openSettingsIndex).toBeGreaterThanOrEqual(0);
    expect(toggleHudIndex).toBeGreaterThanOrEqual(0);
    expect(openSettingsIndex).toBeLessThan(toggleHudIndex);
    app.destroy();
  });

  it('일시정지 상태의 HUD 경과 시간을 멈춘 시점으로 유지한다', async () => {
    vi.useFakeTimers();
    window.webapis = createWebApis(createPlayer(() => undefined));

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    await vi.advanceTimersByTimeAsync(3500);
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'MediaPlayPause' }));

    expect(document.querySelector('#status-state')?.textContent).toBe('paused');
    expect(document.querySelector('#status-elapsed')?.textContent).toBe('3.5s / 10.0s');
    app.destroy();
  });

  it('단일 페이지 만료 시 현재 페이지를 다시 만들지 않고 루프를 유지한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn();
    const stop = vi.fn();
    const close = vi.fn();
    window.webapis = createWebApis({
      ...createPlayer(play),
      stop,
      close,
    });

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    expect(play).toHaveBeenCalledTimes(1);
    stop.mockClear();
    close.mockClear();

    await vi.advanceTimersByTimeAsync(10000);
    await Promise.resolve();

    expect(play).toHaveBeenCalledTimes(1);
    expect(stop).not.toHaveBeenCalled();
    expect(close).not.toHaveBeenCalled();
    expect(document.querySelector('#status-elapsed')?.textContent).toBe('0.0s / 10.0s');

    await vi.advanceTimersByTimeAsync(1200);
    expect(document.querySelector('#status-elapsed')?.textContent).toBe('1.2s / 10.0s');
    app.destroy();
  });

  it('페이지 만료 전에는 다음 페이지 AVPlay를 미리 건드리지 않고 만료 후 전환한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn();
    const players: AVPlayApi[] = [];
    const getPlayer = vi.fn(() => {
      const player = {
        ...createPlayer(play),
        getState: vi.fn(() => 'PLAYING'),
      };
      players.push(player);
      return player;
    });
    window.webapis = createWebApis(createPlayer(play), vi.fn(), { getPlayer });

    const app = new NewHyOnPlayerApp({
      manifest: createTwoPageManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    expect(document.querySelector('#status-page')?.textContent).toContain('first-page');
    expect(play).toHaveBeenCalledTimes(1);
    expect(getPlayer).toHaveBeenCalledTimes(2);
    expect(players[0]?.prepareAsync).toHaveBeenCalledTimes(1);
    expect(players[1]?.prepareAsync).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(6499);
    await Promise.resolve();
    expect(document.querySelector('#status-page')?.textContent).toContain('first-page');
    expect(document.querySelectorAll('.slot')).toHaveLength(1);
    expect(play).toHaveBeenCalledTimes(1);
    expect(getPlayer).toHaveBeenCalledTimes(2);
    expect(players[1]?.prepareAsync).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(3501);
    await vi.advanceTimersByTimeAsync(64);
    await Promise.resolve();
    await Promise.resolve();
    expect(document.querySelector('#status-page')?.textContent).toContain('second-page');
    expect(document.querySelectorAll('.slot')).toHaveLength(1);
    expect(play).toHaveBeenCalledTimes(2);
    expect(getPlayer).toHaveBeenCalledTimes(2);
    expect(window.NEWHYON_PLAYER_HEALTH?.diagnostics.pageStartCount).toBe(2);
    app.destroy();
  });

  it('페이지 시작 직후 다음 페이지 첫 이미지를 standby DOM에 opacity 0으로 준비한다', async () => {
    vi.useFakeTimers();
    const loadedSources: string[] = [];
    const srcDescriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    const decodeDescriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'decode');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        loadedSources.push(value);
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });
    Object.defineProperty(HTMLImageElement.prototype, 'decode', {
      configurable: true,
      value: vi.fn(() => Promise.resolve()),
    });

    try {
      const play = vi.fn();
      window.webapis = createWebApis(createPlayer(play), vi.fn(), {
        getPlayer: vi.fn(() => ({
          ...createPlayer(play),
          getState: vi.fn(() => 'PLAYING'),
        })),
      });

      const app = new NewHyOnPlayerApp({
        manifest: createTwoPageNextImageManifest(),
        settings: {
          ...DEFAULT_PLAYER_SETTINGS,
          playerId: '',
          managerAddress: '',
          manifestUrl: '',
          preserveAspectRatio: false,
          switchOnContentEnd: false,
          hudInitiallyVisible: false,
        },
        hudInitiallyVisible: false,
      });

      await app.start();
      expect(document.querySelector('#status-page')?.textContent).toContain('first-page');
      expect(loadedSources.some((source) => source.includes('second.png'))).toBe(true);
      expect(Array.from(document.querySelectorAll<HTMLImageElement>('.slot-image'))
        .some((image) => image.getAttribute('src')?.includes('second.png'))).toBe(true);
      expect(Array.from(document.querySelectorAll<HTMLImageElement>('.slot-image--visible'))
        .some((image) => image.getAttribute('src')?.includes('second.png'))).toBe(false);

      await vi.advanceTimersByTimeAsync(9999);
      await vi.runAllTicks();

      expect(document.querySelector('#status-page')?.textContent).toContain('first-page');
      expect(Array.from(document.querySelectorAll<HTMLImageElement>('.slot-image'))
        .some((image) => image.getAttribute('src')?.includes('second.png'))).toBe(true);
      expect(Array.from(document.querySelectorAll<HTMLImageElement>('.slot-image--visible'))
        .some((image) => image.getAttribute('src')?.includes('second.png'))).toBe(false);

      await vi.advanceTimersByTimeAsync(1);
      await vi.advanceTimersByTimeAsync(96);
      await Promise.resolve();
      await Promise.resolve();

      expect(document.querySelector('#status-page')?.textContent).toContain('second-page');
      app.destroy();
    } finally {
      if (srcDescriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', srcDescriptor);
      }
      if (decodeDescriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'decode', decodeDescriptor);
      } else {
        delete (HTMLImageElement.prototype as Partial<HTMLImageElement>).decode;
      }
    }
  });

  it('페이지 종료 tick이 먼저 와도 영상 종료 이벤트와 함께 다음 페이지로 전환한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn();
    const listeners: AVPlayListener[] = [];
    const getPlayer = vi.fn(() => ({
      ...createPlayer(play, (listener) => {
        listeners.push(listener);
      }),
      getState: vi.fn(() => 'PLAYING'),
    }));
    window.webapis = createWebApis(createPlayer(play), vi.fn(), { getPlayer });

    const app = new NewHyOnPlayerApp({
      manifest: createPagePriorityManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    expect(document.querySelector('#status-page')?.textContent).toContain('first-page');
    expect(play).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(10000);
    expect(document.querySelector('#status-page')?.textContent).toContain('first-page');
    expect(play).toHaveBeenCalledTimes(1);

    listeners[0]?.onstreamcompleted?.();
    await vi.advanceTimersByTimeAsync(32);
    await Promise.resolve();
    await Promise.resolve();

    expect(document.querySelector('#status-page')?.textContent).toContain('second-page');
    expect(play).toHaveBeenCalledTimes(2);
    expect(document.querySelector('#status-slots')?.textContent).toContain('second.mp4');
    app.destroy();
  });

  it('단일 페이지의 다중 이미지 슬롯은 콘텐츠만 전환하고 페이지 DOM은 재생성하지 않는다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const app = new NewHyOnPlayerApp({
        manifest: createSinglePageImageManifest(),
        settings: {
          ...DEFAULT_PLAYER_SETTINGS,
          playerId: '',
          managerAddress: '',
          manifestUrl: '',
          preserveAspectRatio: false,
          switchOnContentEnd: false,
          hudInitiallyVisible: false,
        },
        hudInitiallyVisible: false,
      });

      const startPromise = app.start();
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);
      await startPromise;
      const slotElement = document.querySelector('.slot');
      expect(slotElement).not.toBeNull();
      expect(window.NEWHYON_PLAYER_HEALTH?.diagnostics).toMatchObject({
        pageStartCount: 1,
        contentShowCount: 1,
        lastContent: 'slot 1: first.png',
      });

      await vi.advanceTimersByTimeAsync(3000);
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);
      expect(window.NEWHYON_PLAYER_HEALTH?.diagnostics).toMatchObject({
        pageStartCount: 1,
        contentShowCount: 2,
        lastContent: 'slot 1: second.png',
      });

      await vi.advanceTimersByTimeAsync(7000);
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(96);
      expect(document.querySelector('.slot')).toBe(slotElement);
      expect(window.NEWHYON_PLAYER_HEALTH?.diagnostics.pageStartCount).toBe(1);
      expect(window.NEWHYON_PLAYER_HEALTH?.diagnostics.contentShowCount).toBeGreaterThan(2);
      app.destroy();
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('방송시간 전에는 검은 대기 상태를 유지하고 시작 시간이 되면 재생한다', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 5, 22, 8, 59, 30));
    const rows = getDefaultWeeklySchedule();
    rows[0] = {
      ...rows[0]!,
      isOnAir: true,
      startHour: 9,
      startMinute: 0,
      endHour: 18,
      endMinute: 0,
    };
    saveWeeklySchedule(rows);
    const play = vi.fn();
    const setPanelMute = vi.fn();
    window.webapis = createWebApis(createPlayer(play), setPanelMute);

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();

    expect(play).not.toHaveBeenCalled();
    expect(setPanelMute).toHaveBeenCalledWith('ON');
    expect(document.querySelector('#status-state')?.textContent).toBe('off-air');
    expect(document.querySelector('#broadcast-standby')?.classList.contains('broadcast-standby--hidden')).toBe(false);

    await vi.advanceTimersByTimeAsync(30100);
    await Promise.resolve();
    await Promise.resolve();

    expect(play).toHaveBeenCalledTimes(1);
    expect(setPanelMute).toHaveBeenLastCalledWith('OFF');
    expect(document.querySelector('#status-state')?.textContent).toBe('playing');
    expect(document.querySelector('#broadcast-standby')?.classList.contains('broadcast-standby--hidden')).toBe(true);
    app.destroy();
  });

  it('updateschedule 수신 후 활성 예약 playlist로 전환한다', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 5, 22, 9, 1, 0));
    const play = vi.fn();
    window.webapis = createWebApis(createPlayer(play));
    window.tizen = {
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: (path) => path === 'downloads/scheduled.mp4',
      },
    };

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');

    const schedulePromise = (app as unknown as {
      applyUpdateScheduleCommand(payload: UpdatePayload): Promise<boolean>;
    }).applyUpdateScheduleCommand({
      Schedule: {
        GeneratedAt: '2026-06-22 09:00:00',
        SpecialSchedules: [
          {
            Id: 'schedule-1',
            PageListName: 'scheduled-list',
            DayOfWeek1: false,
            DayOfWeek2: true,
            DayOfWeek3: false,
            DayOfWeek4: false,
            DayOfWeek5: false,
            DayOfWeek6: false,
            DayOfWeek7: false,
            IsPeriodEnable: false,
            DisplayStartH: 9,
            DisplayStartM: 0,
            DisplayEndH: 18,
            DisplayEndM: 0,
          },
        ],
        Playlists: [
          {
            PlaylistName: 'scheduled-list',
            PageList: { PLI_PageListName: 'scheduled-list' },
            Pages: [
              {
                PIC_PageName: 'scheduled-page',
                PIC_PlaytimeSecond: 10,
                PIC_CanvasWidth: 1920,
                PIC_CanvasHeight: 1080,
                PIC_Elements: [
                  {
                    EIF_Name: 'scheduled-video',
                    EIF_Type: 'Media',
                    EIF_Width: 1920,
                    EIF_Height: 1080,
                    EIF_PosLeft: 0,
                    EIF_PosTop: 0,
                    EIF_IsMuted: true,
                    EIF_ContentsInfoClassList: [
                      {
                        CIF_FileName: 'scheduled.mp4',
                        CIF_FileFullPath: 'downloads/scheduled.mp4',
                        CIF_ContentType: 'Video',
                        CIF_PlayMinute: '00',
                        CIF_PlaySec: '10',
                      },
                    ],
                  },
                ],
              },
            ],
          },
        ],
      },
    });
    await vi.advanceTimersByTimeAsync(64);
    await schedulePromise;
    await Promise.resolve();
    await Promise.resolve();

    expect(document.querySelector('#status-playlist')?.textContent).toBe('scheduled-list');
    expect(document.querySelector('#status-page')?.textContent).toContain('scheduled-page');
    expect(document.querySelector('#status-slots')?.textContent).toContain('scheduled.mp4');
    app.destroy();
  });

  it('updateschedule 콘텐츠 캐시가 실패하면 새 예약 스케줄과 기간 데이터를 저장하지 않는다', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 5, 22, 9, 1, 0));
    saveRemoteScheduleFromUpdatePayload({
      Schedule: {
        GeneratedAt: '2026-06-22 08:00:00',
        SpecialSchedules: [],
        Playlists: [],
        ContentPeriods: [],
      },
    });

    const play = vi.fn();
    window.webapis = createWebApis(createPlayer(play));
    window.tizen = undefined;

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');

    await expect((app as unknown as {
      applyUpdateScheduleCommand(payload: UpdatePayload): Promise<boolean>;
    }).applyUpdateScheduleCommand({
      Schedule: {
        GeneratedAt: '2026-06-22 09:00:00',
        ContentPeriods: [
          {
            ContentGuid: 'new-schedule-content',
            StartDate: '2026-06-22',
            EndDate: '2026-06-22',
            StartTime: '00:00',
            EndTime: '23:59',
          },
        ],
        SpecialSchedules: [
          {
            Id: 'schedule-cache-fail',
            PageListName: 'failed-scheduled-list',
            DayOfWeek1: false,
            DayOfWeek2: true,
            DayOfWeek3: false,
            DayOfWeek4: false,
            DayOfWeek5: false,
            DayOfWeek6: false,
            DayOfWeek7: false,
            IsPeriodEnable: false,
            DisplayStartH: 9,
            DisplayStartM: 0,
            DisplayEndH: 18,
            DisplayEndM: 0,
          },
        ],
        Playlists: [
          {
            PlaylistName: 'failed-scheduled-list',
            PageList: { PLI_PageListName: 'failed-scheduled-list' },
            Pages: [
              {
                PIC_PageName: 'failed-scheduled-page',
                PIC_PlaytimeSecond: 10,
                PIC_CanvasWidth: 1920,
                PIC_CanvasHeight: 1080,
                PIC_Elements: [
                  {
                    EIF_Name: 'failed-video',
                    EIF_Type: 'Media',
                    EIF_Width: 1920,
                    EIF_Height: 1080,
                    EIF_PosLeft: 0,
                    EIF_PosTop: 0,
                    EIF_IsMuted: true,
                    EIF_ContentsInfoClassList: [
                      {
                        CIF_StrGUID: 'new-schedule-content',
                        CIF_FileName: 'failed.mp4',
                        CIF_FileFullPath: 'https://cdn.example.com/failed.mp4',
                        CIF_ContentType: 'Video',
                        CIF_PlayMinute: '00',
                        CIF_PlaySec: '10',
                      },
                    ],
                  },
                ],
              },
            ],
          },
        ],
      },
    })).rejects.toThrow('Tizen Download API');

    expect(loadRemoteSchedule()?.generatedAt).toBe('2026-06-22 08:00:00');
    expect(hasContentPeriod('new-schedule-content')).toBe(false);
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');
    expect(play).toHaveBeenCalledTimes(1);
    app.destroy();
  });

  it('활성 예약 playlist 이름이 같아도 updateschedule 콘텐츠를 다시 적용한다', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 5, 22, 9, 1, 0));
    const play = vi.fn();
    window.webapis = createWebApis(createPlayer(play));
    window.tizen = {
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: (path) => path === 'downloads/first-scheduled.mp4' || path === 'downloads/second-scheduled.mp4',
      },
    };

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();

    const buildSchedulePayload = (fileName: string): UpdatePayload => ({
      Schedule: {
        GeneratedAt: '2026-06-22 09:00:00',
        SpecialSchedules: [
          {
            Id: 'schedule-same-name',
            PageListName: 'scheduled-list',
            DayOfWeek1: false,
            DayOfWeek2: true,
            DayOfWeek3: false,
            DayOfWeek4: false,
            DayOfWeek5: false,
            DayOfWeek6: false,
            DayOfWeek7: false,
            IsPeriodEnable: false,
            DisplayStartH: 9,
            DisplayStartM: 0,
            DisplayEndH: 18,
            DisplayEndM: 0,
          },
        ],
        Playlists: [
          {
            PlaylistName: 'scheduled-list',
            PageList: { PLI_PageListName: 'scheduled-list' },
            Pages: [
              {
                PIC_PageName: 'scheduled-page',
                PIC_PlaytimeSecond: 10,
                PIC_CanvasWidth: 1920,
                PIC_CanvasHeight: 1080,
                PIC_Elements: [
                  {
                    EIF_Name: 'scheduled-video',
                    EIF_Type: 'Media',
                    EIF_Width: 1920,
                    EIF_Height: 1080,
                    EIF_PosLeft: 0,
                    EIF_PosTop: 0,
                    EIF_IsMuted: true,
                    EIF_ContentsInfoClassList: [
                      {
                        CIF_FileName: fileName,
                        CIF_FileFullPath: `downloads/${fileName}`,
                        CIF_ContentType: 'Video',
                        CIF_PlayMinute: '00',
                        CIF_PlaySec: '10',
                      },
                    ],
                  },
                ],
              },
            ],
          },
        ],
      },
    });

    const firstSchedulePromise = (app as unknown as {
      applyUpdateScheduleCommand(payload: UpdatePayload): Promise<boolean>;
    }).applyUpdateScheduleCommand(buildSchedulePayload('first-scheduled.mp4'));
    await vi.advanceTimersByTimeAsync(64);
    await firstSchedulePromise;
    await Promise.resolve();
    expect(document.querySelector('#status-slots')?.textContent).toContain('first-scheduled.mp4');

    const secondSchedulePromise = (app as unknown as {
      applyUpdateScheduleCommand(payload: UpdatePayload): Promise<boolean>;
    }).applyUpdateScheduleCommand(buildSchedulePayload('second-scheduled.mp4'));
    await vi.advanceTimersByTimeAsync(64);
    await secondSchedulePromise;
    await Promise.resolve();

    expect(document.querySelector('#status-playlist')?.textContent).toBe('scheduled-list');
    expect(document.querySelector('#status-slots')?.textContent).toContain('second-scheduled.mp4');
    app.destroy();
  });

  it('updateschedule의 활성 예약 playlist가 비어 있으면 기존 콘텐츠를 유지한다', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 5, 22, 9, 1, 0));
    const play = vi.fn();
    window.webapis = createWebApis(createPlayer(play));

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');

    await (app as unknown as {
      applyUpdateScheduleCommand(payload: UpdatePayload): Promise<boolean>;
    }).applyUpdateScheduleCommand({
      Schedule: {
        GeneratedAt: '2026-06-22 09:00:00',
        SpecialSchedules: [
          {
            Id: 'schedule-empty',
            PageListName: 'empty-scheduled-list',
            DayOfWeek1: false,
            DayOfWeek2: true,
            DayOfWeek3: false,
            DayOfWeek4: false,
            DayOfWeek5: false,
            DayOfWeek6: false,
            DayOfWeek7: false,
            IsPeriodEnable: false,
            DisplayStartH: 9,
            DisplayStartM: 0,
            DisplayEndH: 18,
            DisplayEndM: 0,
          },
        ],
        Playlists: {
          empty: {
            PlaylistName: 'empty-scheduled-list',
            PageList: { PLI_PageListName: 'empty-scheduled-list' },
            Pages: [
              {
                PIC_PageName: 'empty-scheduled-page',
                PIC_PlaytimeSecond: 10,
                PIC_CanvasWidth: 1920,
                PIC_CanvasHeight: 1080,
                PIC_Elements: [],
              },
            ],
          },
        },
      },
    });
    await Promise.resolve();
    await Promise.resolve();

    expect(document.querySelector('#status-playlist')?.textContent).toBe('Tizen Intro');
    expect(document.querySelector('#status-page')?.textContent).toContain('Tizen Intro');
    expect(document.querySelector('.empty-intro-video')).not.toBeNull();
    expect(document.querySelector('#status-message')?.textContent).toContain('예약 스케줄 데이터 없음');
    app.destroy();
  });

  it('updateschedule의 활성 예약 playlist 콘텐츠가 모두 기간 밖이면 기본 콘텐츠를 유지한다', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 5, 22, 9, 1, 0));
    const play = vi.fn();
    window.webapis = createWebApis(createPlayer(play));

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');

    await (app as unknown as {
      applyUpdateScheduleCommand(payload: UpdatePayload): Promise<boolean>;
    }).applyUpdateScheduleCommand({
      Schedule: {
        GeneratedAt: '2026-06-22 09:00:00',
        ContentPeriods: [
          {
            ContentGuid: 'scheduled-period-content',
            StartDate: '2026-06-20',
            EndDate: '2026-06-21',
            StartTime: '00:00',
            EndTime: '23:59',
          },
        ],
        SpecialSchedules: [
          {
            Id: 'schedule-period-out',
            PageListName: 'period-scheduled-list',
            DayOfWeek1: false,
            DayOfWeek2: true,
            DayOfWeek3: false,
            DayOfWeek4: false,
            DayOfWeek5: false,
            DayOfWeek6: false,
            DayOfWeek7: false,
            IsPeriodEnable: false,
            DisplayStartH: 9,
            DisplayStartM: 0,
            DisplayEndH: 18,
            DisplayEndM: 0,
          },
        ],
        Playlists: {
          period: {
            PlaylistName: 'period-scheduled-list',
            PageList: { PLI_PageListName: 'period-scheduled-list' },
            Pages: [
              {
                PIC_PageName: 'period-scheduled-page',
                PIC_PlaytimeSecond: 10,
                PIC_CanvasWidth: 1920,
                PIC_CanvasHeight: 1080,
                PIC_Elements: [
                  {
                    EIF_Name: 'period-video',
                    EIF_Type: 'Media',
                    EIF_Width: 1920,
                    EIF_Height: 1080,
                    EIF_PosLeft: 0,
                    EIF_PosTop: 0,
                    EIF_IsMuted: true,
                    EIF_ContentsInfoClassList: [
                      {
                        CIF_StrGUID: 'scheduled-period-content',
                        CIF_FileName: 'period-scheduled.mp4',
                        CIF_FileFullPath: 'downloads/period-scheduled.mp4',
                        CIF_ContentType: 'Video',
                        CIF_PlayMinute: '00',
                        CIF_PlaySec: '10',
                      },
                    ],
                  },
                ],
              },
            ],
          },
        },
      },
    });
    await Promise.resolve();
    await Promise.resolve();

    expect(document.querySelector('#status-playlist')?.textContent).toBe('period-scheduled-list');
    expect(document.querySelector('#status-page')?.textContent).toContain('No active content');
    expect(document.querySelector('.empty-intro-video')).toBeNull();
    expect(document.querySelectorAll('.slot')).toHaveLength(0);
    expect(document.querySelector('#status-message')?.textContent).toContain('예약 스케줄 콘텐츠 기간 외');
    app.destroy();
  });

  it('updatelist 다운로드 중에는 기존 콘텐츠를 유지하고 완료 후 새 콘텐츠를 적용한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn();
    const getPlayer = vi.fn(() => createPlayer(play));
    window.webapis = createWebApis(createPlayer(play), vi.fn(), { getPlayer });

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });
    await app.start();
    expect(document.querySelector('#loading-overlay')?.classList.contains('loading-overlay--hidden')).toBe(true);
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');

    const downloadCallbacks: DownloadCallback[] = [];
    window.tizen = {
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: (path) => path === 'downloads/updated.mp4',
      },
      DownloadRequest: vi.fn(function DownloadRequest(
        this: DownloadRequest,
        url: string,
        destination?: string | null,
        fileName?: string | null,
        networkType?: 'CELLULAR' | 'WIFI' | 'ALL' | null,
      ) {
        this.url = url;
        this.destination = destination;
        this.fileName = fileName;
        this.networkType = networkType;
      }) as unknown as DownloadRequestConstructor,
      download: {
        start: vi.fn((_request, callback) => {
          if (callback) {
            downloadCallbacks.push(callback);
          }
          return 1;
        }),
      },
    };

    const updatePayload = {
      PageList: { PLI_PageListName: 'updated-list' },
      Pages: [
        {
          PIC_PageName: 'updated-page',
          PIC_PlaytimeSecond: 10,
          PIC_CanvasWidth: 1920,
          PIC_CanvasHeight: 1080,
          PIC_Elements: [
            {
              EIF_Name: 'updated-video',
              EIF_Type: 'Media',
              EIF_Width: 1920,
              EIF_Height: 1080,
              EIF_PosLeft: 0,
              EIF_PosTop: 0,
              EIF_IsMuted: true,
              EIF_ContentsInfoClassList: [
                {
                  CIF_FileName: 'updated.mp4',
                  CIF_FileFullPath: 'https://cdn.example.com/updated.mp4',
                  CIF_ContentType: 'Video',
                  CIF_PlayMinute: '00',
                  CIF_PlaySec: '10',
                },
              ],
            },
          ],
        },
      ],
    };

    const updatePromise = (app as unknown as {
      applyUpdateListCommand(
        payload: typeof updatePayload,
        urgent: boolean,
        commandId: string | null,
      ): Promise<unknown>;
    }).applyUpdateListCommand(updatePayload, true, 'cmd-1');
    await Promise.resolve();

    expect(window.tizen.download?.start).toHaveBeenCalledTimes(1);
    expect(document.querySelector('#loading-overlay')?.classList.contains('loading-overlay--hidden')).toBe(true);
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');
    expect(document.querySelector('#status-update')?.textContent).toContain('다운로드 중');
    expect(document.querySelector('#status-update')?.textContent).toContain('0%');
    expect(document.querySelector('#status-update')?.textContent).toContain('updated-list');
    expect(play).toHaveBeenCalledTimes(1);

    expect(downloadCallbacks).toHaveLength(1);
    downloadCallbacks[0]!.oncompleted?.(1, 'downloads/updated.mp4');
    await Promise.resolve();
    await Promise.resolve();

    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');
    expect(document.querySelectorAll('.slot')).toHaveLength(1);
    await vi.advanceTimersByTimeAsync(32);
    await updatePromise;

    expect(document.querySelector('#loading-overlay')?.classList.contains('loading-overlay--hidden')).toBe(true);
    expect(document.querySelector('#status-playlist')?.textContent).toBe('updated-list');
    expect(document.querySelector('#status-page')?.textContent).toContain('updated-page');
    expect(document.querySelector('#status-update')?.textContent).toContain('완료');
    expect(document.querySelector('#status-update')?.textContent).toContain('100%');
    expect(window.NEWHYON_PLAYER_HEALTH?.diagnostics.updateProgress).toBe(100);
    expect(play).toHaveBeenCalledTimes(2);
    expect(getPlayer).toHaveBeenCalledTimes(2);
    expect(loadRemoteManifest()?.playlistName).toBe('updated-list');
    app.destroy();
  });

  it('surface 재사용 전환은 추가 AVPlay 세션 없이 기존 콘텐츠를 유지하고 업데이트를 적용한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn();
    const initialPlayers = [createPlayer(play), createPlayer(play)];
    const getPlayer = vi.fn(() => initialPlayers.shift() as AVPlayApi);
    window.webapis = createWebApis(createPlayer(play), vi.fn(), { getPlayer });

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });
    await app.start();
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');
    expect(play).toHaveBeenCalledTimes(1);

    window.tizen = {
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: (path) => path === 'downloads/updated.mp4',
      },
    };

    const updatePayload = {
      PageList: { PLI_PageListName: 'updated-list' },
      Pages: [
        {
          PIC_PageName: 'updated-page',
          PIC_PlaytimeSecond: 10,
          PIC_CanvasWidth: 1920,
          PIC_CanvasHeight: 1080,
          PIC_Elements: [
            {
              EIF_Name: 'updated-video',
              EIF_Type: 'Media',
              EIF_Width: 1920,
              EIF_Height: 1080,
              EIF_PosLeft: 0,
              EIF_PosTop: 0,
              EIF_IsMuted: true,
              EIF_ContentsInfoClassList: [
                {
                  CIF_FileName: 'updated.mp4',
                  CIF_FileFullPath: 'downloads/updated.mp4',
                  CIF_ContentType: 'Video',
                  CIF_PlayMinute: '00',
                  CIF_PlaySec: '10',
                },
              ],
            },
          ],
        },
      ],
    };

    const updatePromise = (app as unknown as {
      applyUpdateListCommand(
        payload: typeof updatePayload,
        urgent: boolean,
        commandId: string | null,
      ): Promise<unknown>;
    }).applyUpdateListCommand(updatePayload, true, 'cmd-no-extra-session');
    await vi.advanceTimersByTimeAsync(32);
    await updatePromise;

    expect(document.querySelector('#status-playlist')?.textContent).toBe('updated-list');
    expect(document.querySelector('#status-page')?.textContent).toContain('updated-page');
    expect(document.querySelector('#status-update')?.textContent).toContain('완료');
    expect(document.querySelectorAll('.slot')).toHaveLength(1);
    expect(play).toHaveBeenCalledTimes(2);
    expect(getPlayer).toHaveBeenCalledTimes(2);
    expect(loadRemoteManifest()?.playlistName).toBe('updated-list');
    app.destroy();
  });

  it('새 이미지 콘텐츠 로드가 끝나기 전에는 updatelist 상태를 기존 콘텐츠로 유지한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn();
    window.webapis = createWebApis(createPlayer(play));
    window.tizen = {
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: (path) => path === 'downloads/updated.png',
      },
    };

    const srcDescriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    const completeDescriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'complete');
    const decodeDescriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'decode');
    const originalCreateElement = document.createElement.bind(document);
    const createdImages: HTMLImageElement[] = [];
    const createElementSpy = vi.spyOn(document, 'createElement');
    createElementSpy.mockImplementation(((tagName: string, options?: ElementCreationOptions) => {
      const element = originalCreateElement(tagName, options);
      if (tagName.toLowerCase() === 'img') {
        createdImages.push(element as HTMLImageElement);
      }
      return element;
    }) as typeof document.createElement);
    const loadedImages = new WeakSet<HTMLImageElement>();
    let pendingImage: HTMLImageElement | null = null;
    const decodeResolvers: Array<() => void> = [];
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        pendingImage = this;
      },
    });
    Object.defineProperty(HTMLImageElement.prototype, 'complete', {
      configurable: true,
      get() {
        return loadedImages.has(this);
      },
    });
    Object.defineProperty(HTMLImageElement.prototype, 'decode', {
      configurable: true,
      value: vi.fn(() => new Promise<void>((resolve) => {
        decodeResolvers.push(resolve);
      })),
    });

    try {
      const app = new NewHyOnPlayerApp({
        manifest: createManifest(),
        settings: {
          ...DEFAULT_PLAYER_SETTINGS,
          playerId: '',
          managerAddress: '',
          manifestUrl: '',
          preserveAspectRatio: false,
          switchOnContentEnd: false,
          hudInitiallyVisible: false,
        },
        hudInitiallyVisible: false,
      });
      await app.start();
      expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');

      const updatePayload = {
        PageList: { PLI_PageListName: 'updated-image-list' },
        Pages: [
          {
            PIC_PageName: 'updated-image-page',
            PIC_PlaytimeSecond: 10,
            PIC_CanvasWidth: 1920,
            PIC_CanvasHeight: 1080,
            PIC_Elements: [
              {
                EIF_Name: 'updated-image',
                EIF_Type: 'Media',
                EIF_Width: 1920,
                EIF_Height: 1080,
                EIF_PosLeft: 0,
                EIF_PosTop: 0,
                EIF_IsMuted: true,
                EIF_ContentsInfoClassList: [
                  {
                    CIF_FileName: 'updated.png',
                    CIF_FileFullPath: 'downloads/updated.png',
                    CIF_ContentType: 'Image',
                    CIF_PlayMinute: '00',
                    CIF_PlaySec: '10',
                  },
                ],
              },
            ],
          },
        ],
      };

      const updatePromise = (app as unknown as {
        applyUpdateListCommand(
          payload: typeof updatePayload,
          urgent: boolean,
          commandId: string | null,
        ): Promise<unknown>;
      }).applyUpdateListCommand(updatePayload, false, 'cmd-image');
      await Promise.resolve();
      await Promise.resolve();
      await vi.runAllTicks();
      await Promise.resolve();

      pendingImage ??= Array.from(document.querySelectorAll<HTMLImageElement>('img'))
        .find((image) => image.getAttribute('src')?.includes('updated.png') || image.src.includes('updated.png')) ?? null;
      pendingImage ??= createdImages.find((image) => image.onload !== null || image.src.includes('updated.png')) ?? null;
      expect(pendingImage).not.toBeNull();
      expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');
      expect(document.querySelector('#status-page')?.textContent).toContain('page');
      expect(document.querySelector('#status-update')?.textContent).toContain('적용 중');
      expect(document.querySelectorAll('.slot')).toHaveLength(1);

      loadedImages.add(pendingImage!);
      pendingImage!.onload?.(new Event('load'));
      await Promise.resolve();
      decodeResolvers.forEach((resolve) => resolve());
      await vi.advanceTimersByTimeAsync(32);
      await Promise.resolve();
      await vi.advanceTimersByTimeAsync(32);
      await updatePromise;

      expect(document.querySelector('#status-playlist')?.textContent).toBe('updated-image-list');
      expect(document.querySelector('#status-page')?.textContent).toContain('updated-image-page');
      expect(document.querySelectorAll('.slot')).toHaveLength(1);
      app.destroy();
    } finally {
      if (srcDescriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', srcDescriptor);
      }
      if (completeDescriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'complete', completeDescriptor);
      }
      if (decodeDescriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'decode', decodeDescriptor);
      } else {
        delete (HTMLImageElement.prototype as Partial<HTMLImageElement>).decode;
      }
      createElementSpy.mockRestore();
    }
  });

  it('empty-intro 상태에서 재생 콘텐츠 업데이트가 들어와도 다운로드 중에는 인트로를 유지하고 완료 후 콘텐츠를 적용한다', async () => {
    vi.useFakeTimers();
    const open = vi.fn();
    const avplayPlay = vi.fn();
    window.webapis = createWebApis({
      ...createPlayer(avplayPlay),
      open,
    });
    const downloadCallbacks: DownloadCallback[] = [];
    window.tizen = {
      filesystem: {
        toURI: (path) => `file:///opt/usr/apps/NewHyOnT01.Player/${path}`,
        pathExists: (path) => path === 'wgt-package/media/intro.mp4' || path === 'downloads/updated.mp4',
      },
      DownloadRequest: vi.fn(function DownloadRequest(
        this: DownloadRequest,
        url: string,
        destination?: string | null,
        fileName?: string | null,
        networkType?: 'CELLULAR' | 'WIFI' | 'ALL' | null,
      ) {
        this.url = url;
        this.destination = destination;
        this.fileName = fileName;
        this.networkType = networkType;
      }) as unknown as DownloadRequestConstructor,
      download: {
        start: vi.fn((_request, callback) => {
          if (callback) {
            downloadCallbacks.push(callback);
          }
          return 1;
        }),
      },
    };

    const app = new NewHyOnPlayerApp({
      manifest: {
        playlistName: 'empty',
        preserveAspectRatio: false,
        pages: [],
      },
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });
    await app.start();
    expect(document.querySelector('#status-playlist')?.textContent).toBe('Tizen Intro');
    expect(document.querySelector('.empty-intro-video')).not.toBeNull();
    expect(open).not.toHaveBeenCalled();
    expect(avplayPlay).not.toHaveBeenCalled();
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledTimes(1);

    const updatePayload = {
      PageList: { PLI_PageListName: 'updated-list' },
      Pages: [
        {
          PIC_PageName: 'updated-page',
          PIC_PlaytimeSecond: 10,
          PIC_CanvasWidth: 1920,
          PIC_CanvasHeight: 1080,
          PIC_Elements: [
            {
              EIF_Name: 'updated-video',
              EIF_Type: 'Media',
              EIF_Width: 1920,
              EIF_Height: 1080,
              EIF_PosLeft: 0,
              EIF_PosTop: 0,
              EIF_IsMuted: true,
              EIF_ContentsInfoClassList: [
                {
                  CIF_FileName: 'updated.mp4',
                  CIF_FileFullPath: 'https://cdn.example.com/updated.mp4',
                  CIF_ContentType: 'Video',
                  CIF_PlayMinute: '00',
                  CIF_PlaySec: '10',
                },
              ],
            },
          ],
        },
      ],
    };

    const updatePromise = (app as unknown as {
      applyUpdateListCommand(
        payload: typeof updatePayload,
        urgent: boolean,
        commandId: string | null,
      ): Promise<unknown>;
    }).applyUpdateListCommand(updatePayload, true, 'cmd-content');
    await Promise.resolve();

    expect(window.tizen.download?.start).toHaveBeenCalledTimes(1);
    expect(document.querySelector('#status-state')?.textContent).toBe('playing');
    expect(document.querySelector('#status-playlist')?.textContent).toBe('Tizen Intro');
    expect(document.querySelectorAll('.slot')).toHaveLength(0);
    expect(document.querySelector('.empty-intro-video')).not.toBeNull();
    expect(open).not.toHaveBeenCalled();
    expect(avplayPlay).not.toHaveBeenCalled();

    expect(downloadCallbacks).toHaveLength(1);
    downloadCallbacks[0]!.oncompleted?.(1, 'downloads/updated.mp4');
    await Promise.resolve();
    await Promise.resolve();

    expect(document.querySelector('#status-playlist')?.textContent).toBe('Tizen Intro');
    expect(document.querySelector('.empty-intro-video')).not.toBeNull();
    await vi.advanceTimersByTimeAsync(32);
    await updatePromise;

    expect(document.querySelector('#status-playlist')?.textContent).toBe('updated-list');
    expect(document.querySelector('#status-page')?.textContent).toContain('updated-page');
    expect(document.querySelector('.empty-intro-video')).toBeNull();
    expect(open).toHaveBeenCalledTimes(1);
    expect(avplayPlay).toHaveBeenCalledTimes(1);
    app.destroy();
  });

  it('updatelist가 실제로 빈 데이터일 때만 콘텐츠 없음 인트로 모드로 전환한다', async () => {
    const open = vi.fn();
    const avplayPlay = vi.fn();
    window.webapis = createWebApis({
      ...createPlayer(avplayPlay),
      open,
    });
    window.tizen = {
      filesystem: {
        toURI: (path) => `file:///opt/usr/apps/NewHyOnT01.Player/${path}`,
        pathExists: (path) => path === 'wgt-package/media/intro.mp4',
      },
    };

    const app = new NewHyOnPlayerApp({
      manifest: createManifest(),
      settings: {
        ...DEFAULT_PLAYER_SETTINGS,
        playerId: '',
        managerAddress: '',
        manifestUrl: '',
        preserveAspectRatio: false,
        switchOnContentEnd: false,
        hudInitiallyVisible: false,
      },
      hudInitiallyVisible: false,
    });

    await app.start();
    expect(document.querySelector('#status-playlist')?.textContent).toBe('playlist');
    expect(avplayPlay).toHaveBeenCalledTimes(1);

    const emptyPayload = {
      PageList: { PLI_PageListName: 'empty-list' },
      Pages: [],
    };
    await (app as unknown as {
      applyUpdateListCommand(
        payload: typeof emptyPayload,
        urgent: boolean,
        commandId: string | null,
      ): Promise<unknown>;
    }).applyUpdateListCommand(emptyPayload, false, 'cmd-empty');

    expect(document.querySelector('#status-playlist')?.textContent).toBe('Tizen Intro');
    expect(document.querySelector('#status-page')?.textContent).toContain('Tizen Intro');
    const introVideo = document.querySelector<HTMLVideoElement>('.empty-intro-video');
    expect(introVideo).not.toBeNull();
    expect(introVideo?.getAttribute('src')).toBe('media/intro.mp4');
    expect(open).toHaveBeenCalledTimes(1);
    expect(open).toHaveBeenLastCalledWith('https://example.com/video.mp4');
    expect(avplayPlay).toHaveBeenCalledTimes(1);
    app.destroy();
  });
});
