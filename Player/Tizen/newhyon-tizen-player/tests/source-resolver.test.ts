import { afterEach, describe, expect, it, vi } from 'vitest';
import { resolveAvplaySourceUrl } from '../src/player/source-resolver';

describe('source-resolver', () => {
  afterEach(() => {
    window.tizen = undefined;
  });

  it('Tizen 패키지 실행 URL에서도 상대 미디어 경로를 wgt-package 기준으로 해석한다', () => {
    const pathExists = vi.fn((path: string) => path === 'wgt-package/media/intro.mp4');
    const toURI = vi.fn((path: string) => `file:///opt/usr/apps/NewHyOnT01.Player/${path}`);
    window.tizen = {
      filesystem: {
        pathExists,
        toURI,
      },
    };

    const resolved = resolveAvplaySourceUrl(
      'media/intro.mp4',
      'file:///opt/usr/apps/NewHyOnT01.Player/index.html',
    );

    expect(pathExists).toHaveBeenCalledWith('wgt-package/media/intro.mp4');
    expect(toURI).toHaveBeenCalledWith('wgt-package/media/intro.mp4');
    expect(resolved).toBe('/opt/usr/apps/NewHyOnT01.Player/wgt-package/media/intro.mp4');
  });

  it('명시적인 Tizen 가상 경로는 그대로 사용한다', () => {
    const pathExists = vi.fn((path: string) => path === 'wgt-package/media/intro.mp4');
    const toURI = vi.fn((path: string) => `file:///opt/usr/apps/NewHyOnT01.Player/${path}`);
    window.tizen = {
      filesystem: {
        pathExists,
        toURI,
      },
    };

    resolveAvplaySourceUrl('wgt-package/media/intro.mp4');

    expect(pathExists).toHaveBeenCalledWith('wgt-package/media/intro.mp4');
    expect(toURI).toHaveBeenCalledWith('wgt-package/media/intro.mp4');
  });
});
