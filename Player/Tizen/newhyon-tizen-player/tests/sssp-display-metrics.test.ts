import { beforeEach, describe, expect, it, vi } from 'vitest';
import { resolveSsspDisplayMetrics } from '../src/app/sssp-display-metrics';

describe('SSSP display metrics', () => {
  beforeEach(() => {
    window.tizen = undefined;
  });

  it('SSSP DISPLAY와 PANEL API에서 실제 패널 해상도를 읽는다', async () => {
    const getPropertyValue = vi.fn((property: string, success: (value: unknown) => void) => {
      success(property === 'DISPLAY'
        ? { resolutionWidth: 3840, resolutionHeight: 2160 }
        : property === 'PANEL'
          ? { panelWidth: 3840, panelHeight: 2160 }
          : { status: 'LANDSCAPE_PRIMARY' });
    });
    window.tizen = {
      systeminfo: { getPropertyValue },
    };

    await expect(resolveSsspDisplayMetrics()).resolves.toMatchObject({
      source: 'sssp',
      outputWidth: 3840,
      outputHeight: 2160,
      panelWidth: 3840,
      panelHeight: 2160,
    });
    expect(getPropertyValue).toHaveBeenCalledWith('DISPLAY', expect.any(Function), expect.any(Function));
    expect(getPropertyValue).toHaveBeenCalledWith('PANEL', expect.any(Function), expect.any(Function));
    expect(getPropertyValue).toHaveBeenCalledWith('DEVICE_ORIENTATION', expect.any(Function), expect.any(Function));
  });

  it('SSSP가 보고한 잘못된 DISPLAY 해상도는 시작 오류로 처리한다', async () => {
    window.tizen = {
      systeminfo: {
        getPropertyValue: (property, success) => {
          success(property === 'DISPLAY'
            ? { resolutionWidth: 0, resolutionHeight: 2160 }
            : property === 'PANEL'
              ? { panelWidth: 3840, panelHeight: 2160 }
              : { status: 'LANDSCAPE_PRIMARY' });
        },
      },
    };

    await expect(resolveSsspDisplayMetrics()).rejects.toThrow('SSSP DISPLAY width 해상도가 올바르지 않습니다: 0');
  });

  it('세로 설치된 패널은 DEVICE_ORIENTATION에 맞춰 실제 출력 축을 전환한다', async () => {
    window.tizen = {
      systeminfo: {
        getPropertyValue: (property, success) => {
          success(property === 'DISPLAY'
            ? { resolutionWidth: 3840, resolutionHeight: 2160 }
            : property === 'PANEL'
              ? { panelWidth: 3840, panelHeight: 2160 }
              : { status: 'PORTRAIT_PRIMARY' });
        },
      },
    };

    await expect(resolveSsspDisplayMetrics()).resolves.toMatchObject({
      outputWidth: 2160,
      outputHeight: 3840,
      orientation: 'PORTRAIT_PRIMARY',
    });
  });
});
