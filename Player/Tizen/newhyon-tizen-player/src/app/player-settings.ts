export interface PlayerSettings {
  readonly playerId: string;
  readonly managerAddress: string;
  readonly remoteStreamingGatewayUrl: string;
  readonly dataServerAddress: string;
  readonly messageServerAddress: string;
  readonly signalrPort: number;
  readonly signalrHubPath: string;
  readonly ftpPort: number;
  readonly ftpPasvMinPort: number;
  readonly ftpPasvMaxPort: number;
  readonly ftpRootPath: string;
  readonly manifestUrl: string;
  readonly preserveAspectRatio: boolean;
  readonly switchOnContentEnd: boolean;
  readonly defaultVolume: number;
  readonly hudInitiallyVisible: boolean;
}

const STORAGE_KEY = 'newhyon-tizen-player.settings.v1';
export const DEFAULT_REMOTE_STREAMING_GATEWAY_URL = 'https://newhyon-remote.turtlelab.app';

export const DEFAULT_PLAYER_SETTINGS: PlayerSettings = {
  playerId: 'tizen',
  managerAddress: 'turtlesrv.ddns.net',
  remoteStreamingGatewayUrl: DEFAULT_REMOTE_STREAMING_GATEWAY_URL,
  dataServerAddress: '',
  messageServerAddress: '',
  signalrPort: 5000,
  signalrHubPath: '/Data',
  ftpPort: 10021,
  ftpPasvMinPort: 0,
  ftpPasvMaxPort: 0,
  ftpRootPath: '/NewHyOn',
  manifestUrl: '',
  preserveAspectRatio: false,
  switchOnContentEnd: false,
  defaultVolume: 100,
  hudInitiallyVisible: false,
};

function sanitizeString(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}

function sanitizeBoolean(value: unknown, defaultValue: boolean): boolean {
  return typeof value === 'boolean' ? value : defaultValue;
}

function sanitizeInteger(value: unknown, defaultValue: number): number {
  const numeric = typeof value === 'number' ? value : Number.parseInt(String(value ?? ''), 10);
  return Number.isFinite(numeric) && numeric >= 0 ? Math.round(numeric) : defaultValue;
}

export function normalizeVolume(value: unknown, defaultValue = DEFAULT_PLAYER_SETTINGS.defaultVolume): number {
  const numeric = typeof value === 'number' ? value : Number.parseInt(String(value ?? ''), 10);
  if (!Number.isFinite(numeric)) {
    return defaultValue;
  }

  return Math.min(100, Math.max(0, Math.round(numeric)));
}

function sanitizeHubPath(value: unknown): string {
  const normalized = sanitizeString(value);
  if (!normalized) {
    return DEFAULT_PLAYER_SETTINGS.signalrHubPath;
  }

  return normalized.startsWith('/') ? normalized : `/${normalized}`;
}

export function loadPlayerSettings(storage: Storage = window.localStorage): PlayerSettings {
  const raw = storage.getItem(STORAGE_KEY);
  if (!raw) {
    return DEFAULT_PLAYER_SETTINGS;
  }

  let parsed: Partial<PlayerSettings>;
  try {
    parsed = JSON.parse(raw) as Partial<PlayerSettings>;
  } catch {
    storage.removeItem(STORAGE_KEY);
    return DEFAULT_PLAYER_SETTINGS;
  }

  return {
    playerId: sanitizeString(parsed.playerId),
    managerAddress: sanitizeString(parsed.managerAddress),
    remoteStreamingGatewayUrl: sanitizeString(parsed.remoteStreamingGatewayUrl),
    dataServerAddress: sanitizeString(parsed.dataServerAddress),
    messageServerAddress: sanitizeString(parsed.messageServerAddress),
    signalrPort: sanitizeInteger(parsed.signalrPort, DEFAULT_PLAYER_SETTINGS.signalrPort),
    signalrHubPath: sanitizeHubPath(parsed.signalrHubPath),
    ftpPort: sanitizeInteger(parsed.ftpPort, DEFAULT_PLAYER_SETTINGS.ftpPort),
    ftpPasvMinPort: sanitizeInteger(parsed.ftpPasvMinPort, DEFAULT_PLAYER_SETTINGS.ftpPasvMinPort),
    ftpPasvMaxPort: sanitizeInteger(parsed.ftpPasvMaxPort, DEFAULT_PLAYER_SETTINGS.ftpPasvMaxPort),
    ftpRootPath: sanitizeString(parsed.ftpRootPath) || DEFAULT_PLAYER_SETTINGS.ftpRootPath,
    manifestUrl: sanitizeString(parsed.manifestUrl),
    preserveAspectRatio: sanitizeBoolean(parsed.preserveAspectRatio, DEFAULT_PLAYER_SETTINGS.preserveAspectRatio),
    switchOnContentEnd: sanitizeBoolean(parsed.switchOnContentEnd, DEFAULT_PLAYER_SETTINGS.switchOnContentEnd),
    defaultVolume: normalizeVolume(parsed.defaultVolume),
    hudInitiallyVisible: sanitizeBoolean(parsed.hudInitiallyVisible, DEFAULT_PLAYER_SETTINGS.hudInitiallyVisible),
  };
}

export function savePlayerSettings(settings: PlayerSettings, storage: Storage = window.localStorage): void {
  storage.setItem(STORAGE_KEY, JSON.stringify(settings));
}

export function clearPlayerSettings(storage: Storage = window.localStorage): void {
  storage.removeItem(STORAGE_KEY);
}
