export interface SsspDisplayMetrics {
  readonly source: 'sssp' | 'browser';
  readonly outputWidth: number;
  readonly outputHeight: number;
  readonly panelWidth: number | null;
  readonly panelHeight: number | null;
  readonly orientation: 'LANDSCAPE_PRIMARY' | 'LANDSCAPE_SECONDARY' | 'PORTRAIT_PRIMARY' | 'PORTRAIT_SECONDARY' | null;
  readonly viewportWidth: number;
  readonly viewportHeight: number;
}

interface SystemInfoDisplay {
  readonly resolutionWidth: number;
  readonly resolutionHeight: number;
}

interface SystemInfoPanel {
  readonly panelWidth: number;
  readonly panelHeight: number;
}

interface SystemInfoDeviceOrientation {
  readonly status: string;
}

function readViewportSize(): { readonly width: number; readonly height: number } {
  return {
    width: Math.max(window.visualViewport?.width ?? 0, document.documentElement.clientWidth, window.innerWidth, 1),
    height: Math.max(window.visualViewport?.height ?? 0, document.documentElement.clientHeight, window.innerHeight, 1),
  };
}

function readPositiveInteger(value: unknown, label: string): number {
  const numberValue = Number(value);
  if (!Number.isInteger(numberValue) || numberValue <= 0) {
    throw new Error(`SSSP ${label} 해상도가 올바르지 않습니다: ${String(value)}`);
  }

  return numberValue;
}

function getSystemInfoProperty<T>(property: 'DISPLAY' | 'PANEL' | 'DEVICE_ORIENTATION'): Promise<T> {
  const systemInfo = window.tizen?.systeminfo;
  if (!systemInfo?.getPropertyValue) {
    throw new Error('SSSP SystemInfo API를 찾지 못했습니다.');
  }

  return new Promise<T>((resolve, reject) => {
    systemInfo.getPropertyValue!(property, (value) => resolve(value as T), reject);
  });
}

export function readBrowserDisplayMetrics(): SsspDisplayMetrics {
  const viewport = readViewportSize();
  return {
    source: 'browser',
    outputWidth: viewport.width,
    outputHeight: viewport.height,
    panelWidth: null,
    panelHeight: null,
    orientation: null,
    viewportWidth: viewport.width,
    viewportHeight: viewport.height,
  };
}

export async function resolveSsspDisplayMetrics(): Promise<SsspDisplayMetrics> {
  if (!window.tizen?.systeminfo?.getPropertyValue) {
    return readBrowserDisplayMetrics();
  }

  const [display, panel, deviceOrientation] = await Promise.all([
    getSystemInfoProperty<SystemInfoDisplay>('DISPLAY'),
    getSystemInfoProperty<SystemInfoPanel>('PANEL'),
    getSystemInfoProperty<SystemInfoDeviceOrientation>('DEVICE_ORIENTATION'),
  ]);
  const viewport = readViewportSize();
  const orientation = readOrientation(deviceOrientation.status);
  const resolutionWidth = readPositiveInteger(display.resolutionWidth, 'DISPLAY width');
  const resolutionHeight = readPositiveInteger(display.resolutionHeight, 'DISPLAY height');
  const isPortrait = orientation === 'PORTRAIT_PRIMARY' || orientation === 'PORTRAIT_SECONDARY';

  return {
    source: 'sssp',
    outputWidth: isPortrait ? resolutionHeight : resolutionWidth,
    outputHeight: isPortrait ? resolutionWidth : resolutionHeight,
    panelWidth: readPositiveInteger(panel.panelWidth, 'PANEL width'),
    panelHeight: readPositiveInteger(panel.panelHeight, 'PANEL height'),
    orientation,
    viewportWidth: viewport.width,
    viewportHeight: viewport.height,
  };
}

function readOrientation(value: unknown): SsspDisplayMetrics['orientation'] {
  switch (value) {
    case 'LANDSCAPE_PRIMARY':
    case 'LANDSCAPE_SECONDARY':
    case 'PORTRAIT_PRIMARY':
    case 'PORTRAIT_SECONDARY':
      return value;
    default:
      throw new Error(`SSSP DEVICE_ORIENTATION 값이 올바르지 않습니다: ${String(value)}`);
  }
}

export function formatSsspDisplayMetrics(metrics: SsspDisplayMetrics): string {
  const panel = metrics.panelWidth !== null && metrics.panelHeight !== null
    ? `${metrics.panelWidth}x${metrics.panelHeight}`
    : '-';
  return `source=${metrics.source} output=${metrics.outputWidth}x${metrics.outputHeight} panel=${panel} orientation=${metrics.orientation ?? '-'} viewport=${metrics.viewportWidth}x${metrics.viewportHeight}`;
}
