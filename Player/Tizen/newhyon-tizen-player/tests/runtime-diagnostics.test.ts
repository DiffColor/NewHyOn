import { beforeEach, describe, expect, it, vi } from 'vitest';
import { collectRuntimeDiagnostics, formatRuntimeDiagnostics } from '../src/app/runtime-diagnostics';

describe('runtime diagnostics', () => {
  beforeEach(() => {
    window.webapis = undefined;
    window.tizen = undefined;
  });

  it('Tizen 런타임 API 가용 상태를 HUD 표시 문자열로 만든다', () => {
    window.webapis = {
      avplay: {} as AVPlayApi,
      productinfo: {},
      systemcontrol: {},
    };
    window.tizen = {
      ApplicationControl: vi.fn() as unknown as TizenApplicationControlConstructor,
      ApplicationControlData: vi.fn() as unknown as TizenApplicationControlDataConstructor,
      application: {
        launchAppControl: () => undefined,
      },
      tvinputdevice: {
        registerKey: () => undefined,
      },
      filesystem: {
        toURI: (path) => `file:///opt/${path}`,
      },
    };

    expect(formatRuntimeDiagnostics(collectRuntimeDiagnostics())).toBe(
      'webapis=OK avplay=OK avplaystore=MISS network=MISS productinfo=OK systemcontrol=OK remotepower=MISS tizen=OK download=MISS filesystem=OK app=OK appctrl=OK input=OK audio=MISS',
    );
  });
});
