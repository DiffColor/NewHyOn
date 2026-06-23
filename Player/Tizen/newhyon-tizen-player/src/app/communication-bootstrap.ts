import { DEFAULT_PLAYER_SETTINGS, savePlayerSettings, type PlayerSettings } from './player-settings';
import Horizon from '@horizon/client';
import { resolvePlayerIdentityOnce } from './player-guid-sync';

const REFERENCE_FTP_USER_NAME = 'asdf';
const REFERENCE_FTP_PASSWORD = 'Emfndhk!';

export type ConnectionStatus = 'not-configured' | 'checking' | 'connected' | 'failed';

export interface ServerSettingsSnapshot {
  readonly DataServerIp?: string;
  readonly dataServerIp?: string;
  readonly MessageServerIp?: string;
  readonly messageServerIp?: string;
  readonly FTP_Port?: number;
  readonly ftP_Port?: number;
  readonly ftpPort?: number;
  readonly FTP_PasvMinPort?: number;
  readonly ftP_PasvMinPort?: number;
  readonly ftpPasvMinPort?: number;
  readonly FTP_PasvMaxPort?: number;
  readonly ftP_PasvMaxPort?: number;
  readonly ftpPasvMaxPort?: number;
  readonly FTP_RootPath?: string;
  readonly ftP_RootPath?: string;
  readonly ftpRootPath?: string;
  readonly FTP_UserName?: string;
  readonly ftP_UserName?: string;
  readonly ftpUserName?: string;
  readonly FTP_Username?: string;
  readonly ftpUsername?: string;
  readonly FTP_Password?: string;
  readonly ftP_Password?: string;
  readonly ftpPassword?: string;
}

export interface CommunicationBootstrapResult {
  readonly horizonHost: string;
  readonly playerGuid: string;
  readonly playerName: string;
  readonly dataServerAddress: string;
  readonly messageServerAddress: string;
  readonly dbHost: string;
  readonly dbStatus: ConnectionStatus;
  readonly signalrUrl: string;
  readonly signalrStatus: ConnectionStatus;
  readonly ftpHost: string;
  readonly ftpPort: number;
  readonly ftpPasvMinPort: number;
  readonly ftpPasvMaxPort: number;
  readonly ftpRootPath: string;
  readonly ftpUserName: string;
  readonly ftpPassword: string;
  readonly ftpStatus: ConnectionStatus;
  readonly ftpStatusDetail: string;
  readonly signalrNegotiated: boolean;
}

export interface CommunicationStatusUpdate {
  readonly target: 'db' | 'signalr' | 'ftp';
  readonly status: ConnectionStatus;
  readonly detail: string;
}

export interface CommunicationBootstrapOptions {
  readonly onStatus?: (update: CommunicationStatusUpdate) => void;
}

interface RethinkEndpoint {
  readonly host: string;
  readonly port: number;
  readonly displayAddress: string;
  readonly horizonHost: string;
  readonly secure: boolean;
}

function normalizeHostOrUrl(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return '';
  }

  return /^https?:\/\//i.test(trimmed) ? trimmed : `http://${trimmed}`;
}

function parseRethinkEndpoint(value: string): RethinkEndpoint {
  const trimmed = value.trim();
  if (!trimmed) {
    throw new Error('데이터서버 주소가 비어 있습니다.');
  }

  const url = new URL(normalizeHostOrUrl(trimmed));
  const host = url.hostname.trim();
  if (!host) {
    throw new Error('데이터서버 주소 형식이 올바르지 않습니다.');
  }

  const explicitPort = url.port ? Number.parseInt(url.port, 10) : 0;
  const secure = url.protocol === 'https:';
  const horizonPort = explicitPort > 0 ? explicitPort : 8181;
  const displayAddress = host;
  return {
    host,
    port: horizonPort,
    displayAddress,
    horizonHost: `${host}:${horizonPort}`,
    secure,
  };
}

function normalizeHost(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return '';
  }

  return /^https?:\/\//i.test(trimmed) ? new URL(trimmed).hostname : trimmed.replace(/\/.*$/, '');
}

function normalizeHubPath(value: string): string {
  const trimmed = value.trim() || DEFAULT_PLAYER_SETTINGS.signalrHubPath;
  return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
}

function normalizePort(value: number | undefined, defaultValue: number): number {
  return Number.isFinite(value) && value !== undefined && value > 0 ? Math.round(value) : defaultValue;
}

function normalizeOptionalString(value: string | undefined): string {
  return typeof value === 'string' ? value.trim() : '';
}

function buildSignalRUrl(messageServerAddress: string, port: number, hubPath: string): string {
  const host = normalizeHost(messageServerAddress);
  if (!host) {
    throw new Error('MessageServerIp가 비어 있어 SignalR URL을 만들 수 없습니다.');
  }

  return `http://${host}:${port}${normalizeHubPath(hubPath)}`;
}

interface HorizonObservable<T> {
  subscribe(observer: {
    next?: (value: T) => void;
    error?: (error: unknown) => void;
    complete?: () => void;
  }): { unsubscribe?: () => void };
}

interface HorizonClient {
  (collectionName: string): {
    find(idOrObject: unknown): { fetch(): HorizonObservable<unknown> };
    limit(size: number): { fetch(): HorizonObservable<unknown> };
  };
  disconnect(): void;
}

function firstFromObservable<T>(observable: HorizonObservable<T>, timeoutMs: number, errorMessage: string): Promise<T | null> {
  return new Promise((resolve, reject) => {
    let settled = false;
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
        if (settled) {
          return;
        }
        settled = true;
        window.clearTimeout(timer);
        subscription?.unsubscribe?.();
        resolve(value ?? null);
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
        resolve(null);
      },
    });
  });
}

async function fetchServerSettings(horizon: HorizonClient): Promise<ServerSettingsSnapshot> {
  const byId = await firstFromObservable<unknown>(
    horizon('ServerSettings').find(0).fetch(),
    10000,
    'Horizon ServerSettings 조회 시간이 초과되었습니다.',
  );
  if (byId && typeof byId === 'object') {
    return byId as ServerSettingsSnapshot;
  }

  const firstRows = await firstFromObservable<unknown>(
    horizon('ServerSettings').limit(1).fetch(),
    10000,
    'Horizon ServerSettings 목록 조회 시간이 초과되었습니다.',
  );
  if (Array.isArray(firstRows) && firstRows[0] && typeof firstRows[0] === 'object') {
    return firstRows[0] as ServerSettingsSnapshot;
  }

  throw new Error('Horizon ServerSettings 레코드를 찾지 못했습니다.');
}

function buildSignalRNegotiateUrl(signalrUrl: string, playerName: string, playerGuid: string): string {
  const url = new URL(`${signalrUrl.replace(/\/$/, '')}/negotiate`);
  url.searchParams.set('playerName', playerName);
  url.searchParams.set('playerGuid', playerGuid);
  url.searchParams.set('negotiateVersion', '1');
  return url.toString();
}

async function negotiateSignalR(signalrUrl: string, playerName: string, playerGuid: string): Promise<boolean> {
  const negotiateUrl = buildSignalRNegotiateUrl(signalrUrl, playerName, playerGuid);
  const response = await fetch(negotiateUrl, { method: 'POST', cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`SignalR negotiate 실패: ${response.status} ${response.statusText}`);
  }

  return true;
}

export async function bootstrapCommunicationSettings(
  settings: PlayerSettings,
  options: CommunicationBootstrapOptions = {},
): Promise<CommunicationBootstrapResult | null> {
  if (!settings.managerAddress.trim()) {
    return null;
  }

  const rethinkEndpoint = parseRethinkEndpoint(settings.managerAddress);
  const horizon = Horizon({
    host: rethinkEndpoint.horizonHost,
    secure: rethinkEndpoint.secure,
    lazyWrites: false,
  }) as HorizonClient;
  options.onStatus?.({ target: 'db', status: 'checking', detail: `Horizon ${rethinkEndpoint.horizonHost}` });
  let serverSettings: ServerSettingsSnapshot;
  try {
    serverSettings = await fetchServerSettings(horizon);
  } finally {
    horizon.disconnect();
  }
  options.onStatus?.({ target: 'db', status: 'connected', detail: `Horizon ${rethinkEndpoint.horizonHost}` });
  const playerIdentity = await resolvePlayerIdentityOnce(settings.managerAddress, settings.playerId);
  const playerGuid = playerIdentity.playerGuid;
  const playerName = playerIdentity.playerName;
  const dataServerAddress = normalizeHost(serverSettings.DataServerIp ?? serverSettings.dataServerIp ?? '');
  if (!dataServerAddress) {
    throw new Error('ServerSettings.DataServerIp가 비어 있습니다.');
  }
  const messageServerAddress = normalizeHost(serverSettings.MessageServerIp ?? serverSettings.messageServerIp ?? '');
  if (!messageServerAddress) {
    throw new Error('ServerSettings.MessageServerIp가 비어 있습니다.');
  }
  const ftpPort = normalizePort(serverSettings.FTP_Port ?? serverSettings.ftP_Port ?? serverSettings.ftpPort, 0);
  if (ftpPort <= 0) {
    throw new Error('ServerSettings.FTP_Port가 비어 있습니다.');
  }
  const ftpPasvMinPort = Math.max(
    0,
    Math.round(serverSettings.FTP_PasvMinPort ?? serverSettings.ftP_PasvMinPort ?? serverSettings.ftpPasvMinPort ?? settings.ftpPasvMinPort),
  );
  const ftpPasvMaxPort = Math.max(
    0,
    Math.round(serverSettings.FTP_PasvMaxPort ?? serverSettings.ftP_PasvMaxPort ?? serverSettings.ftpPasvMaxPort ?? settings.ftpPasvMaxPort),
  );
  const ftpRootPath =
    (serverSettings.FTP_RootPath ?? serverSettings.ftP_RootPath ?? serverSettings.ftpRootPath ?? settings.ftpRootPath).trim()
    || DEFAULT_PLAYER_SETTINGS.ftpRootPath;
  const ftpUserName = normalizeOptionalString(
    serverSettings.FTP_UserName
    ?? serverSettings.ftP_UserName
    ?? serverSettings.ftpUserName
    ?? serverSettings.FTP_Username
    ?? serverSettings.ftpUsername,
  ) || REFERENCE_FTP_USER_NAME;
  const ftpPassword = normalizeOptionalString(
    serverSettings.FTP_Password
    ?? serverSettings.ftP_Password
    ?? serverSettings.ftpPassword,
  ) || REFERENCE_FTP_PASSWORD;
  const signalrUrl = buildSignalRUrl(messageServerAddress, settings.signalrPort, settings.signalrHubPath);
  options.onStatus?.({ target: 'signalr', status: 'checking', detail: signalrUrl });
  const signalrNegotiated = await negotiateSignalR(signalrUrl, playerName, playerGuid);
  options.onStatus?.({ target: 'signalr', status: 'connected', detail: signalrUrl });
  const ftpStatusDetail = `${dataServerAddress}:${ftpPort}${ftpRootPath}`;
  options.onStatus?.({ target: 'ftp', status: 'connected', detail: ftpStatusDetail });

  savePlayerSettings({
    ...settings,
    dataServerAddress,
    messageServerAddress,
    ftpPort,
    ftpPasvMinPort,
    ftpPasvMaxPort,
    ftpRootPath,
  });

  return {
    horizonHost: rethinkEndpoint.horizonHost,
    playerGuid,
    playerName,
    dataServerAddress,
    messageServerAddress,
    dbHost: dataServerAddress,
    dbStatus: 'connected',
    signalrUrl,
    signalrStatus: 'connected',
    ftpHost: dataServerAddress,
    ftpPort,
    ftpPasvMinPort,
    ftpPasvMaxPort,
    ftpRootPath,
    ftpUserName,
    ftpPassword,
    ftpStatus: 'connected',
    ftpStatusDetail,
    signalrNegotiated,
  };
}
