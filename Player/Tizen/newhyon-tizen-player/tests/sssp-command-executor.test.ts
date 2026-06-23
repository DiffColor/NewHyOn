import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  configureSsspSignageForPlayer,
  prepareSsspPowerOff,
  prepareSsspReboot,
  setSsspPanelMute,
} from '../src/app/sssp-command-executor';

describe('SSSP command executor', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.webapis = {};
  });

  it('reboot 명령은 SystemControl rebootDevice를 ack 이후 실행 대상으로 준비한다', () => {
    const rebootDevice = vi.fn();
    window.webapis = {
      systemcontrol: {
        rebootDevice,
      },
    };

    const command = prepareSsspReboot();

    expect(command.handled).toBe(true);
    expect(rebootDevice).not.toHaveBeenCalled();
    command.afterAck();
    expect(rebootDevice).toHaveBeenCalledTimes(1);
  });

  it('poweroff 명령은 RemotePower powerOff를 ack 이후 실행 대상으로 준비한다', () => {
    const powerOff = vi.fn();
    let remoteConfiguration: 'ON' | 'OFF' = 'OFF';
    const setRemoteConfiguration = vi.fn((state: 'ON' | 'OFF') => {
      remoteConfiguration = state;
    });
    window.webapis = {
      remotepower: {
        setRemoteConfiguration,
        getRemoteConfiguration: () => remoteConfiguration,
        powerOff,
      },
    };

    const command = prepareSsspPowerOff();

    expect(command.handled).toBe(true);
    expect(setRemoteConfiguration).toHaveBeenCalledWith('ON');
    expect(powerOff).not.toHaveBeenCalled();
    command.afterAck();
    expect(powerOff).toHaveBeenCalledTimes(1);
  });

  it('방송시간 OFF/ON은 SystemControl setPanelMute로 패널을 끄고 켠다', () => {
    let panelMute: 'ON' | 'OFF' = 'OFF';
    const setPanelMute = vi.fn();
    window.webapis = {
      systemcontrol: {
        setPanelMute: (state) => {
          panelMute = state;
          setPanelMute(state);
        },
        getPanelMute: () => panelMute,
      },
    };

    setSsspPanelMute(true);
    setSsspPanelMute(false);

    expect(setPanelMute).toHaveBeenNthCalledWith(1, 'ON');
    expect(setPanelMute).toHaveBeenNthCalledWith(2, 'OFF');
  });

  it('Signage 제어 정책은 OSD 메시지를 끄고 원격전원 설정을 켠 뒤 조회값으로 검증한다', () => {
    let messageDisplay: 'ON' | 'OFF' = 'ON';
    let remoteConfiguration: 'ON' | 'OFF' = 'OFF';
    const setMessageDisplay = vi.fn((state: 'ON' | 'OFF') => {
      messageDisplay = state;
    });
    const setRemoteConfiguration = vi.fn((state: 'ON' | 'OFF') => {
      remoteConfiguration = state;
    });
    window.webapis = {
      systemcontrol: {
        getPanelMute: () => 'OFF',
        setMessageDisplay,
        getMessageDisplay: () => messageDisplay,
      },
      remotepower: {
        setRemoteConfiguration,
        getRemoteConfiguration: () => remoteConfiguration,
      },
    };

    expect(configureSsspSignageForPlayer()).toEqual({
      panelMute: 'OFF',
      messageDisplay: 'OFF',
      remoteConfiguration: 'ON',
    });
    expect(setMessageDisplay).toHaveBeenCalledWith('OFF');
    expect(setRemoteConfiguration).toHaveBeenCalledWith('ON');
  });

  it('SSSP 전용 API가 없으면 명령 실패로 처리할 수 있게 예외를 낸다', () => {
    window.webapis = {
      systemcontrol: {},
      remotepower: {},
    };

    expect(() => prepareSsspReboot()).toThrow('rebootDevice');
    expect(() => prepareSsspPowerOff()).toThrow('setRemoteConfiguration');
    expect(() => setSsspPanelMute(true)).toThrow('setPanelMute');
  });
});
