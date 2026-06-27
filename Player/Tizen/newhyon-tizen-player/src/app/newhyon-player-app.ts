import type { RuntimeConfig } from './runtime-config';
import type { PlayerSettings } from './player-settings';
import { RingLogger } from '../core/logger';
import { buildPagePlan, type BuildPagePlanOptions, type SeamlessContentItem, type SeamlessPagePlan, type SeamlessSlotPlan } from '../domain/page-plan';
import {
  REMOTE_KEY_REGISTRATION_LIST,
  resolveRemoteControlAction,
  type RemoteControlAction,
} from '../input/remote-control';
import { AvplaySessionPool } from '../player/avplay-session';
import { TizenAudioPolicy } from '../player/audio-policy';
import { SlotPlayer, type SlotPlayerTimelineSnapshot } from '../player/slot-player';
import { RuntimeHealthReporter } from './runtime-health-reporter';
import { collectRuntimeDiagnostics, formatRuntimeDiagnostics } from './runtime-diagnostics';
import { SettingsOverlay } from './settings-overlay';
import {
  bootstrapCommunicationSettings,
  type CommunicationBootstrapResult,
  type ConnectionStatus,
} from './communication-bootstrap';
import { HeartbeatReporter } from './heartbeat-reporter';
import { LicenseHubAuthService, type LicenseAuthState } from './licensehub-auth';
import { LicenseAuthOverlay } from './license-auth-overlay';
import { syncPlayerInfoMessage } from './player-info-sync';
import { RemoteCommandService, type RemoteCommandCallbackResult } from './remote-command-service';
import {
  buildManifestFromRemoteSchedulePlaylist,
  buildManifestsFromRemoteSchedulePlaylists,
  evaluateRemoteSchedule,
  loadRemoteSchedule,
  saveRemoteScheduleSnapshot,
  saveRemoteScheduleFromUpdatePayload,
  type RemoteScheduleDecision,
  type RemoteScheduleSnapshot,
} from './remote-schedule';
import {
  configureSsspSignageForPlayer,
  prepareSsspPowerOff,
  prepareSsspReboot,
  setSsspPanelMute,
} from './sssp-command-executor';
import {
  cacheRemoteManifestContent,
  countRemoteManifestContentForOptions,
  type ContentCacheOptions,
} from './sssp-content-cache';
import {
  hasContentPeriod,
  isContentPeriodAllowed,
  saveContentPeriodSnapshot,
  saveContentPeriodsFromSchedule,
  saveContentPeriodsFromUpdatePayload,
} from './content-period';
import { ContentPeriodSyncClient } from './content-period-sync';
import { buildManifestFromUpdatePayload, saveRemoteManifest, type UpdatePayload } from './update-payload';
import { evaluateWeeklySchedule, loadWeeklySchedule, saveWeeklyScheduleFromUpdatePayload } from './weekly-schedule';
import { TIZEN_INTRO_VIDEO_FILE, createTizenIntroManifest } from './default-manifest';

interface ViewRefs {
  readonly stage: HTMLElement;
  readonly broadcastStandby: HTMLElement;
  readonly loadingOverlay: HTMLElement;
  readonly loadingTitle: HTMLElement;
  readonly loadingMessage: HTMLElement;
  readonly loadingDbStatus: HTMLElement;
  readonly loadingSignalrStatus: HTMLElement;
  readonly loadingFtpStatus: HTMLElement;
  readonly loadingAuthStatus: HTMLElement;
  readonly hud: HTMLElement;
  readonly state: HTMLElement;
  readonly playlist: HTMLElement;
  readonly page: HTMLElement;
  readonly elapsed: HTMLElement;
  readonly lastKey: HTMLElement;
  readonly lastAction: HTMLElement;
  readonly platform: HTMLElement;
  readonly communication: HTMLElement;
  readonly auth: HTMLElement;
  readonly update: HTMLElement;
  readonly slots: HTMLElement;
  readonly timeline: HTMLElement;
  readonly message: HTMLElement;
  readonly logOutput: HTMLElement;
}

interface PlayerInfoSyncUiOptions {
  readonly showLoading?: boolean;
  readonly hideLoadingWhenDone?: boolean;
}

type PlaybackMode = 'content' | 'empty-intro' | 'blackout';

interface PagePlanResolution {
  readonly pagePlans: SeamlessPagePlan[];
  readonly mode: PlaybackMode;
}

interface PagePlayOptions {
  readonly preservePreviousUntilReady?: boolean;
  readonly pagePlans?: readonly SeamlessPagePlan[];
  readonly playbackMode?: PlaybackMode;
  readonly commitPageTimelineBeforeSurfaceSwap?: boolean;
}

interface EmptyIntroVideoOptions {
  readonly hidden?: boolean;
  readonly makeCurrent?: boolean;
  readonly recordStarted?: boolean;
  readonly reportErrors?: boolean;
}

interface PlaybackStateSnapshot {
  readonly pagePlans: SeamlessPagePlan[];
  readonly playbackMode: PlaybackMode;
  readonly pageIndex: number;
  readonly pageStartedAt: number;
  readonly pagePausedElapsedMs: number;
  readonly playing: boolean;
}

interface StagedContentShown {
  readonly slotIndex: number;
  readonly item: SeamlessContentItem;
}

type UpdateOverlayPhase = 'idle' | 'downloading' | 'applying' | 'complete' | 'failed';

interface UpdateOverlayState {
  readonly phase: UpdateOverlayPhase;
  readonly playlistName: string;
  readonly commandId: string;
  readonly completed: number;
  readonly total: number;
  readonly progress: number;
  readonly currentFile: string;
  readonly detail: string;
  readonly updatedAt: string;
}

type LoadingStepState = 'pending' | 'active' | 'complete' | 'error' | 'skipped';

const TRANSITION_SLOT_INDEX_OFFSET = 100;
const MASTER_TICK_INTERVAL_MS = 200;
const MASTER_TICK_LAG_WARN_MS = 500;
const SCHEDULE_CHECK_INTERVAL_MS = 1000;
const RUNTIME_HEALTH_RECENT_LOG_LIMIT = 200;
const RUNTIME_HEALTH_LOG_FLUSH_DELAY_MS = 250;

function getRequiredElement<T extends HTMLElement>(selector: string): T {
  const element = document.querySelector<T>(selector);
  if (!element) {
    throw new Error(`필수 요소를 찾지 못했습니다: ${selector}`);
  }

  return element;
}

function formatError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  return String(error);
}

export class NewHyOnPlayerApp {
  private readonly logger = new RingLogger(400);
  private readonly view: ViewRefs;
  private readonly pagePlans: SeamlessPagePlan[];
  private readonly slotPlayers: SlotPlayer[] = [];
  private readonly audioPolicy = new TizenAudioPolicy(this.logger);
  private readonly healthReporter = new RuntimeHealthReporter();
  private avplayPool: AvplaySessionPool | null = null;
  private pageIndex = 0;
  private pageStartedAt = 0;
  private pagePausedElapsedMs = 0;
  private masterTimerId: number | null = null;
  private masterTickNextAt = 0;
  private lastMasterTickAt = 0;
  private lastMasterTickDelayMs = 0;
  private lastMasterTickIntervalMs = 0;
  private lastRenderAt = 0;
  private lastRenderIntervalMs = 0;
  private runtimeHealthLogFlushTimerId: number | null = null;
  private pageTransitionInProgress = false;
  private slotTimelineSyncInProgress = false;
  private scheduleCheckInProgress = false;
  private remoteScheduleSwitchInProgress = false;
  private remoteScheduleUpdateInProgress = false;
  private activeRemoteSchedulePlaylistName: string | null = null;
  private lastScheduleCheckSecond = -1;
  private lastBroadcastScheduleCheckMinute = -1;
  private lastContentPeriodCheckMinute = -1;
  private settingsOverlay: SettingsOverlay | null = null;
  private lastRemoteKey = '-';
  private lastRemoteAction = '-';
  private pageStartCount = 0;
  private contentShowCount = 0;
  private lastContent = '-';
  private communicationStatus = 'not-started';
  private communication: CommunicationBootstrapResult | null = null;
  private dbStatus: ConnectionStatus = 'not-configured';
  private signalrStatus: ConnectionStatus = 'not-configured';
  private ftpStatus: ConnectionStatus = 'not-configured';
  private dbStatusDetail = '-';
  private signalrStatusDetail = '-';
  private ftpStatusDetail = '-';
  private heartbeatStatus = 'not-started';
  private heartbeatStatusDetail = '-';
  private heartbeatReporter: HeartbeatReporter | null = null;
  private remoteCommandService: RemoteCommandService | null = null;
  private authStatus = 'not-started';
  private authStatusDetail = '-';
  private authService: LicenseHubAuthService | null = null;
  private authOverlay: LicenseAuthOverlay | null = null;
  private lastAuthState: LicenseAuthState | null = null;
  private currentContentManifest: RuntimeConfig['manifest'];
  private contentPlaybackAllowed = true;
  private offlineHeartbeatRequested = false;
  private broadcastOnAir = true;
  private panelMuted: boolean | null = null;
  private playing = false;
  private destroyed = false;
  private startupLoadingActive = true;
  private playbackMode: PlaybackMode = 'content';
  private emptyIntroVideoElement: HTMLVideoElement | null = null;
  private contentReplacementInProgress = false;
  private updateOverlayState: UpdateOverlayState = {
    phase: 'idle',
    playlistName: '-',
    commandId: '-',
    completed: 0,
    total: 0,
    progress: 0,
    currentFile: '-',
    detail: '대기',
    updatedAt: '-',
  };

  constructor(private config: RuntimeConfig) {
    this.view = {
      stage: getRequiredElement('#stage'),
      broadcastStandby: getRequiredElement('#broadcast-standby'),
      loadingOverlay: getRequiredElement('#loading-overlay'),
      loadingTitle: getRequiredElement('#loading-title'),
      loadingMessage: getRequiredElement('#loading-message'),
      loadingDbStatus: getRequiredElement('#loading-db-status'),
      loadingSignalrStatus: getRequiredElement('#loading-signalr-status'),
      loadingFtpStatus: getRequiredElement('#loading-ftp-status'),
      loadingAuthStatus: getRequiredElement('#loading-auth-status'),
      hud: getRequiredElement('#debug-hud'),
      state: getRequiredElement('#status-state'),
      playlist: getRequiredElement('#status-playlist'),
      page: getRequiredElement('#status-page'),
      elapsed: getRequiredElement('#status-elapsed'),
      lastKey: getRequiredElement('#status-last-key'),
      lastAction: getRequiredElement('#status-last-action'),
      platform: getRequiredElement('#status-platform'),
      communication: getRequiredElement('#status-communication'),
      auth: getRequiredElement('#status-auth'),
      update: getRequiredElement('#status-update'),
      slots: getRequiredElement('#status-slots'),
      timeline: getRequiredElement('#status-timeline'),
      message: getRequiredElement('#status-message'),
      logOutput: getRequiredElement('#log-output'),
    };
    this.currentContentManifest = this.config.manifest;
    const resolution = this.createStartupPagePlans(this.config.manifest);
    this.pagePlans = resolution.pagePlans;
    this.playbackMode = resolution.mode;
  }

  async start(): Promise<void> {
    this.showLoading('플레이어 준비 중', '시스템을 초기화합니다.');
    if (this.pagePlans.length === 0) {
      this.showLoading('플레이어 오류', '재생할 페이지가 없습니다.', true);
      throw new Error('재생할 페이지가 없습니다.');
    }

    try {
      this.bindUi();
      this.registerInputKeys();
      this.settingsOverlay = new SettingsOverlay({
        onApply: (settings) => {
          this.applyPlayerSettings(settings);
        },
        onAuthenticate: () => {
          void this.openAuthenticationOverlay('인증 상태를 다시 확인합니다.');
        },
        getAuthStatusText: () => `인증 상태 : ${this.authStatus} (${this.authStatusDetail})`,
      });
      this.setHudVisible(this.config.hudInitiallyVisible);
      this.logRuntimeDiagnostics();
      this.configureSsspSignageControl();
      this.writeRuntimeHealth('app-started');
      await this.bootstrapCommunication();
      await this.syncContentPeriodsForManifests([this.currentContentManifest], 'startup', false);
      const startupResolution = this.createStartupPagePlans(this.currentContentManifest);
      this.commitPlaybackPlan(startupResolution.pagePlans, startupResolution.mode, 0);
      await this.ensureAuthentication();
      this.showLoading('플레이어 준비 중', '콘텐츠 재생을 준비합니다.');
      this.configureRemoteCommands();
      this.startHeartbeat();
      this.remoteCommandService?.start();
      await this.applyBroadcastSchedule('startup');
      this.startMasterTimer();
      this.hideLoading();
    } catch (error) {
      const message = formatError(error);
      this.setHudVisible(true);
      this.setMessage(message);
      this.showLoading('플레이어 오류', message, true);
      throw error;
    }
  }

  destroy(): void {
    this.destroyed = true;
    this.stopMasterTimer();
    this.stopSlots();
    this.requestOfflineHeartbeat();
    this.heartbeatReporter = null;
    this.remoteCommandService?.dispose();
    this.remoteCommandService = null;
    this.avplayPool?.stopAll();
    this.removeEmptyIntroVideo();
    this.audioPolicy.restore();
    this.settingsOverlay?.close();
    this.authOverlay?.close();
    window.removeEventListener('resize', this.handleResize);
    window.removeEventListener('keydown', this.handleKeyDown);
    window.removeEventListener('pagehide', this.handlePageHide);
    window.removeEventListener('beforeunload', this.handleBeforeUnload);
    document.removeEventListener('visibilitychange', this.handleVisibilityChange);
  }

  private ensureAvplayPool(): AvplaySessionPool {
    if (this.avplayPool) {
      return this.avplayPool;
    }

    this.avplayPool = new AvplaySessionPool(this.view.stage, this.logger, (index) => ({
      onEnded: () => {
        this.logger.info('avplay', `slot ${index + 1} stream completed`);
      },
      onError: (message) => {
        this.setMessage(message);
        this.logger.error('avplay', message);
      },
    }));
    return this.avplayPool;
  }

  private applyPlayerSettings(settings: PlayerSettings): void {
    const preserveChanged = this.config.settings.preserveAspectRatio !== settings.preserveAspectRatio;
    const switchOnEndChanged = this.config.settings.switchOnContentEnd !== settings.switchOnContentEnd;

    this.config = {
      ...this.config,
      settings,
      hudInitiallyVisible: settings.hudInitiallyVisible,
      manifest: {
        ...this.config.manifest,
        preserveAspectRatio: settings.preserveAspectRatio,
      },
    };
    this.currentContentManifest = {
      ...this.currentContentManifest,
      preserveAspectRatio: settings.preserveAspectRatio,
    };
    this.slotPlayers.forEach((slotPlayer) => {
      slotPlayer.updatePlaybackSettings(settings.preserveAspectRatio, settings.switchOnContentEnd);
      slotPlayer.applyDisplayRect();
    });
    this.applyEmptyIntroDisplayMode();
    this.setHudVisible(settings.hudInitiallyVisible);
    this.setMessage([
      '설정 적용 완료',
      `비율대로표출=${settings.preserveAspectRatio ? 'ON' : 'OFF'}`,
      `컨텐츠 종료시 전환=${settings.switchOnContentEnd ? 'ON' : 'OFF'}`,
    ].join(' / '));
    if (preserveChanged || switchOnEndChanged) {
      this.logger.info(
        'settings',
        `playback settings applied: preserveAspectRatio=${settings.preserveAspectRatio}, switchOnContentEnd=${settings.switchOnContentEnd}`,
      );
    }
    this.render();
    this.writeRuntimeHealth('settings-applied');
  }

  private bindUi(): void {
    this.logger.subscribe((entries) => {
      this.view.logOutput.textContent = [...entries]
        .reverse()
        .map((entry) => `${entry.timestamp} | ${entry.level.toUpperCase()} | ${entry.scope} | ${this.sanitizeOverlaySensitiveText(entry.message)}`)
        .join('\n');
      const latest = entries[entries.length - 1];
      if (latest?.scope === 'avplay' || latest?.scope === 'avplay-trace') {
        this.scheduleRuntimeHealthLogFlush(latest.level === 'error' ? 0 : RUNTIME_HEALTH_LOG_FLUSH_DELAY_MS);
      }
    });
    window.addEventListener('resize', this.handleResize);
    window.addEventListener('keydown', this.handleKeyDown);
    window.addEventListener('pagehide', this.handlePageHide);
    window.addEventListener('beforeunload', this.handleBeforeUnload);
    document.addEventListener('visibilitychange', this.handleVisibilityChange);
  }

  private registerInputKeys(): void {
    const inputDevice = window.tizen?.tvinputdevice;
    if (!inputDevice) {
      this.logger.warn('input', 'tvinputdevice API를 찾지 못해 리모컨 특수키 등록을 생략합니다.');
      return;
    }

    try {
      const supportedKeys = typeof inputDevice.getSupportedKeys === 'function'
        ? new Set(inputDevice.getSupportedKeys().map((key) => key.name))
        : null;
      const keys = supportedKeys
        ? REMOTE_KEY_REGISTRATION_LIST.filter((key) => supportedKeys.has(key))
        : [...REMOTE_KEY_REGISTRATION_LIST];

      if (keys.length === 0) {
        this.logger.warn('input', '등록 가능한 리모컨 특수키가 없습니다.');
        return;
      }

      if (typeof inputDevice.registerKeyBatch === 'function') {
        inputDevice.registerKeyBatch([...keys]);
      } else {
        for (const key of keys) {
          inputDevice.registerKey(key);
        }
      }
      this.logger.info('input', `리모컨 특수키 등록: ${keys.join(', ')}`);
    } catch (error) {
      this.logger.warn('input', `리모컨 키 등록 실패: ${formatError(error)}`);
    }
  }

  private logRuntimeDiagnostics(): void {
    this.logger.info('runtime', formatRuntimeDiagnostics(collectRuntimeDiagnostics()));
  }

  private configureSsspSignageControl(): void {
    const snapshot = configureSsspSignageForPlayer();
    this.panelMuted = snapshot.panelMute === 'ON';
    this.logger.info(
      'sssp',
      `signage control ready: panelMute=${snapshot.panelMute}, messageDisplay=${snapshot.messageDisplay}, remotePower=${snapshot.remoteConfiguration}`,
    );
  }

  private async bootstrapCommunication(): Promise<void> {
    if (!this.config.settings.managerAddress.trim()) {
      this.communicationStatus = 'not-configured';
      this.setConnectionStatus('db', 'not-configured', '데이터서버 IP 미설정');
      this.setConnectionStatus('signalr', 'not-configured', '데이터서버 IP 미설정');
      this.setConnectionStatus('ftp', 'not-configured', '데이터서버 IP 미설정');
      this.logger.warn('communication', '데이터서버 주소가 비어 있어 통신 설정 부트스트랩을 건너뜁니다.');
      this.writeRuntimeHealth('communication-not-configured');
      this.render();
      return;
    }

    this.communicationStatus = 'connecting';
    this.showLoading('통신 연결 확인 중', 'DB 설정을 확인합니다.');
    this.setConnectionStatus('db', 'checking', this.config.settings.managerAddress);
    this.setConnectionStatus('signalr', 'checking', '-');
    this.setConnectionStatus('ftp', 'checking', '-');
    this.writeRuntimeHealth('communication-connecting');
    try {
      this.communication = await bootstrapCommunicationSettings(this.config.settings, {
        onStatus: ({ target, status, detail }) => {
          this.setConnectionStatus(target, status, detail);
          if (target === 'db' && status === 'connected') {
            this.showLoading('통신 연결 확인 중', 'SignalR 연결을 확인합니다.');
          } else if (target === 'signalr' && status === 'connected') {
            this.showLoading('통신 연결 확인 중', 'FTP 연결을 확인합니다.');
          }
          this.render();
          this.writeRuntimeHealth(`communication-${target}-${status}`);
        },
      });
      if (!this.communication) {
        this.communicationStatus = 'not-configured';
        this.writeRuntimeHealth('communication-not-configured');
        return;
      }
    } catch (error) {
      this.communicationStatus = 'failed';
      this.markCheckingConnectionsFailed(formatError(error));
      this.render();
      this.writeRuntimeHealth('communication-failed');
      throw error;
    }

    this.communicationStatus = 'connected';
    await this.syncPlayerInfo(null);
    this.logger.info(
      'communication',
      `데이터서버/DB/SignalR/FTP 설정 완료: db=${this.communication.dbHost}, signalr=${this.communication.signalrUrl}, ftp=${this.communication.ftpHost}:${this.communication.ftpPort}${this.communication.ftpRootPath}`,
    );
    this.writeRuntimeHealth('communication-connected');
  }

  private startHeartbeat(): void {
    if (!this.communication) {
      this.heartbeatStatus = 'not-configured';
      this.heartbeatStatusDetail = 'SignalR 통신 미설정';
      this.render();
      this.writeRuntimeHealth('heartbeat-not-configured');
      return;
    }

    this.heartbeatReporter?.dispose();
    this.heartbeatStatus = 'starting';
    this.heartbeatStatusDetail = this.communication.signalrUrl;
    this.heartbeatReporter = new HeartbeatReporter({
      signalrUrl: this.communication.signalrUrl,
      playerGuid: this.communication.playerGuid,
      playerName: this.communication.playerName,
      getVersion: () => this.getHeartbeatVersion(),
      getStatus: () => this.getHeartbeatStatus(),
      getProcess: () => this.getHeartbeatProcess(),
      getCurrentPage: () => this.getCurrentPageName(),
      getHdmiState: () => this.getHeartbeatHdmiState(),
      onStatus: (status, detail) => {
        this.heartbeatStatus = status;
        this.heartbeatStatusDetail = detail;
        this.logger.info('heartbeat', `${status}: ${detail}`);
        this.render();
        this.writeRuntimeHealth(`heartbeat-${status}`);
      },
      onMessage: (message) => {
        this.remoteCommandService?.handleSignalRMessage(message);
      },
    });
    this.heartbeatReporter.start();
  }

  private configureRemoteCommands(): void {
    if (!this.communication) {
      return;
    }

    this.remoteCommandService?.dispose();
    this.remoteCommandService = new RemoteCommandService({
      managerAddress: this.config.settings.managerAddress,
      playerGuid: this.communication.playerGuid,
      playerName: this.communication.playerName,
      onStatus: (status, detail) => {
        this.logger.info('command', `${status}: ${detail}`);
        this.setMessage(`원격 명령 ${status}: ${detail}`);
      },
      onUpdateList: async (payload, urgent, commandId) => this.applyUpdateListCommand(payload, urgent, commandId),
      onUpdateSchedule: async (payload) => this.applyUpdateScheduleCommand(payload),
      onUpdateWeekly: async (payload) => this.applyUpdateWeeklyCommand(payload, 'updateweekly'),
      onUpdateContentPeriod: async (payload) => this.applyUpdateContentPeriodCommand(payload),
      onCheck: async () => {
        await this.sendHeartbeatNow();
        return true;
      },
      onGetMac: async () => {
        await this.syncPlayerInfo(this.lastAuthState, { showLoading: false });
        await this.sendHeartbeatNow();
        return true;
      },
      onClearQueue: async () => 0,
      onReboot: async () => {
        const command = prepareSsspReboot();
        await this.sendStoppedHeartbeatForDevicePowerCommand();
        this.setMessage('원격 재시작 명령을 적용합니다.');
        return command;
      },
      onPowerOff: async () => {
        const command = prepareSsspPowerOff();
        this.setMessage('원격 종료 명령을 적용합니다.');
        this.stopPlayback();
        await this.sendStoppedHeartbeatForDevicePowerCommand();
        return command;
      },
    });
  }

  private async applyUpdateListCommand(
    payload: UpdatePayload,
    urgent: boolean,
    commandId: string | null,
  ): Promise<RemoteCommandCallbackResult> {
    const manifest = buildManifestFromUpdatePayload(payload, this.config.manifest.preserveAspectRatio);
    const cacheOptions = this.buildRemoteContentOptions();
    const remoteContentCount = countRemoteManifestContentForOptions(manifest, cacheOptions);
    const updateSessionId = this.beginUpdateHeartbeatReporting();
    const cacheNamespace = this.buildUpdateCacheNamespace(commandId, updateSessionId);
    this.setUpdateOverlayState({
      phase: remoteContentCount > 0 ? 'downloading' : 'applying',
      playlistName: manifest.playlistName,
      commandId: commandId ?? '-',
      completed: 0,
      total: remoteContentCount,
      progress: remoteContentCount > 0 ? 0 : 90,
      currentFile: '-',
      detail: remoteContentCount > 0 ? '콘텐츠 다운로드 대기' : '다운로드할 원격 콘텐츠 없음, 적용 준비',
    }, 'update-started');
    this.reportUpdateHeartbeat(
      remoteContentCount > 0 ? 'DOWNLOADING' : 'APPLYING',
      this.updateOverlayState.progress,
      true,
      updateSessionId,
    );
    if (remoteContentCount > 0) {
      this.logger.info('download', `조용한 업데이트 다운로드 시작: total=${remoteContentCount}`);
    }
    try {
      const cachedManifest = await cacheRemoteManifestContent(manifest, {
        ...cacheOptions,
        cacheNamespace,
        onProgress: (progress) => {
          const updateProgress = this.calculateDownloadUpdateProgress(progress.completed, progress.total);
          this.logger.info(
            'download',
            `조용한 업데이트 다운로드: ${progress.completed}/${progress.total} ${progress.fileName}`,
          );
          this.setUpdateOverlayState({
            phase: 'downloading',
            playlistName: manifest.playlistName,
            commandId: commandId ?? '-',
            completed: progress.completed,
            total: progress.total,
            progress: updateProgress,
            currentFile: progress.fileName,
            detail: '콘텐츠 다운로드 중',
          }, 'update-download-progress');
          this.reportUpdateHeartbeat('DOWNLOADING', updateProgress, false, updateSessionId);
        },
      });
      const cachedContentCount = this.countCachedManifestContent(cachedManifest);
      this.logger.info(
        'command',
        `updatelist 적용 준비 완료: playlist=${cachedManifest.playlistName}, pages=${cachedManifest.pages.length}, remoteContents=${remoteContentCount}, cachedContents=${cachedContentCount}, urgent=${urgent}, commandId=${commandId ?? '-'}`,
      );
      await this.syncContentPeriodsForManifests([cachedManifest], `updatelist:${commandId ?? '-'}`, false);
      this.setUpdateOverlayState({
        phase: 'applying',
        playlistName: cachedManifest.playlistName,
        commandId: commandId ?? '-',
        completed: remoteContentCount,
        total: remoteContentCount,
        progress: 90,
        currentFile: '-',
        detail: '다운로드 완료, 화면 적용 중',
      }, 'update-applying');
      this.reportUpdateHeartbeat('APPLYING', 90, true, updateSessionId);
      await this.applyManifest(cachedManifest, `updatelist:${commandId ?? '-'}`);
      saveRemoteManifest(cachedManifest);
      this.setUpdateOverlayState({
        phase: 'complete',
        playlistName: cachedManifest.playlistName,
        commandId: commandId ?? '-',
        completed: cachedContentCount,
        total: remoteContentCount,
        progress: 100,
        currentFile: '-',
        detail: `캐시 완료 ${cachedContentCount}/${remoteContentCount}`,
      }, 'update-complete');
      this.reportUpdateHeartbeat('DONE', 100, true, updateSessionId);
      this.setMessage(`업데이트 적용: ${cachedManifest.playlistName}`);
      return {
        handled: true,
        metadata: [
          `playlist=${cachedManifest.playlistName}`,
          `pages=${cachedManifest.pages.length}`,
          `remoteContents=${remoteContentCount}`,
          `cachedContents=${cachedContentCount}`,
          `urgent=${urgent}`,
          `commandId=${commandId ?? '-'}`,
        ].join(';'),
      };
    } catch (error) {
      const message = formatError(error);
      this.setUpdateOverlayState({
        phase: 'failed',
        playlistName: manifest.playlistName,
        commandId: commandId ?? '-',
        detail: message,
      }, 'update-failed');
      this.reportUpdateHeartbeat('FAILED', this.updateOverlayState.progress, true, updateSessionId);
      throw error;
    } finally {
      await this.endUpdateHeartbeatReporting(updateSessionId, true);
    }
  }

  private async applyManifest(manifest: RuntimeConfig['manifest'], source: string): Promise<void> {
    this.currentContentManifest = manifest;
    const resolution = this.createEffectiveUpdatePagePlans(manifest);
    const previousState = this.capturePlaybackState();
    const canRestoreCurrentPlayback = this.broadcastOnAir
      && this.hasActivePlaybackSurface();
    this.logger.info(
      'manifest',
      `${source} 적용: pages=${manifest.pages.length}, effectivePages=${resolution.pagePlans.length}, mode=${resolution.mode}`,
    );

    const previousReplacementInProgress = this.contentReplacementInProgress;
    this.contentReplacementInProgress = true;
    try {
      if (this.broadcastOnAir) {
        await this.playPage(0, {
          preservePreviousUntilReady: true,
          pagePlans: resolution.pagePlans,
          playbackMode: resolution.mode,
        });
        return;
      }

      this.commitPlaybackPlan(resolution.pagePlans, resolution.mode, 0);
      this.render();
      this.writeRuntimeHealth('manifest-applied');
    } catch (error) {
      const message = formatError(error);
      if (canRestoreCurrentPlayback) {
        this.restorePlaybackState(previousState);
        this.restorePlaybackTimersAfterFailedTransition();
      }
      this.logger.error('manifest', `${source} 적용 실패: ${message}`);
      this.setMessage(`업데이트 적용 실패: ${message}`);
      this.render();
      this.writeRuntimeHealth('manifest-apply-failed');
      throw error;
    } finally {
      this.contentReplacementInProgress = previousReplacementInProgress;
    }
  }

  private commitPlaybackPlan(
    pagePlans: readonly SeamlessPagePlan[],
    playbackMode: PlaybackMode,
    pageIndex: number,
  ): void {
    this.pagePlans.splice(0, this.pagePlans.length, ...pagePlans);
    this.playbackMode = playbackMode;
    this.pageIndex = (pageIndex + this.pagePlans.length) % this.pagePlans.length;
    this.pagePausedElapsedMs = 0;
  }

  private capturePlaybackState(): PlaybackStateSnapshot {
    return {
      pagePlans: [...this.pagePlans],
      playbackMode: this.playbackMode,
      pageIndex: this.pageIndex,
      pageStartedAt: this.pageStartedAt,
      pagePausedElapsedMs: this.pagePausedElapsedMs,
      playing: this.playing,
    };
  }

  private restorePlaybackState(snapshot: PlaybackStateSnapshot): void {
    this.pagePlans.splice(0, this.pagePlans.length, ...snapshot.pagePlans);
    this.playbackMode = snapshot.playbackMode;
    this.pageIndex = snapshot.pageIndex;
    this.pageStartedAt = snapshot.pageStartedAt;
    this.pagePausedElapsedMs = snapshot.pagePausedElapsedMs;
    this.playing = snapshot.playing;
  }

  private restorePlaybackTimersAfterFailedTransition(): void {
    if (!this.broadcastOnAir || !this.playing || !this.hasActivePlaybackSurface()) {
      return;
    }

    this.startMasterTimer();
  }

  private setUpdateOverlayState(
    nextState: Partial<Omit<UpdateOverlayState, 'updatedAt'>>,
    healthStage: string,
  ): void {
    this.updateOverlayState = {
      ...this.updateOverlayState,
      ...nextState,
      progress: this.normalizeUpdateProgress(nextState.progress ?? this.updateOverlayState.progress),
      updatedAt: new Date().toISOString(),
    };
    this.render();
    this.writeRuntimeHealth(healthStage);
  }

  private calculateDownloadUpdateProgress(completed: number, total: number): number {
    if (!Number.isFinite(completed) || !Number.isFinite(total) || total <= 0) {
      return 0;
    }

    const safeCompleted = Math.min(Math.max(0, completed), total);
    return this.normalizeUpdateProgress((safeCompleted / total) * 80);
  }

  private normalizeUpdateProgress(progress: number): number {
    if (!Number.isFinite(progress)) {
      return 0;
    }

    return Math.min(100, Math.max(0, Math.round(progress)));
  }

  private beginUpdateHeartbeatReporting(): number {
    return this.heartbeatReporter?.beginUpdateReporting() ?? 0;
  }

  private reportUpdateHeartbeat(status: string, progress: number, force: boolean, sessionId: number): void {
    if (!this.heartbeatReporter || sessionId <= 0) {
      return;
    }

    void this.heartbeatReporter.reportUpdateNow(status, progress, force, sessionId)
      .catch((error) => {
        this.logger.warn('heartbeat', `업데이트 진행률 전송 실패: ${formatError(error)}`);
      });
  }

  private async endUpdateHeartbeatReporting(sessionId: number, sendNormalHeartbeatNow: boolean): Promise<void> {
    if (!this.heartbeatReporter || sessionId <= 0) {
      return;
    }

    try {
      await this.heartbeatReporter.endUpdateReporting(sessionId, sendNormalHeartbeatNow);
    } catch (error) {
      this.logger.warn('heartbeat', `업데이트 진행률 종료 전송 실패: ${formatError(error)}`);
    }
  }

  private countCachedManifestContent(manifest: RuntimeConfig['manifest']): number {
    return manifest.pages
      .flatMap((page) => page.PIC_Elements ?? [])
      .flatMap((element) => element.EIF_ContentsInfoClassList ?? [])
      .filter((content) =>
        content.CIF_FileExist === true
        && (content.CIF_FileFullPath?.startsWith('downloads/') || content.CIF_FileFullPath?.startsWith('file://')),
      )
      .length;
  }

  private buildRemoteContentOptions(): ContentCacheOptions {
    if (!this.communication) {
      return {};
    }

    return {
      ftp: {
        host: this.communication.ftpHost,
        port: this.communication.ftpPort,
        basePath: this.communication.ftpRootPath,
        userName: this.communication.ftpUserName,
        password: this.communication.ftpPassword,
      },
    };
  }

  private buildUpdateCacheNamespace(commandId: string | null, updateSessionId: number): string {
    const seed = commandId?.trim() || `local-${updateSessionId}-${Date.now()}`;
    return `updatelist-${seed}`;
  }

  private async syncContentPeriodsForManifests(
    manifests: readonly RuntimeConfig['manifest'][],
    source: string,
    refreshPlayback: boolean,
  ): Promise<void> {
    const contentGuids = this.collectManifestContentGuids(manifests);
    const result = await this.syncContentPeriodsByGuids(contentGuids, source);
    if (result && refreshPlayback) {
      await this.refreshPlaybackForContentPeriodChange();
    }
  }

  private async syncContentPeriodsByGuids(
    contentGuids: readonly string[],
    source: string,
  ): Promise<ReturnType<typeof saveContentPeriodSnapshot> | null> {
    const requestedGuids = [...new Set(contentGuids.map((guid) => guid.trim()).filter((guid) => guid.length > 0))];
    if (requestedGuids.length === 0 || !this.communication) {
      return null;
    }

    const client = new ContentPeriodSyncClient(this.config.settings.managerAddress);
    try {
      const periods = await client.fetchByContentGuids(requestedGuids);
      const result = saveContentPeriodSnapshot(requestedGuids, periods);
      this.logger.info(
        'content-period',
        `${source} 동기화 완료: requested=${result.requested}, fetched=${periods.length}, upserted=${result.upserted}, removed=${result.removed}, total=${result.total}`,
      );
      return result;
    } catch (error) {
      this.logger.warn('content-period', `${source} 동기화 실패: ${formatError(error)}`);
      return null;
    } finally {
      client.dispose();
    }
  }

  private collectManifestContentGuids(manifests: readonly RuntimeConfig['manifest'][]): string[] {
    const contentGuids = new Set<string>();
    manifests.forEach((manifest) => {
      manifest.pages
        .flatMap((page) => page.PIC_Elements ?? [])
        .flatMap((element) => element.EIF_ContentsInfoClassList ?? [])
        .forEach((content) => {
          const guid = content.CIF_StrGUID?.trim();
          if (guid) {
            contentGuids.add(guid);
          }
        });
    });
    return [...contentGuids];
  }

  private async applyUpdateWeeklyCommand(payload: UpdatePayload, commandName: string): Promise<boolean> {
    const rows = saveWeeklyScheduleFromUpdatePayload(payload);
    this.setMessage(`주간 스케줄 적용: ${rows.length}일`);
    this.logger.info('command', `${commandName} 적용 완료: rows=${rows.length}`);
    await this.applyBroadcastSchedule(commandName);
    return true;
  }

  private async applyUpdateScheduleCommand(payload: UpdatePayload): Promise<boolean> {
    let snapshot: RemoteScheduleSnapshot;
    this.remoteScheduleUpdateInProgress = true;
    try {
      snapshot = saveRemoteScheduleFromUpdatePayload(payload);
      const contentPeriodResult = saveContentPeriodsFromSchedule(snapshot.schedule);
      this.setMessage('서버 스케줄 적용 완료');
      this.logger.info(
        'command',
        `updateschedule 적용 완료: special=${snapshot.specialScheduleCount}, playlists=${snapshot.playlistScheduleCount}, contentPeriods=${snapshot.contentPeriodCount}, contentPeriodCache=${contentPeriodResult.total}, generatedAt=${snapshot.generatedAt || '-'}`,
      );
      await this.cacheRemoteSchedulePlaylistContent(snapshot);
      await this.syncContentPeriodsForManifests(
        [
          this.currentContentManifest,
          ...buildManifestsFromRemoteSchedulePlaylists(snapshot, this.config.manifest.preserveAspectRatio),
        ],
        'updateschedule',
        false,
      );
    } finally {
      this.remoteScheduleUpdateInProgress = false;
    }
    await this.applyRemoteScheduleSnapshot(snapshot, Date.now(), { force: true });
    return true;
  }

  private async applyUpdateContentPeriodCommand(payload: UpdatePayload | null): Promise<boolean> {
    let result = saveContentPeriodsFromUpdatePayload(payload ?? ({} as UpdatePayload));
    const requestedGuids = payload?.ContentPeriodUpdateGuids ?? [];
    if (requestedGuids.length > 0) {
      result = await this.syncContentPeriodsByGuids(requestedGuids, 'updatecontentperiod') ?? result;
    }
    this.setMessage(`기간별 콘텐츠 스케줄 적용: ${result.upserted}건`);
    this.logger.info(
      'command',
      `updatecontentperiod 적용 완료: requested=${result.requested}, upserted=${result.upserted}, removed=${result.removed}, total=${result.total}`,
    );
    await this.refreshPlaybackForContentPeriodChange();
    return true;
  }

  private async refreshPlaybackForContentPeriodChange(): Promise<void> {
    if (!this.contentPlaybackAllowed) {
      return;
    }

    if (this.activeRemoteSchedulePlaylistName) {
      const snapshot = loadRemoteSchedule();
      const manifest = snapshot
        ? buildManifestFromRemoteSchedulePlaylist(
          snapshot,
          this.activeRemoteSchedulePlaylistName,
          this.config.manifest.preserveAspectRatio,
        )
        : null;
      if (!manifest) {
        this.logger.warn('command', `기간별 콘텐츠 스케줄 반영 보류: 활성 예약 playlist 데이터를 찾지 못했습니다(${this.activeRemoteSchedulePlaylistName}).`);
        return;
      }

      const pagePlans = this.createContentPagePlans(manifest);
      if (pagePlans.length === 0) {
        const hasContentItems = this.hasManifestContentItems(manifest);
        const resolution = hasContentItems
          ? this.createBlackoutPagePlans(manifest)
          : this.createEmptyIntroPagePlans(manifest);
        const reason = hasContentItems ? '기간 밖' : '데이터 없음';
        this.logger.warn('command', `활성 예약 playlist에 현재 재생 가능한 콘텐츠가 없습니다(${reason}): ${manifest.playlistName}`);
        await this.applyRebuiltPlaybackPlans(resolution.pagePlans, resolution.mode);
        return;
      }

      await this.applyRebuiltPlaybackPlans(pagePlans, 'content');
      return;
    }

    const resolution = this.createEffectiveUpdatePagePlans(this.currentContentManifest);
    await this.applyRebuiltPlaybackPlans(resolution.pagePlans, resolution.mode);
  }

  private async applyRebuiltPlaybackPlans(
    pagePlans: readonly SeamlessPagePlan[],
    playbackMode: PlaybackMode,
    options: { readonly force?: boolean } = {},
  ): Promise<void> {
    if (pagePlans.length === 0) {
      return;
    }

    if (options.force !== true && this.playbackPlanSignature(this.pagePlans, this.playbackMode) === this.playbackPlanSignature(pagePlans, playbackMode)) {
      return;
    }

    const targetPageIndex = Math.min(this.pageIndex, pagePlans.length - 1);
    if (this.broadcastOnAir) {
      await this.playPage(targetPageIndex, {
        preservePreviousUntilReady: true,
        commitPageTimelineBeforeSurfaceSwap: true,
        pagePlans,
        playbackMode,
      });
      return;
    }

    this.commitPlaybackPlan(pagePlans, playbackMode, targetPageIndex);
    this.render();
    this.writeRuntimeHealth('content-period-applied');
  }

  private playbackPlanSignature(pagePlans: readonly SeamlessPagePlan[], playbackMode: PlaybackMode): string {
    return [
      playbackMode,
      ...pagePlans.map((page) => [
        page.playlistName,
        page.pageName,
        page.durationSeconds,
        ...page.slots.map((slot) => slot.items
          .filter((item) => this.isContentItemPlayable(item))
          .map((item) => item.id)
          .join(',')),
      ].join(':')),
    ].join('|');
  }

  private async cacheRemoteSchedulePlaylistContent(snapshot: RemoteScheduleSnapshot): Promise<void> {
    const manifests = buildManifestsFromRemoteSchedulePlaylists(
      snapshot,
      this.config.manifest.preserveAspectRatio,
    );
    if (manifests.length === 0) {
      return;
    }

    const cacheOptions = this.buildRemoteContentOptions();
    for (const manifest of manifests) {
      const remoteContentCount = countRemoteManifestContentForOptions(manifest, cacheOptions);
      if (remoteContentCount === 0) {
        continue;
      }

      this.logger.info('schedule', `예약 playlist 캐시 시작: ${manifest.playlistName}, contents=${remoteContentCount}`);
      const cachedManifest = await cacheRemoteManifestContent(manifest, {
        ...cacheOptions,
        cacheNamespace: `updateschedule-${manifest.playlistName}`,
      });
      this.replaceRemoteSchedulePlaylistPages(snapshot, cachedManifest);
      this.logger.info('schedule', `예약 playlist 캐시 완료: ${manifest.playlistName}`);
    }

    saveRemoteScheduleSnapshot(snapshot);
  }

  private replaceRemoteSchedulePlaylistPages(snapshot: RemoteScheduleSnapshot, manifest: RuntimeConfig['manifest']): void {
    if (!snapshot.schedule || typeof snapshot.schedule !== 'object') {
      return;
    }

    const scheduleObject = snapshot.schedule as { Playlists?: unknown; playlists?: unknown };
    const playlistCollection = scheduleObject.Playlists ?? scheduleObject.playlists;
    const playlists = Array.isArray(playlistCollection)
      ? playlistCollection
      : playlistCollection && typeof playlistCollection === 'object'
        ? Object.values(playlistCollection)
        : [];
    const target = playlists.find((playlist) => {
      if (!playlist || typeof playlist !== 'object') {
        return false;
      }

      const candidate = playlist as {
        PlaylistName?: string;
        playlistName?: string;
        PageList?: { PLI_PageListName?: string };
        pageList?: { PLI_PageListName?: string };
      };
      const playlistName = candidate.PlaylistName || candidate.playlistName || candidate.PageList?.PLI_PageListName || candidate.pageList?.PLI_PageListName || '';
      return playlistName.localeCompare(manifest.playlistName, undefined, { sensitivity: 'accent' }) === 0;
    });
    if (target && typeof target === 'object') {
      (target as { Pages?: RuntimeConfig['manifest']['pages']; pages?: RuntimeConfig['manifest']['pages'] }).Pages = manifest.pages;
    }
  }

  private async applyBroadcastSchedule(source: string): Promise<void> {
    if (this.destroyed) {
      return;
    }

    const evaluation = evaluateWeeklySchedule(new Date(), loadWeeklySchedule());
    const wasOnAir = this.broadcastOnAir;
    this.broadcastOnAir = evaluation.isOnAir;
    this.view.broadcastStandby.classList.toggle('broadcast-standby--hidden', evaluation.isOnAir);
    this.view.broadcastStandby.setAttribute('aria-hidden', String(evaluation.isOnAir));

    if (!evaluation.isOnAir) {
      if (this.playing || this.slotPlayers.length > 0) {
        this.stopPlayback();
      }
      this.applyPanelMute(true, source);
      this.setMessage(`방송시간 외 대기: ${evaluation.reason}`);
      if (wasOnAir) {
        this.logger.info('schedule', `off-air (${source}): ${evaluation.reason}`);
        await this.sendHeartbeatNow();
      }
      this.render();
      this.writeRuntimeHealth('schedule-off-air');
      return;
    }

    if (!wasOnAir || !this.hasActivePlaybackSurface()) {
      this.logger.info('schedule', `on-air (${source}): ${evaluation.reason}`);
      this.applyPanelMute(false, source);
      await this.playPage(this.pageIndex);
      await this.sendHeartbeatNow();
      return;
    }

    this.applyPanelMute(false, source);

    this.render();
    this.writeRuntimeHealth('schedule-on-air');
  }

  private applyPanelMute(muted: boolean, source: string): void {
    if (this.panelMuted === muted) {
      return;
    }

    setSsspPanelMute(muted);
    this.panelMuted = muted;
    this.logger.info('sssp', `panel mute ${muted ? 'ON' : 'OFF'} (${source})`);
  }

  private async sendHeartbeatNow(): Promise<void> {
    const reporter = this.heartbeatReporter;
    if (!reporter) {
      return;
    }

    await reporter.sendNow();
  }

  private async sendStoppedHeartbeatForDevicePowerCommand(): Promise<void> {
    if (this.offlineHeartbeatRequested) {
      return;
    }

    this.offlineHeartbeatRequested = true;
    const reporter = this.heartbeatReporter;
    this.heartbeatReporter = null;
    if (!reporter) {
      return;
    }

    try {
      await reporter.stop();
    } catch (error) {
      this.logger.warn('heartbeat', `전원 명령 전 stopped heartbeat 전송 실패: ${formatError(error)}`);
    }
  }

  private async ensureAuthentication(): Promise<void> {
    if (!this.communication) {
      this.setAuthStatus('not-configured', '통신 설정 미완료');
      return;
    }

    this.setAuthStatus('checking', 'LicenseHub 인증 확인 중');
    this.contentPlaybackAllowed = false;
    this.authService = new LicenseHubAuthService({
      playerGuid: this.communication.playerGuid,
      playerName: this.communication.playerName,
      appVersion: this.getHeartbeatVersion(),
    });
    this.authOverlay = new LicenseAuthOverlay({
      service: this.authService,
      onStatus: (status, detail) => {
        this.setAuthStatus(status, detail);
      },
    });

    let state: LicenseAuthState;
    try {
      state = await this.authService.validateStoredOrBootstrap();
    } catch (error) {
      state = {
        isValid: false,
        mode: 'NONE',
        status: 'failed',
        reason: formatError(error),
        deviceFingerprint: '',
        deviceId: '',
        licenseToken: '',
        serverChecked: false,
        usedOfflineFallback: false,
      };
    }

    if (state.isValid) {
      this.contentPlaybackAllowed = true;
      this.setAuthStatus('authenticated', `${state.mode} ${state.status}`);
      this.lastAuthState = state;
      await this.syncPlayerInfo(state);
      return;
    }

    this.showLoading('LicenseHub 인증 필요', state.reason || '인증을 완료해야 플레이어를 시작할 수 있습니다.');
    const resolved = await this.authOverlay.open(state.reason || 'LicenseHub 인증을 진행해 주세요.');
    if (!resolved.isValid) {
      await this.handleAuthenticationCancellation(resolved.reason || 'LicenseHub 인증이 완료되지 않았습니다.', false);
      return;
    }

    this.contentPlaybackAllowed = true;
    this.setAuthStatus('authenticated', `${resolved.mode} ${resolved.status}`);
    this.lastAuthState = resolved;
    await this.syncPlayerInfo(resolved);
  }

  private async openAuthenticationOverlay(message: string): Promise<void> {
    if (!this.communication) {
      this.setMessage('통신 설정 완료 후 인증을 진행할 수 있습니다.');
      return;
    }

    if (!this.authService) {
      this.authService = new LicenseHubAuthService({
        playerGuid: this.communication.playerGuid,
        playerName: this.communication.playerName,
        appVersion: this.getHeartbeatVersion(),
      });
    }

    if (!this.authOverlay) {
      this.authOverlay = new LicenseAuthOverlay({
        service: this.authService,
        onStatus: (status, detail) => {
          this.setAuthStatus(status, detail);
        },
      });
    }

    const resolved = await this.authOverlay.open(message);
    if (resolved.isValid) {
      this.contentPlaybackAllowed = true;
      this.setAuthStatus('authenticated', `${resolved.mode} ${resolved.status}`);
      this.lastAuthState = resolved;
      let syncError: string | null = null;
      try {
        await this.syncPlayerInfo(resolved, { hideLoadingWhenDone: true });
      } catch (error) {
        this.hideLoading();
        syncError = formatError(error);
        this.logger.warn('communication', `인증 후 PlayerInfoManager 동기화 실패: ${syncError}`);
      }
      await this.enterAuthenticatedContentPlayback();
      this.setMessage(syncError
        ? `LicenseHub 인증 완료, PlayerInfo 동기화 실패: ${syncError}`
        : 'LicenseHub 인증이 완료되었습니다.');
      return;
    }

    await this.handleAuthenticationCancellation(resolved.reason || 'LicenseHub 인증이 완료되지 않았습니다.', true);
  }

  private async syncPlayerInfo(authState: LicenseAuthState | null, uiOptions: PlayerInfoSyncUiOptions = {}): Promise<void> {
    if (!this.communication) {
      return;
    }

    const showLoading = uiOptions.showLoading ?? true;
    if (showLoading) {
      this.showLoading('플레이어 정보 동기화 중', 'PlayerInfoManager 정보를 갱신합니다.');
    }

    try {
      await syncPlayerInfoMessage({
        managerAddress: this.config.settings.managerAddress,
        playerGuid: this.communication.playerGuid,
        playerName: this.communication.playerName,
        appVersion: this.getHeartbeatVersion(),
        authState,
      });
      this.logger.info('communication', `PlayerInfoManager 동기화 완료: ${this.communication.playerName}/${this.communication.playerGuid}`);
    } finally {
      if (uiOptions.hideLoadingWhenDone) {
        this.hideLoading();
      }
    }
  }

  private async playPage(index: number, options: PagePlayOptions = {}): Promise<void> {
    if (this.destroyed || !this.broadcastOnAir) {
      return;
    }

    const targetPagePlans = options.pagePlans ?? this.pagePlans;
    const targetPlaybackMode = options.playbackMode ?? this.playbackMode;
    if (targetPagePlans.length === 0) {
      throw new Error('재생할 페이지가 없습니다.');
    }

    const targetPageIndex = (index + targetPagePlans.length) % targetPagePlans.length;
    const avplayPool = targetPlaybackMode === 'content' ? this.ensureAvplayPool() : null;
    const preserveActiveSurface = options.preservePreviousUntilReady === true && this.hasActivePlaybackSurface();
    const preservePrevious = preserveActiveSurface
      && targetPlaybackMode === 'content';
    const preserveIntroTransition = preserveActiveSurface
      && targetPlaybackMode === 'empty-intro';
    const previousSlotPlayers = preservePrevious ? [...this.slotPlayers] : [];
    const previousSlotElements = preservePrevious
      ? Array.from(this.view.stage.querySelectorAll<HTMLElement>('.slot'))
      : [];

    if (!preservePrevious && !preserveIntroTransition) {
      this.clearTimers();
      this.stopSlots();
      avplayPool?.resetLeases();
      this.removeStageSlots();
      this.removeEmptyIntroVideo();
      this.commitPlaybackPlan(targetPagePlans, targetPlaybackMode, targetPageIndex);
    }

    const page = targetPagePlans[targetPageIndex]!;
    if (preservePrevious && targetPlaybackMode === 'content') {
      await this.switchSurfacesToPage(
        targetPagePlans,
        targetPlaybackMode,
        targetPageIndex,
        page,
        options.commitPageTimelineBeforeSurfaceSwap === true,
      );
      return;
    }

    if (!preservePrevious && !preserveIntroTransition) {
      this.audioPolicy.applyForPage(page);
    }
    this.view.stage.style.setProperty('--canvas-width', String(page.canvasWidth));
    this.view.stage.style.setProperty('--canvas-height', String(page.canvasHeight));
    this.view.stage.style.aspectRatio = `${page.canvasWidth} / ${page.canvasHeight}`;
    const nextSlotPlayers: SlotPlayer[] = [];
    const nextSlotElements: HTMLElement[] = [];
    const stagedContentShown: StagedContentShown[] = [];
    let transitionCommitted = !preservePrevious;

    if (targetPlaybackMode === 'empty-intro') {
      if (preserveIntroTransition) {
        this.logger.info('page', `prepare ${page.pageName} (${page.durationSeconds}s, empty-intro seamless)`);
        const stagedIntroVideo = await this.playEmptyIntroVideo({
          hidden: true,
          makeCurrent: false,
          recordStarted: false,
          reportErrors: false,
        });
        this.clearTimers();
        this.stopSlots();
        this.removeStageSlots();
        this.removeEmptyIntroVideo();
        this.commitPlaybackPlan(targetPagePlans, targetPlaybackMode, targetPageIndex);
        this.audioPolicy.applyForPage(page);
        stagedIntroVideo.style.visibility = '';
        this.emptyIntroVideoElement = stagedIntroVideo;
      }

      this.slotPlayers.splice(0);
      this.playing = true;
      this.pageStartCount += 1;
      this.pageStartedAt = performance.now();
      this.render();
      this.setMessage(`페이지 재생: ${page.pageName}`);
      this.logger.info('page', `start ${page.pageName} (${page.durationSeconds}s, empty-intro)`);
      if (preserveIntroTransition) {
        this.recordEmptyIntroVideoStarted();
      } else {
        await this.playEmptyIntroVideo();
      }
      this.writeRuntimeHealth('page-started');
      this.startMasterTimer();
      return;
    }

    page.slots.forEach((slot, slotIndex) => {
      if (!this.slotNeedsSurface(slot)) {
        return;
      }

      const element = document.createElement('section');
      element.className = 'slot';
      element.style.left = `${(slot.left / page.canvasWidth) * 100}%`;
      element.style.top = `${(slot.top / page.canvasHeight) * 100}%`;
      element.style.width = `${(slot.width / page.canvasWidth) * 100}%`;
      element.style.height = `${(slot.height / page.canvasHeight) * 100}%`;
      element.style.zIndex = String(slot.zIndex);
      if (preservePrevious) {
        element.style.visibility = 'hidden';
      }
      this.view.stage.appendChild(element);

      const leaseSlotIndex = preservePrevious ? slotIndex + TRANSITION_SLOT_INDEX_OFFSET : slotIndex;
      nextSlotElements.push(element);
      const slotPlayer = new SlotPlayer(
        slotIndex,
        element,
        slot,
        this.config.manifest.preserveAspectRatio,
        this.config.settings.switchOnContentEnd,
        () => avplayPool!.acquire(leaseSlotIndex),
        this.logger,
        (shownSlotIndex, item) => {
          if (transitionCommitted) {
            this.recordContentShown(shownSlotIndex, item);
            return;
          }

          stagedContentShown.push({ slotIndex: shownSlotIndex, item });
        },
        preservePrevious,
        (session) => avplayPool!.release(session),
        (endedSlotIndex, item) => {
          this.handlePageBoundaryContentEnded(endedSlotIndex, item);
        },
        () => this.isPageTransitionPending(),
        (item) => this.isContentItemPlayable(item),
      );
      slotPlayer.setPageTimeline(0, Math.max(1, page.durationSeconds) * 1000, targetPagePlans.length <= 1);
      nextSlotPlayers.push(slotPlayer);
    });

    if (!preservePrevious) {
      this.slotPlayers.splice(0, this.slotPlayers.length, ...nextSlotPlayers);
      this.playing = true;
      this.pageStartCount += 1;
      this.pageStartedAt = performance.now();
      this.render();
      this.setMessage(`페이지 재생: ${page.pageName}`);
      this.logger.info('page', `start ${page.pageName} (${page.durationSeconds}s)`);
    } else {
      this.logger.info('page', `prepare ${page.pageName} (${page.durationSeconds}s, seamless)`);
    }

    const startResults = await Promise.all(nextSlotPlayers.map((slotPlayer) => slotPlayer.start()));
    if (preservePrevious) {
      const failed = startResults.some((started) => !started);
      if (failed) {
        const failureMessage = this.formatSlotTransitionFailure(nextSlotPlayers);
        nextSlotPlayers.forEach((slotPlayer) => slotPlayer.stop());
        nextSlotElements.forEach((element) => element.remove());
        avplayPool?.releaseTransitionLeases(TRANSITION_SLOT_INDEX_OFFSET);
        throw new Error(`심리스 전환 준비 실패: ${failureMessage}`);
      }

      transitionCommitted = true;
      this.commitPlaybackPlan(targetPagePlans, targetPlaybackMode, targetPageIndex);
      this.audioPolicy.applyForPage(page);
      nextSlotElements.forEach((element) => {
        element.style.visibility = '';
      });
      nextSlotPlayers.forEach((slotPlayer) => slotPlayer.applyDisplayRect());
      await this.waitForPaint();
      previousSlotPlayers.forEach((slotPlayer) => slotPlayer.stop());
      previousSlotElements.forEach((element) => element.remove());
      this.removeEmptyIntroVideo();
      avplayPool?.commitTransitionLeases(TRANSITION_SLOT_INDEX_OFFSET);
      this.slotPlayers.splice(0, this.slotPlayers.length, ...nextSlotPlayers);
      stagedContentShown.forEach(({ slotIndex, item }) => {
        this.recordContentShown(slotIndex, item);
      });
      this.playing = true;
      this.pageStartCount += 1;
      this.pageStartedAt = performance.now();
      this.render();
      this.setMessage(`페이지 재생: ${page.pageName}`);
      this.logger.info('page', `start ${page.pageName} (${page.durationSeconds}s, seamless)`);
    }
    this.writeRuntimeHealth('page-started');
    this.startMasterTimer();
    this.prepareUpcomingPageFirstImages('page-started');
  }

  private recordContentShown(slotIndex: number, item: SeamlessContentItem): void {
    this.contentShowCount += 1;
    this.lastContent = `slot ${slotIndex + 1}: ${item.name}`;
    this.writeRuntimeHealth('content-shown');
  }

  private async switchSurfacesToPage(
    targetPagePlans: readonly SeamlessPagePlan[],
    targetPlaybackMode: PlaybackMode,
    targetPageIndex: number,
    page: SeamlessPagePlan,
    commitPageTimelineBeforeSurfaceSwap: boolean,
  ): Promise<void> {
    const avplayPool = this.ensureAvplayPool();
    const previousState = commitPageTimelineBeforeSurfaceSwap ? this.capturePlaybackState() : null;
    this.logger.info('page', `prepare ${page.pageName} (${page.durationSeconds}s, surface-swap)`);
    this.view.stage.style.setProperty('--canvas-width', String(page.canvasWidth));
    this.view.stage.style.setProperty('--canvas-height', String(page.canvasHeight));
    this.view.stage.style.aspectRatio = `${page.canvasWidth} / ${page.canvasHeight}`;

    if (commitPageTimelineBeforeSurfaceSwap) {
      this.commitPlaybackPlan(targetPagePlans, targetPlaybackMode, targetPageIndex);
      this.audioPolicy.applyForPage(page);
      this.playing = true;
      this.pageStartCount += 1;
      this.pageStartedAt = performance.now();
      this.render();
      this.writeRuntimeHealth('page-started');
    }

    const startResults = await Promise.all(page.slots.map((slot, slotIndex) => {
      const existing = this.slotPlayers[slotIndex];
      if (!existing && !this.slotNeedsSurface(slot)) {
        return Promise.resolve(true);
      }

      const slotPlayer = existing ?? this.ensureSlotSurface(slotIndex, slot, page, avplayPool);
      slotPlayer.setPageTimeline(0, Math.max(1, page.durationSeconds) * 1000, targetPagePlans.length <= 1);
      return slotPlayer.switchToSlotPlan(slot, page.canvasWidth, page.canvasHeight);
    }));
    const failed = startResults.some((started) => !started);
    if (failed) {
      if (previousState) {
        this.restorePlaybackState(previousState);
        this.render();
      }
      throw new Error(`페이지 surface 전환 준비 실패: ${this.formatSlotTransitionFailure(this.slotPlayers)}`);
    }

    if (!commitPageTimelineBeforeSurfaceSwap) {
      this.commitPlaybackPlan(targetPagePlans, targetPlaybackMode, targetPageIndex);
      this.audioPolicy.applyForPage(page);
    }
    this.removeEmptyIntroVideo();
    this.slotPlayers.forEach((slotPlayer) => slotPlayer.applyDisplayRect());
    await this.waitForPaint();
    if (!commitPageTimelineBeforeSurfaceSwap) {
      this.playing = true;
      this.pageStartCount += 1;
      this.pageStartedAt = performance.now();
    }
    this.render();
    this.setMessage(`페이지 재생: ${page.pageName}`);
    this.logger.info('page', `start ${page.pageName} (${page.durationSeconds}s, surface-swap)`);
    if (!commitPageTimelineBeforeSurfaceSwap) {
      this.writeRuntimeHealth('page-started');
    }
    this.startMasterTimer();
    this.prepareUpcomingPageFirstImages('page-started');
  }

  private prepareUpcomingPageFirstImages(reason: string): void {
    if (this.playbackMode !== 'content' || this.pagePlans.length === 0) {
      return;
    }

    const nextPageIndex = this.pagePlans.length <= 1
      ? this.pageIndex
      : (this.pageIndex + 1) % this.pagePlans.length;
    const nextPage = this.pagePlans[nextPageIndex];
    if (!nextPage) {
      return;
    }

    nextPage.slots.forEach((slot, slotIndex) => {
      const item = this.firstPlayableContentItem(slot);
      if (item?.contentType === 'Image') {
        const slotPlayer = this.slotPlayers[slotIndex];
        if (!slotPlayer) {
          this.logger.warn('image', `standby prepare skipped(${reason}): slot ${slotIndex + 1} surface 없음, ${item.name}`);
          return;
        }

        const preparePromise = slotPlayer.prepareFirstContentForSlotPlan(slot);
        if (preparePromise) {
          void preparePromise
            .then(() => {
              this.logger.debug('image', `standby prepared(${reason}): slot ${slotIndex + 1} ${item.name}`);
            })
            .catch((error) => {
              this.logger.warn('image', `standby prepare failed(${reason}): slot ${slotIndex + 1} ${item.name}: ${formatError(error)}`);
            });
        }
      }
    });
  }

  private slotNeedsSurface(slot: SeamlessSlotPlan): boolean {
    return this.firstPlayableContentItem(slot) !== null && slot.width > 0 && slot.height > 0;
  }

  private ensureSlotSurface(
    slotIndex: number,
    slot: SeamlessSlotPlan,
    page: SeamlessPagePlan,
    avplayPool: AvplaySessionPool,
  ): SlotPlayer {
    const existing = this.slotPlayers[slotIndex];
    if (existing) {
      return existing;
    }

    const element = document.createElement('section');
    element.className = 'slot';
    this.view.stage.appendChild(element);
    const slotPlayer = new SlotPlayer(
      slotIndex,
      element,
      slot,
      this.config.manifest.preserveAspectRatio,
      this.config.settings.switchOnContentEnd,
      () => avplayPool.acquire(slotIndex),
      this.logger,
      (shownSlotIndex, item) => {
        this.recordContentShown(shownSlotIndex, item);
      },
      false,
      (session) => avplayPool.release(session),
      (endedSlotIndex, item) => {
        this.handlePageBoundaryContentEnded(endedSlotIndex, item);
      },
      () => this.isPageTransitionPending(),
      (item) => this.isContentItemPlayable(item),
    );
    slotPlayer.applyLayout(page.canvasWidth, page.canvasHeight);
    slotPlayer.setPageTimeline(0, Math.max(1, page.durationSeconds) * 1000, this.pagePlans.length <= 1);
    this.slotPlayers[slotIndex] = slotPlayer;
    return slotPlayer;
  }

  private formatSlotTransitionFailure(slotPlayers: readonly SlotPlayer[]): string {
    const failures = slotPlayers
      .map((slotPlayer) => slotPlayer.snapshot())
      .filter((snapshot) => snapshot.includes('ERROR:'));

    return failures.length > 0 ? failures.join(' | ') : '새 콘텐츠 슬롯이 시작되지 않았습니다.';
  }

  private waitForPaint(): Promise<void> {
    return new Promise((resolve) => {
      window.requestAnimationFrame(() => {
        window.requestAnimationFrame(() => resolve());
      });
    });
  }

  private startMasterTimer(): void {
    if (this.masterTimerId !== null || this.destroyed) {
      return;
    }

    this.masterTickNextAt = performance.now() + MASTER_TICK_INTERVAL_MS;
    this.lastMasterTickAt = 0;
    this.lastMasterTickDelayMs = 0;
    this.lastMasterTickIntervalMs = 0;
    this.scheduleNextMasterTick();
  }

  private stopMasterTimer(): void {
    if (this.masterTimerId !== null) {
      window.clearTimeout(this.masterTimerId);
      this.masterTimerId = null;
    }
    this.masterTickNextAt = 0;
    this.lastMasterTickAt = 0;
    this.lastMasterTickDelayMs = 0;
    this.lastMasterTickIntervalMs = 0;
  }

  private scheduleNextMasterTick(): void {
    if (this.destroyed || this.masterTimerId !== null) {
      return;
    }

    if (this.masterTickNextAt <= 0) {
      this.masterTickNextAt = performance.now() + MASTER_TICK_INTERVAL_MS;
    }

    const expectedTickAt = this.masterTickNextAt;
    const delayMs = Math.max(0, expectedTickAt - performance.now());
    this.masterTimerId = window.setTimeout(() => {
      this.masterTimerId = null;
      const firedAt = performance.now();
      this.lastMasterTickDelayMs = Math.max(0, firedAt - expectedTickAt);
      this.lastMasterTickIntervalMs = this.lastMasterTickAt > 0
        ? Math.max(0, firedAt - this.lastMasterTickAt)
        : MASTER_TICK_INTERVAL_MS;
      this.lastMasterTickAt = firedAt;
      if (this.lastMasterTickDelayMs >= MASTER_TICK_LAG_WARN_MS) {
        this.logger.warn(
          'timer',
          `master tick 지연 ${Math.round(this.lastMasterTickDelayMs)}ms interval=${Math.round(this.lastMasterTickIntervalMs)}ms page=${this.pageIndex + 1}/${this.pagePlans.length} elapsed=${this.formatSeconds(this.currentPageRawElapsedMilliseconds())}`,
        );
        this.writeRuntimeHealth('timer-lag');
      }
      this.runMasterTick();
      const nowMs = performance.now();
      do {
        this.masterTickNextAt += MASTER_TICK_INTERVAL_MS;
      } while (this.masterTickNextAt <= nowMs);
      this.scheduleNextMasterTick();
    }, delayMs);
  }

  private runMasterTick(): void {
    if (this.destroyed) {
      return;
    }

    const pageElapsedMs = this.currentPageElapsedMilliseconds();
    const rawPageElapsedMs = this.currentPageRawElapsedMilliseconds();
    this.render();
    const pageTransitionStarted = this.checkPageTransition(rawPageElapsedMs);
    if (!pageTransitionStarted && !this.pageTransitionInProgress && !this.isCurrentPageExpired(rawPageElapsedMs)) {
      this.scheduleSlotTimelineSync(pageElapsedMs, this.currentPageDurationMilliseconds(), this.pagePlans.length <= 1);
    }
    this.checkSchedules();
  }

  private scheduleSlotTimelineSync(pageElapsedMs: number, pageDurationMs: number, loopCurrentPageAtPageEnd: boolean): void {
    if (!this.playing || this.slotTimelineSyncInProgress || this.slotPlayers.length === 0) {
      return;
    }

    this.slotTimelineSyncInProgress = true;
    void Promise.all(this.slotPlayers.map((slotPlayer) => (
      slotPlayer.syncToPageElapsed(pageElapsedMs, pageDurationMs, loopCurrentPageAtPageEnd)
    )))
      .catch((error) => {
        this.logger.warn('slot', `master tick 슬롯 동기화 실패: ${formatError(error)}`);
      })
      .finally(() => {
        this.slotTimelineSyncInProgress = false;
      });
  }

  private checkPageTransition(pageElapsedMs: number): boolean {
    if (
      !this.playing
      || this.pagePlans.length <= 1
      || this.pageTransitionInProgress
      || this.contentReplacementInProgress
    ) {
      return false;
    }

    if (!this.isCurrentPageExpired(pageElapsedMs)) {
      return false;
    }

    const pageDurationMs = this.currentPageDurationMilliseconds();
    this.slotPlayers.forEach((slotPlayer) => {
      slotPlayer.setPageTimeline(pageElapsedMs, pageDurationMs, this.pagePlans.length <= 1);
    });

    if (this.slotPlayers.some((slotPlayer) => slotPlayer.blocksPageTransitionForContentEnd())) {
      return false;
    }

    this.startScheduledPageTransition('timer');
    return true;
  }

  private handlePageBoundaryContentEnded(slotIndex: number, item: SeamlessContentItem): void {
    if (
      !this.playing
      || this.pagePlans.length <= 1
      || this.pageTransitionInProgress
      || this.contentReplacementInProgress
      || !this.isCurrentPageExpired(this.currentPageRawElapsedMilliseconds())
    ) {
      return;
    }

    this.logger.info('page', `slot ${slotIndex + 1} 종료 이벤트로 예약 페이지 전환: ${item.name}`);
    this.startScheduledPageTransition('content-end');
  }

  private isPageTransitionPending(): boolean {
    return this.pageTransitionInProgress || this.isCurrentPageExpired(this.currentPageRawElapsedMilliseconds());
  }

  private startScheduledPageTransition(source: 'timer' | 'content-end'): void {
    if (this.pageTransitionInProgress) {
      return;
    }

    this.pageTransitionInProgress = true;
    void this.playPage(this.pageIndex + 1, {
      preservePreviousUntilReady: true,
      commitPageTimelineBeforeSurfaceSwap: true,
    })
      .catch((error) => {
        this.logger.error('page', `페이지 전환 실패(${source}): ${formatError(error)}`);
        this.setMessage(`페이지 전환 실패: ${formatError(error)}`);
      })
      .finally(() => {
        this.pageTransitionInProgress = false;
      });
  }

  private checkSchedules(): void {
    const nowMs = Date.now();
    const currentSecond = Math.floor(nowMs / SCHEDULE_CHECK_INTERVAL_MS);
    if (currentSecond !== this.lastScheduleCheckSecond) {
      this.lastScheduleCheckSecond = currentSecond;
      this.checkReservationScheduleLookahead(nowMs);
    }

    const currentMinute = Math.floor(nowMs / 60000);
    this.checkContentPeriodBoundary(currentMinute);
    if (currentMinute === this.lastBroadcastScheduleCheckMinute || this.scheduleCheckInProgress) {
      return;
    }

    this.lastBroadcastScheduleCheckMinute = currentMinute;
    this.scheduleCheckInProgress = true;
    void this.applyBroadcastSchedule('master-tick')
      .catch((error) => {
        this.logger.warn('schedule', `master tick 방송 스케줄 체크 실패: ${formatError(error)}`);
      })
      .finally(() => {
        this.scheduleCheckInProgress = false;
      });
  }

  private checkContentPeriodBoundary(currentMinute: number): void {
    if (
      currentMinute === this.lastContentPeriodCheckMinute
      || !this.broadcastOnAir
      || !this.playing
      || this.contentReplacementInProgress
      || this.pageTransitionInProgress
      || this.remoteScheduleSwitchInProgress
    ) {
      return;
    }

    this.lastContentPeriodCheckMinute = currentMinute;
    void this.refreshPlaybackForContentPeriodChange()
      .catch((error) => {
        this.logger.warn('schedule', `기간별 콘텐츠 경계 재평가 실패: ${formatError(error)}`);
      });
  }

  private checkReservationScheduleLookahead(nowMs: number): void {
    if (this.remoteScheduleUpdateInProgress) {
      return;
    }

    const snapshot = loadRemoteSchedule();
    if (!snapshot) {
      return;
    }

    void this.applyRemoteScheduleSnapshot(snapshot, nowMs)
      .catch((error) => {
        this.logger.warn('schedule', `예약 스케줄 적용 실패: ${formatError(error)}`);
      });
  }

  private async applyRemoteScheduleSnapshot(
    snapshot: RemoteScheduleSnapshot,
    nowMs: number,
    options: { readonly force?: boolean } = {},
  ): Promise<void> {
    if (this.remoteScheduleSwitchInProgress || this.remoteScheduleUpdateInProgress || this.contentReplacementInProgress) {
      return;
    }

    const fallbackPlaylistName = this.currentContentManifest.playlistName;
    const decision = evaluateRemoteSchedule(snapshot, new Date(nowMs), fallbackPlaylistName);
    const lookaheadDecision = evaluateRemoteSchedule(snapshot, new Date(nowMs + 3000), fallbackPlaylistName);
    if (
      lookaheadDecision.nextSwitchAtMs > 0
      && lookaheadDecision.nextSwitchAtMs <= nowMs + 3000
      && lookaheadDecision.nextPlaylistName
    ) {
      this.logger.debug(
        'schedule',
        `예약 스케줄 lookahead: ${new Date(lookaheadDecision.nextSwitchAtMs).toISOString()} -> ${lookaheadDecision.nextPlaylistName}`,
      );
    }

    const activeKey = decision.isFromSchedule ? decision.playlistName : null;
    if (options.force !== true && this.activeRemoteSchedulePlaylistName === activeKey) {
      return;
    }

    this.remoteScheduleSwitchInProgress = true;
    try {
      await this.applyRemoteScheduleDecision(snapshot, decision);
    } finally {
      this.remoteScheduleSwitchInProgress = false;
    }
  }

  private async applyRemoteScheduleDecision(
    snapshot: RemoteScheduleSnapshot,
    decision: RemoteScheduleDecision,
  ): Promise<void> {
    if (this.destroyed || !this.broadcastOnAir) {
      return;
    }

    if (!decision.isFromSchedule) {
      if (this.activeRemoteSchedulePlaylistName === null) {
        return;
      }

      const resolution = this.createEffectiveUpdatePagePlans(this.currentContentManifest);
      await this.playPage(0, {
        preservePreviousUntilReady: true,
        commitPageTimelineBeforeSurfaceSwap: true,
        pagePlans: resolution.pagePlans,
        playbackMode: resolution.mode,
      });
      this.activeRemoteSchedulePlaylistName = null;
      this.setMessage(`예약 스케줄 종료: ${this.currentContentManifest.playlistName}`);
      this.logger.info('schedule', `예약 스케줄 종료, 기본 playlist=${this.currentContentManifest.playlistName}`);
      return;
    }

    const manifest = buildManifestFromRemoteSchedulePlaylist(
      snapshot,
      decision.playlistName,
      this.config.manifest.preserveAspectRatio,
    );
    if (!manifest) {
      this.logger.warn('schedule', `예약 스케줄 playlist 데이터를 찾지 못했습니다: ${decision.playlistName}`);
      return;
    }

    if (!this.contentPlaybackAllowed) {
      this.logger.info('schedule', `LicenseHub 미인증 상태라 예약 playlist 적용을 보류합니다: ${decision.playlistName}`);
      return;
    }

    const pagePlans = this.createContentPagePlans(manifest);
    if (pagePlans.length === 0) {
      const hasContentItems = this.hasManifestContentItems(manifest);
      const resolution = hasContentItems
        ? this.createBlackoutPagePlans(manifest)
        : this.createEmptyIntroPagePlans(manifest);
      await this.applyRebuiltPlaybackPlans(resolution.pagePlans, resolution.mode);
      if (hasContentItems) {
        this.activeRemoteSchedulePlaylistName = decision.playlistName;
        this.setMessage(`예약 스케줄 콘텐츠 기간 외: ${decision.playlistName}`);
        this.logger.info('schedule', `예약 playlist의 모든 콘텐츠가 현재 기간 밖이라 검은 화면으로 전환합니다: playlist=${decision.playlistName}, schedule=${decision.scheduleId || '-'}`);
      } else {
        this.setMessage(`예약 스케줄 데이터 없음: ${decision.playlistName}`);
        this.logger.warn('schedule', `예약 playlist에 재생 가능한 콘텐츠 데이터가 없어 인트로로 전환합니다: playlist=${decision.playlistName}, schedule=${decision.scheduleId || '-'}`);
      }
      return;
    }

    await this.playPage(0, {
      preservePreviousUntilReady: true,
      commitPageTimelineBeforeSurfaceSwap: true,
      pagePlans,
      playbackMode: 'content',
    });
    this.activeRemoteSchedulePlaylistName = decision.playlistName;
    this.setMessage(`예약 스케줄 적용: ${decision.playlistName}`);
    this.logger.info('schedule', `예약 스케줄 적용: playlist=${decision.playlistName}, schedule=${decision.scheduleId || '-'}`);
  }

  private stopSlots(): void {
    this.slotPlayers.forEach((slotPlayer) => slotPlayer.stop());
    this.slotPlayers.splice(0);
  }

  private removeStageSlots(): void {
    this.view.stage.querySelectorAll('.slot').forEach((element) => element.remove());
  }

  private async playEmptyIntroVideo(options: EmptyIntroVideoOptions = {}): Promise<HTMLVideoElement> {
    const makeCurrent = options.makeCurrent ?? true;
    const recordStarted = options.recordStarted ?? true;
    const reportErrors = options.reportErrors ?? true;
    const video = document.createElement('video');
    video.className = 'empty-intro-video';
    if (options.hidden === true) {
      video.style.visibility = 'hidden';
    }
    video.src = TIZEN_INTRO_VIDEO_FILE;
    video.autoplay = true;
    video.loop = true;
    video.muted = true;
    video.defaultMuted = true;
    video.playsInline = true;
    video.preload = 'auto';
    video.style.objectFit = this.config.manifest.preserveAspectRatio ? 'contain' : 'fill';
    video.setAttribute('aria-hidden', 'true');
    video.setAttribute('playsinline', '');
    video.setAttribute('webkit-playsinline', '');
    video.addEventListener('error', () => {
      const message = this.formatEmptyIntroVideoError(video);
      if (reportErrors) {
        this.reportEmptyIntroVideoError(message);
      } else {
        this.logger.error('intro', message);
      }
    }, { once: true });

    this.view.stage.appendChild(video);
    if (makeCurrent) {
      this.emptyIntroVideoElement = video;
    }

    try {
      await video.play();
    } catch (error) {
      const message = `Tizen 인트로 영상 재생 실패: ${formatError(error)}`;
      if (reportErrors) {
        this.reportEmptyIntroVideoError(message);
      } else {
        this.logger.error('intro', message);
      }
      video.remove();
      throw new Error(message);
    }

    if (recordStarted) {
      this.recordEmptyIntroVideoStarted();
    }
    return video;
  }

  private recordEmptyIntroVideoStarted(): void {
    this.contentShowCount += 1;
    this.lastContent = `empty-intro: ${TIZEN_INTRO_VIDEO_FILE}`;
    this.logger.info('intro', `Tizen intro video started: ${TIZEN_INTRO_VIDEO_FILE}`);
    this.writeRuntimeHealth('content-shown');
  }

  private removeEmptyIntroVideo(): void {
    const video = this.emptyIntroVideoElement;
    if (!video) {
      return;
    }

    try {
      video.pause();
      video.removeAttribute('src');
      video.load();
    } catch (error) {
      this.logger.warn('intro', `Tizen intro video 정리 실패: ${formatError(error)}`);
    }

    video.remove();
    this.emptyIntroVideoElement = null;
  }

  private applyEmptyIntroDisplayMode(): void {
    if (!this.emptyIntroVideoElement) {
      return;
    }

    this.emptyIntroVideoElement.style.objectFit = this.config.manifest.preserveAspectRatio ? 'contain' : 'fill';
  }

  private formatEmptyIntroVideoError(video: HTMLVideoElement): string {
    const code = video.error?.code ?? 0;
    const detail = video.error?.message || `media error code ${code}`;
    return `Tizen 인트로 영상을 로드하지 못했습니다: ${TIZEN_INTRO_VIDEO_FILE} (${detail})`;
  }

  private reportEmptyIntroVideoError(message: string): void {
    this.logger.error('intro', message);
    this.setHudVisible(true);
    this.setMessage(message);
    this.showLoading('플레이어 오류', message, true);
    this.writeRuntimeHealth('empty-intro-error');
  }

  private hasActivePlaybackSurface(): boolean {
    return this.slotPlayers.length > 0 || this.emptyIntroVideoElement !== null;
  }

  private clearTimers(): void {
    this.pageTransitionInProgress = false;
    this.slotTimelineSyncInProgress = false;
  }

  private currentPageElapsedMilliseconds(): number {
    const durationMs = this.currentPageDurationMilliseconds();
    if (!this.playing) {
      return this.pagePlans.length <= 1
        ? this.pagePausedElapsedMs % durationMs
        : Math.min(this.pagePausedElapsedMs, durationMs);
    }

    const elapsedMs = this.currentPageRawElapsedMilliseconds();
    if (this.pagePlans.length <= 1) {
      return elapsedMs % durationMs;
    }

    return Math.min(elapsedMs, durationMs);
  }

  private currentPageRawElapsedMilliseconds(): number {
    if (!this.playing) {
      return this.pagePausedElapsedMs;
    }

    return Math.max(0, performance.now() - this.pageStartedAt);
  }

  private currentPageDurationMilliseconds(): number {
    return Math.max(1, this.pagePlans[this.pageIndex].durationSeconds) * 1000;
  }

  private isCurrentPageExpired(pageElapsedMs: number): boolean {
    return this.pagePlans.length > 1 && pageElapsedMs >= this.currentPageDurationMilliseconds();
  }

  private render(): void {
    const renderAt = performance.now();
    this.lastRenderIntervalMs = this.lastRenderAt > 0
      ? Math.max(0, renderAt - this.lastRenderAt)
      : 0;
    this.lastRenderAt = renderAt;
    const page = this.pagePlans[this.pageIndex];
    const pageElapsedMs = this.currentPageElapsedMilliseconds();
    this.view.state.textContent = this.broadcastOnAir ? this.playing ? 'playing' : 'paused' : 'off-air';
    this.view.playlist.textContent = page.playlistName;
    this.view.page.textContent = `${this.pageIndex + 1}/${this.pagePlans.length} ${page.pageName}`;
    this.view.elapsed.textContent = this.formatPageElapsedText(page, pageElapsedMs);
    this.view.lastKey.textContent = this.lastRemoteKey;
    this.view.lastAction.textContent = this.lastRemoteAction;
    this.view.platform.textContent = formatRuntimeDiagnostics(collectRuntimeDiagnostics());
    this.view.communication.textContent = this.formatCommunicationStatus();
    this.view.auth.textContent = `LicenseHub: ${this.authStatus} (${this.authStatusDetail})`;
    this.view.update.textContent = this.formatUpdateOverlayStatus();
    this.view.slots.textContent = this.slotPlayers.map((slotPlayer) => slotPlayer.snapshot()).join('\n') || '-';
    this.view.timeline.innerHTML = this.formatPlaybackTimeline(page, pageElapsedMs);
  }

  private formatPageElapsedText(page: SeamlessPagePlan, elapsedMs: number): string {
    const durationMs = Math.max(1, page.durationSeconds) * 1000;
    const remainingMs = Math.max(0, durationMs - elapsedMs);
    const nextPage = this.pagePlans.length > 1
      ? this.pagePlans[(this.pageIndex + 1) % this.pagePlans.length]
      : null;
    const nextText = nextPage ? ` / 다음 페이지 ${this.formatSeconds(remainingMs)} 후` : '';
    return `${this.formatSeconds(elapsedMs)} / ${this.formatSeconds(durationMs)}${nextText}`;
  }

  private formatPlaybackTimeline(page: SeamlessPagePlan, pageElapsedMs: number): string {
    const pageDurationMs = Math.max(1, page.durationSeconds) * 1000;
    const pageRemainingMs = Math.max(0, pageDurationMs - pageElapsedMs);
    const pageProgress = pageDurationMs > 0 ? Math.min(1, Math.max(0, pageElapsedMs / pageDurationMs)) : 0;
    const nextPage = this.pagePlans.length > 1
      ? this.pagePlans[(this.pageIndex + 1) % this.pagePlans.length]
      : null;
    const pageSwitchText = nextPage
      ? `${this.formatSeconds(pageRemainingMs)} 후 ${this.escapeHtml(nextPage.pageName || `page-${this.pageIndex + 2}`)}`
      : '마지막/단일 페이지';
    const slotSnapshots = this.slotPlayers.map((slotPlayer) => slotPlayer.timelineSnapshot());
    const slotHtml = slotSnapshots.length > 0
      ? slotSnapshots.map((snapshot) => this.formatSlotTimeline(snapshot)).join('')
      : '<div class="timeline-empty">활성 슬롯 없음</div>';
    const tickHtml = this.formatProgressRow({
      label: 'TICK',
      name: 'UI thread / master timer',
      elapsedMs: this.lastMasterTickIntervalMs,
      durationMs: MASTER_TICK_INTERVAL_MS,
      progress: Math.min(1, this.lastMasterTickIntervalMs / MASTER_TICK_INTERVAL_MS),
      detail: `지연 ${this.formatMilliseconds(this.lastMasterTickDelayMs)} · tick ${this.formatMilliseconds(this.lastMasterTickIntervalMs)} · render ${this.formatMilliseconds(this.lastRenderIntervalMs)}`,
      level: this.lastMasterTickDelayMs > MASTER_TICK_INTERVAL_MS ? 'error' : 'tick',
    });

    return `
      <section class="timeline-panel">
        <div class="timeline-panel__header">
          <strong>${this.escapeHtml(page.playlistName)}</strong>
          <span>${this.escapeHtml(`${this.pageIndex + 1}/${this.pagePlans.length} ${page.pageName}`)}</span>
        </div>
        ${this.formatProgressRow({
          label: 'PAGE',
          name: '페이지 진행',
          elapsedMs: pageElapsedMs,
          durationMs: pageDurationMs,
          progress: pageProgress,
          detail: `전환 ${pageSwitchText}`,
          level: 'page',
        })}
        ${tickHtml}
        <div class="timeline-panel__slots">${slotHtml}</div>
      </section>
    `;
  }

  private formatSlotTimeline(snapshot: SlotPlayerTimelineSnapshot): string {
    const itemLabel = snapshot.itemIndex >= 0
      ? `${snapshot.itemIndex + 1}/${snapshot.itemCount}`
      : '-';
    const detail = snapshot.failureMessage
      ? `오류: ${snapshot.failureMessage}`
      : `전환 ${snapshot.nextTransitionText}`;
    return this.formatProgressRow({
      label: `S${snapshot.slotIndex + 1}`,
      name: `${snapshot.slotName} · ${itemLabel} · ${snapshot.contentType} · ${snapshot.itemName}`,
      elapsedMs: snapshot.elapsedMs,
      durationMs: snapshot.durationMs,
      progress: snapshot.progress,
      detail,
      level: snapshot.failureMessage ? 'error' : 'slot',
    });
  }

  private formatProgressRow(options: {
    readonly label: string;
    readonly name: string;
    readonly elapsedMs: number;
    readonly durationMs: number;
    readonly progress: number;
    readonly detail: string;
    readonly level: 'page' | 'slot' | 'tick' | 'error';
  }): string {
    const widthPercent = Math.round(Math.min(1, Math.max(0, options.progress)) * 1000) / 10;
    return `
      <div class="timeline-row timeline-row--${options.level}">
        <div class="timeline-row__top">
          <span class="timeline-row__label">${this.escapeHtml(options.label)}</span>
          <span class="timeline-row__name">${this.escapeHtml(options.name)}</span>
          <span class="timeline-row__time">${this.formatSeconds(options.elapsedMs)} / ${this.formatSeconds(options.durationMs)}</span>
        </div>
        <div class="timeline-row__bar" style="background: linear-gradient(90deg, #a855f7 0%, #a855f7 ${widthPercent}%, rgba(255, 255, 255, 0.18) ${widthPercent}%, rgba(255, 255, 255, 0.18) 100%)" aria-hidden="true"></div>
        <div class="timeline-row__bottom">
          <span>${this.escapeHtml(options.detail)}</span>
        </div>
      </div>
    `;
  }

  private formatSeconds(milliseconds: number): string {
    const seconds = Math.max(0, milliseconds) / 1000;
    return `${seconds.toFixed(1)}s`;
  }

  private formatMilliseconds(milliseconds: number): string {
    return `${Math.round(Math.max(0, milliseconds))}ms`;
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  private showLoading(title: string, message: string, error = false): void {
    if (!this.startupLoadingActive && !error) {
      this.logger.info('loading', `런타임 로딩 오버레이 생략: ${title} - ${message}`);
      return;
    }

    this.view.loadingTitle.textContent = title;
    this.view.loadingMessage.textContent = message;
    this.view.loadingOverlay.classList.remove('loading-overlay--hidden');
    this.view.loadingOverlay.classList.toggle('loading-overlay--error', error);
    this.view.loadingOverlay.classList.toggle('loading-overlay--startup', this.startupLoadingActive);
    this.view.loadingOverlay.classList.toggle('loading-overlay--runtime', !this.startupLoadingActive);
  }

  private hideLoading(): void {
    this.view.loadingOverlay.classList.add('loading-overlay--hidden');
    this.view.loadingOverlay.classList.remove('loading-overlay--error');
    this.view.loadingOverlay.classList.remove('loading-overlay--startup');
    this.view.loadingOverlay.classList.remove('loading-overlay--runtime');
    this.startupLoadingActive = false;
  }

  private createStartupPagePlans(manifest: RuntimeConfig['manifest']): PagePlanResolution {
    const pagePlans = this.createContentPagePlans(manifest);
    if (pagePlans.length > 0) {
      return { pagePlans, mode: 'content' };
    }

    if (this.hasManifestContentItems(manifest)) {
      this.logger.info('manifest', '현재 기간에 표출할 콘텐츠가 없어 검은 화면으로 전환합니다.');
      return this.createBlackoutPagePlans(manifest);
    }

    return this.createEmptyIntroPagePlans(manifest);
  }

  private createUpdatePagePlans(manifest: RuntimeConfig['manifest']): PagePlanResolution {
    const pagePlans = this.createContentPagePlans(manifest);
    if (pagePlans.length > 0) {
      return { pagePlans, mode: 'content' };
    }

    if (this.hasManifestContentItems(manifest)) {
      this.logger.info('manifest', '현재 기간에 표출할 콘텐츠가 없어 검은 화면으로 전환합니다.');
      return this.createBlackoutPagePlans(manifest);
    }

    this.logger.info('manifest', 'updatelist에 재생 가능한 데이터가 없어 empty-intro 모드로 전환합니다.');
    return this.createEmptyIntroPagePlans(manifest);
  }

  private createEffectiveUpdatePagePlans(manifest: RuntimeConfig['manifest']): PagePlanResolution {
    if (!this.contentPlaybackAllowed) {
      this.logger.info('auth', 'LicenseHub 미인증 상태라 콘텐츠 대신 Tizen intro 영상을 유지합니다.');
      return this.createEmptyIntroPagePlans(manifest);
    }

    return this.createUpdatePagePlans(manifest);
  }

  private async handleAuthenticationCancellation(detail: string, startIfOnAir: boolean): Promise<void> {
    const currentState = await this.validateCurrentAuthenticationAfterCancellation();
    if (currentState?.isValid) {
      this.contentPlaybackAllowed = true;
      this.lastAuthState = currentState;
      this.setAuthStatus('authenticated', `${currentState.mode} ${currentState.status}`);
      try {
        await this.syncPlayerInfo(currentState, { showLoading: false });
      } catch (error) {
        this.logger.warn('communication', `인증 취소 후 PlayerInfoManager 동기화 실패: ${formatError(error)}`);
      }

      if (startIfOnAir && this.broadcastOnAir && this.playbackMode !== 'content') {
        await this.enterAuthenticatedContentPlayback();
      }
      this.setMessage('LicenseHub 인증이 유효해 콘텐츠 재생을 유지합니다.');
      return;
    }

    if (this.lastAuthState?.isValid) {
      this.contentPlaybackAllowed = true;
      this.setAuthStatus('authenticated', `${this.lastAuthState.mode} ${this.lastAuthState.status}`);
      this.setMessage('기존 LicenseHub 인증 상태를 유지합니다.');
      return;
    }

    await this.enterUnauthenticatedIntroPlayback(detail, startIfOnAir);
  }

  private async validateCurrentAuthenticationAfterCancellation(): Promise<LicenseAuthState | null> {
    if (!this.authService) {
      return null;
    }

    try {
      return await this.authService.validateStoredOrBootstrap();
    } catch (error) {
      this.logger.warn('auth', `인증 취소 후 LicenseHub 상태 재확인 실패: ${formatError(error)}`);
      return null;
    }
  }

  private async enterUnauthenticatedIntroPlayback(detail: string, startIfOnAir: boolean): Promise<void> {
    this.contentPlaybackAllowed = false;
    this.lastAuthState = null;
    this.setAuthStatus('unauthenticated', detail);
    const resolution = this.createEmptyIntroPagePlans(this.currentContentManifest);
    if (startIfOnAir && this.broadcastOnAir) {
      await this.playPage(0, {
        preservePreviousUntilReady: true,
        pagePlans: resolution.pagePlans,
        playbackMode: resolution.mode,
      });
      this.setMessage('LicenseHub 인증이 취소되어 인트로 영상만 재생합니다.');
      return;
    }

    this.commitPlaybackPlan(resolution.pagePlans, resolution.mode, 0);
    this.setMessage('LicenseHub 미인증 상태라 인트로 영상만 재생합니다.');
    this.render();
    this.writeRuntimeHealth('auth-unauthenticated-intro');
  }

  private async enterAuthenticatedContentPlayback(): Promise<void> {
    const resolution = this.createUpdatePagePlans(this.currentContentManifest);
    if (this.broadcastOnAir) {
      await this.playPage(0, {
        preservePreviousUntilReady: true,
        pagePlans: resolution.pagePlans,
        playbackMode: resolution.mode,
      });
      return;
    }

    this.commitPlaybackPlan(resolution.pagePlans, resolution.mode, 0);
    this.render();
    this.writeRuntimeHealth('auth-authenticated-content-ready');
  }

  private createContentPagePlans(manifest: RuntimeConfig['manifest']): SeamlessPagePlan[] {
    const pagePlans = manifest.pages.map((page) => buildPagePlan(page, manifest.playlistName, this.createContentPeriodPlanOptions()));
    return this.hasPlayablePagePlans(pagePlans) ? pagePlans : [];
  }

  private createEmptyIntroPagePlans(manifest: RuntimeConfig['manifest']): PagePlanResolution {
    this.logger.info('manifest', '재생 가능한 데이터가 없어 Tizen intro 영상을 사용합니다.');
    const introManifest = createTizenIntroManifest(manifest.preserveAspectRatio);
    return {
      pagePlans: introManifest.pages.map((page) => buildPagePlan(page, introManifest.playlistName)),
      mode: 'empty-intro',
    };
  }

  private createBlackoutPagePlans(manifest: RuntimeConfig['manifest']): PagePlanResolution {
    const referencePage = manifest.pages[0];
    return {
      pagePlans: [
        {
          playlistName: manifest.playlistName,
          pageName: 'No active content',
          canvasWidth: referencePage?.PIC_CanvasWidth && referencePage.PIC_CanvasWidth > 0 ? referencePage.PIC_CanvasWidth : 1920,
          canvasHeight: referencePage?.PIC_CanvasHeight && referencePage.PIC_CanvasHeight > 0 ? referencePage.PIC_CanvasHeight : 1080,
          durationSeconds: 60,
          slots: [],
        },
      ],
      mode: 'blackout',
    };
  }

  private hasPlayablePagePlans(pagePlans: readonly SeamlessPagePlan[]): boolean {
    return pagePlans.some((page) =>
      page.slots.some((slot) => slot.width > 0 && slot.height > 0 && this.firstPlayableContentItem(slot) !== null));
  }

  private hasManifestContentItems(manifest: RuntimeConfig['manifest']): boolean {
    const pagePlans = manifest.pages.map((page) => buildPagePlan(page, manifest.playlistName));
    return pagePlans.some((page) =>
      page.slots.some((slot) => slot.width > 0 && slot.height > 0 && slot.items.length > 0));
  }

  private createContentPeriodPlanOptions(now = new Date()): BuildPagePlanOptions {
    return {
      hasContentPeriod: (content) => hasContentPeriod(content.CIF_StrGUID),
      isContentAllowed: (content) => isContentPeriodAllowed(content.CIF_StrGUID, now),
    };
  }

  private isContentItemPlayable(item: SeamlessContentItem): boolean {
    return isContentPeriodAllowed(item.source.CIF_StrGUID, new Date());
  }

  private firstPlayableContentItem(slot: SeamlessSlotPlan): SeamlessContentItem | null {
    return slot.items.find((item) => this.isContentItemPlayable(item)) ?? null;
  }

  private setConnectionStatus(target: 'db' | 'signalr' | 'ftp', status: ConnectionStatus, detail: string): void {
    const stepStatus = this.formatLoadingConnectionStatus(status);
    const stepState = this.loadingStateForConnectionStatus(status);
    if (target === 'db') {
      this.dbStatus = status;
      this.dbStatusDetail = detail;
      this.setLoadingStepStatus(this.view.loadingDbStatus, stepStatus, stepState);
    } else if (target === 'signalr') {
      this.signalrStatus = status;
      this.signalrStatusDetail = detail;
      this.setLoadingStepStatus(this.view.loadingSignalrStatus, stepStatus, stepState);
    } else {
      this.ftpStatus = status;
      this.ftpStatusDetail = detail;
      this.setLoadingStepStatus(this.view.loadingFtpStatus, stepStatus, stepState);
    }
  }

  private setAuthStatus(status: string, detail: string): void {
    this.authStatus = status;
    this.authStatusDetail = detail;
    this.setLoadingStepStatus(
      this.view.loadingAuthStatus,
      this.formatLoadingAuthStatus(status),
      this.loadingStateForAuthStatus(status),
    );
    this.render();
    this.writeRuntimeHealth(`auth-${status}`);
  }

  private markCheckingConnectionsFailed(detail: string): void {
    if (this.dbStatus === 'checking') {
      this.setConnectionStatus('db', 'failed', detail);
    }
    if (this.signalrStatus === 'checking') {
      this.setConnectionStatus('signalr', 'failed', detail);
    }
    if (this.ftpStatus === 'checking') {
      this.setConnectionStatus('ftp', 'failed', detail);
    }
  }

  private formatConnectionStatus(status: ConnectionStatus): string {
    if (status === 'connected') {
      return '연결됨';
    }
    if (status === 'checking') {
      return '확인중';
    }
    if (status === 'failed') {
      return '실패';
    }
    return '미설정';
  }

  private formatLoadingConnectionStatus(status: ConnectionStatus): string {
    if (status === 'connected') {
      return '완료';
    }
    if (status === 'checking') {
      return '확인중';
    }
    if (status === 'failed') {
      return '오류';
    }
    return '대기';
  }

  private loadingStateForConnectionStatus(status: ConnectionStatus): LoadingStepState {
    if (status === 'connected') {
      return 'complete';
    }
    if (status === 'checking') {
      return 'active';
    }
    if (status === 'failed') {
      return 'error';
    }
    return 'pending';
  }

  private formatLoadingAuthStatus(status: string): string {
    const normalized = status.trim().toLowerCase();
    if (normalized === 'authenticated') {
      return '완료';
    }
    if (normalized === 'checking') {
      return '확인중';
    }
    if (normalized === 'not-configured') {
      return '대기';
    }
    if (normalized === 'failed' || normalized.includes('invalid') || normalized.includes('blocked')) {
      return '오류';
    }
    return '대기';
  }

  private loadingStateForAuthStatus(status: string): LoadingStepState {
    const normalized = status.trim().toLowerCase();
    if (normalized === 'authenticated') {
      return 'complete';
    }
    if (normalized === 'checking') {
      return 'active';
    }
    if (normalized === 'not-configured') {
      return 'pending';
    }
    if (normalized === 'failed' || normalized.includes('invalid') || normalized.includes('blocked')) {
      return 'error';
    }
    return 'pending';
  }

  private setLoadingStepStatus(element: HTMLElement, text: string, state: LoadingStepState): void {
    element.textContent = text;
    const step = element.closest('.loading-step');
    if (!step) {
      return;
    }

    step.classList.remove(
      'loading-step--pending',
      'loading-step--active',
      'loading-step--complete',
      'loading-step--error',
      'loading-step--skipped',
    );
    step.classList.add(`loading-step--${state}`);
  }

  private formatCommunicationStatus(): string {
    return [
      `DB: ${this.formatConnectionStatus(this.dbStatus)}`,
      `SignalR: ${this.formatConnectionStatus(this.signalrStatus)}`,
      `FTP: ${this.formatConnectionStatus(this.ftpStatus)}`,
      `Heartbeat: ${this.formatOverlayStatusText(this.heartbeatStatus)}`,
    ].join(' / ');
  }

  private formatUpdateOverlayStatus(): string {
    const state = this.updateOverlayState;
    if (state.phase === 'idle') {
      return '대기';
    }

    const phaseText: Record<UpdateOverlayPhase, string> = {
      idle: '대기',
      downloading: '다운로드 중',
      applying: '적용 중',
      complete: '완료',
      failed: '실패',
    };
    const countText = state.total > 0
      ? `파일 ${state.completed}/${state.total}`
      : '파일 0/0';
    const fileText = state.currentFile !== '-' ? ` · ${state.currentFile}` : '';
    const commandText = state.commandId !== '-' ? ` · ${this.sanitizeOverlaySensitiveText(state.commandId)}` : '';
    const detailText = state.detail ? ` · ${this.sanitizeOverlaySensitiveText(state.detail)}` : '';

    return `${phaseText[state.phase]} ${state.progress}% / ${state.playlistName} / ${countText}${fileText}${commandText}${detailText}`;
  }

  private formatOverlayStatusText(value: string): string {
    return this.sanitizeOverlaySensitiveText(value).trim() || '-';
  }

  private sanitizeOverlaySensitiveText(value: string): string {
    return value
      .replace(/\b(?:https?|ftp|ws|wss):\/\/[^\s)]+/gi, '[endpoint]')
      .replace(/\b(?:\d{1,3}\.){3}\d{1,3}(?::\d+)?(?:\/[^\s)]*)?/g, '[server]')
      .replace(/([?&](?:token|password|pass|key|id|playerGuid|playerName)=)[^&\s)]+/gi, '$1***')
      .replace(/\b(?:password|passwd|pwd|pass|token|secret|key)=\S+/gi, (match) => match.replace(/=.*/, '=***'));
  }

  private setMessage(message: string): void {
    this.view.message.textContent = message;
    this.writeRuntimeHealth('message');
  }

  private writeRuntimeHealth(stage: string): void {
    const page = this.pagePlans[this.pageIndex];
    const platform = formatRuntimeDiagnostics(collectRuntimeDiagnostics());
    const slotSnapshots = this.slotPlayers.map((slotPlayer) => slotPlayer.snapshot());
    const snapshot: RuntimeHealthSnapshot = {
      schemaVersion: 1,
      updatedAt: new Date().toISOString(),
      stage,
      state: this.broadcastOnAir ? this.playing ? 'playing' : 'paused' : 'off-air',
      playlist: page?.playlistName ?? '-',
      page: page ? `${this.pageIndex + 1}/${this.pagePlans.length} ${page.pageName}` : '-',
      elapsed: page ? `${(this.currentPageElapsedMilliseconds() / 1000).toFixed(1)}s / ${page.durationSeconds}s` : '-',
      lastKey: this.lastRemoteKey,
      lastAction: this.lastRemoteAction,
      platform,
      slots: slotSnapshots,
      message: this.view.message.textContent ?? '',
      recentLogs: this.logger.snapshot(RUNTIME_HEALTH_RECENT_LOG_LIMIT).map((entry) => ({ ...entry })),
      diagnostics: {
        pageStartCount: this.pageStartCount,
        contentShowCount: this.contentShowCount,
        lastContent: this.lastContent,
        masterTickDelayMs: Math.round(this.lastMasterTickDelayMs),
        masterTickIntervalMs: Math.round(this.lastMasterTickIntervalMs),
        renderIntervalMs: Math.round(this.lastRenderIntervalMs),
        communicationStatus: this.communicationStatus,
        dbStatus: this.dbStatus,
        dbStatusDetail: this.dbStatusDetail,
        signalrStatus: this.signalrStatus,
        signalrStatusDetail: this.signalrStatusDetail,
        ftpStatus: this.ftpStatus,
        ftpStatusDetail: this.ftpStatusDetail,
        heartbeatStatus: this.heartbeatStatus,
        heartbeatStatusDetail: this.heartbeatStatusDetail,
        authStatus: this.authStatus,
        authStatusDetail: this.authStatusDetail,
        updateStatus: this.formatUpdateOverlayStatus(),
        updatePhase: this.updateOverlayState.phase,
        updateProgress: this.updateOverlayState.progress,
        updateCompleted: this.updateOverlayState.completed,
        updateTotal: this.updateOverlayState.total,
        updateCurrentFile: this.updateOverlayState.currentFile,
        updateCommandId: this.updateOverlayState.commandId,
        playerGuid: this.communication?.playerGuid ?? '',
        playerName: this.communication?.playerName ?? this.config.settings.playerId,
        dataServerAddress: this.communication?.dataServerAddress ?? this.config.settings.dataServerAddress,
        messageServerAddress: this.communication?.messageServerAddress ?? this.config.settings.messageServerAddress,
        signalrUrl: this.communication?.signalrUrl ?? '',
        ftpEndpoint: this.communication
          ? `${this.communication.ftpHost}:${this.communication.ftpPort}${this.communication.ftpRootPath}`
          : '',
      },
      settings: {
        preserveAspectRatio: this.config.manifest.preserveAspectRatio,
        switchOnContentEnd: this.config.settings.switchOnContentEnd,
        hudInitiallyVisible: this.config.hudInitiallyVisible,
      },
    };

    void this.healthReporter.write(snapshot).catch((error) => {
      this.logger.warn('runtime', `헬스 스냅샷 기록 실패: ${formatError(error)}`);
    });
  }

  private scheduleRuntimeHealthLogFlush(delayMs: number): void {
    if (this.runtimeHealthLogFlushTimerId !== null) {
      window.clearTimeout(this.runtimeHealthLogFlushTimerId);
      this.runtimeHealthLogFlushTimerId = null;
    }

    this.runtimeHealthLogFlushTimerId = window.setTimeout(() => {
      this.runtimeHealthLogFlushTimerId = null;
      this.writeRuntimeHealth('avplay-log');
    }, delayMs);
  }

  private toggleHud(): void {
    this.setHudVisible(this.view.hud.classList.contains('debug-hud--hidden'));
  }

  private setHudVisible(visible: boolean): void {
    this.view.hud.classList.toggle('debug-hud--hidden', !visible);
  }

  private togglePlayback(): void {
    if (!this.broadcastOnAir) {
      this.setMessage('방송시간 외 대기 중입니다.');
      this.render();
      return;
    }

    if (!this.playing) {
      if (this.emptyIntroVideoElement) {
        this.playing = true;
        this.startMasterTimer();
        this.pageStartedAt = performance.now() - this.pagePausedElapsedMs;
        void this.emptyIntroVideoElement.play().catch((error) => {
          const message = `Tizen 인트로 영상 재개 실패: ${formatError(error)}`;
          this.reportEmptyIntroVideoError(message);
        });
        this.setMessage('재생 재개');
        this.render();
        return;
      }

      if (this.slotPlayers.length === 0) {
        void this.playPage(this.pageIndex);
        return;
      }

      this.playing = true;
      this.slotPlayers.forEach((slotPlayer) => slotPlayer.resume());
      this.startMasterTimer();
      this.pageStartedAt = performance.now() - this.pagePausedElapsedMs;
      this.setMessage('재생 재개');
      this.render();
      return;
    }

    this.pagePausedElapsedMs = this.currentPageRawElapsedMilliseconds();
    this.playing = false;
    this.clearTimers();
    if (this.emptyIntroVideoElement) {
      this.emptyIntroVideoElement.pause();
    } else {
      this.slotPlayers.forEach((slotPlayer) => slotPlayer.pause());
    }
    this.setMessage('일시 정지');
    this.render();
  }

  private stopPlayback(message = '재생 정지'): void {
    this.playing = false;
    this.pagePausedElapsedMs = 0;
    this.clearTimers();
    this.stopSlots();
    this.avplayPool?.stopAll();
    this.removeStageSlots();
    this.removeEmptyIntroVideo();
    this.audioPolicy.restore();
    this.setMessage(message);
    this.render();
  }

  private getHeartbeatStatus(): string {
    if (!this.broadcastOnAir) {
      return 'off-air';
    }

    if (this.playing && this.hasActivePlaybackSurface()) {
      return 'playing';
    }

    return 'idle';
  }

  private getHeartbeatProcess(): number {
    if (this.updateOverlayState.phase === 'downloading' || this.updateOverlayState.phase === 'applying') {
      return this.updateOverlayState.progress;
    }

    return 0;
  }

  private getCurrentPageName(): string {
    return this.pagePlans[this.pageIndex]?.pageName ?? '';
  }

  private getHeartbeatHdmiState(): boolean {
    if (!this.broadcastOnAir) {
      return false;
    }

    return this.getHeartbeatStatus() !== 'idle';
  }

  private getHeartbeatVersion(): string {
    const version = window.tizen?.application?.getCurrentApplication?.().appInfo?.version;
    return version?.trim() || '0.0.0';
  }

  private handleResize = (): void => {
    this.slotPlayers.forEach((slotPlayer) => slotPlayer.applyDisplayRect());
  };

  private handlePageHide = (): void => {
    this.destroy();
  };

  private handleBeforeUnload = (): void => {
    this.requestOfflineHeartbeat();
  };

  private handleVisibilityChange = (): void => {
    if (document.visibilityState === 'hidden') {
      this.requestOfflineHeartbeat();
      return;
    }

    if (document.visibilityState === 'visible' && !this.destroyed && this.offlineHeartbeatRequested) {
      this.offlineHeartbeatRequested = false;
      this.heartbeatReporter = null;
      this.startHeartbeat();
    }
  };

  private requestOfflineHeartbeat(): void {
    if (this.offlineHeartbeatRequested) {
      return;
    }

    this.offlineHeartbeatRequested = true;
    const reporter = this.heartbeatReporter;
    if (!reporter) {
      return;
    }

    void reporter.stop().catch((error) => {
      this.logger.warn('heartbeat', `offline heartbeat 전송 실패: ${formatError(error)}`);
    });
  }

  private recordRemoteInput(event: KeyboardEvent, action: RemoteControlAction | 'settings-overlay'): void {
    this.lastRemoteKey = event.key || event.code || `keyCode:${event.keyCode}`;
    this.lastRemoteAction = action;
    this.logger.info('input', `${this.lastRemoteKey} -> ${this.lastRemoteAction}`);
    this.render();
    this.writeRuntimeHealth('remote-input');
  }

  private handleKeyDown = (event: KeyboardEvent): void => {
    if (this.authOverlay?.handleKeyDown(event)) {
      this.recordRemoteInput(event, 'settings-overlay');
      return;
    }

    if (this.settingsOverlay?.handleKeyDown(event)) {
      this.recordRemoteInput(event, 'settings-overlay');
      return;
    }

    const action = resolveRemoteControlAction(event);
    this.recordRemoteInput(event, action);
    if (action === 'toggle-playback') {
      this.togglePlayback();
    } else if (action === 'stop-playback') {
      this.stopPlayback();
    } else if (action === 'toggle-hud') {
      this.toggleHud();
    } else if (action === 'open-settings') {
      this.settingsOverlay?.open();
      this.setMessage('설정창을 열었습니다.');
    } else if (action === 'next-page') {
      void this.playPage(this.pageIndex + 1, { preservePreviousUntilReady: true });
    } else if (action === 'previous-page') {
      void this.playPage(this.pageIndex - 1, { preservePreviousUntilReady: true });
    }
  };
}
