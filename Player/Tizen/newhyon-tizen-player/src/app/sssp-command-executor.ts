export interface DeferredSsspCommand {
  readonly handled: true;
  readonly afterAck: () => void;
}

export interface SsspSignageControlSnapshot {
  readonly panelMute: 'ON' | 'OFF';
  readonly messageDisplay: 'ON' | 'OFF';
  readonly remoteConfiguration: 'ON' | 'OFF';
}

function requireSystemControl(): SystemControlManager {
  const systemcontrol = window.webapis?.systemcontrol;
  if (!systemcontrol) {
    throw new Error('SSSP SystemControl API를 사용할 수 없습니다.');
  }

  return systemcontrol;
}

function requireRemotePower(): RemotePowerManager {
  const remotepower = window.webapis?.remotepower;
  if (!remotepower) {
    throw new Error('SSSP RemotePower API를 사용할 수 없습니다.');
  }

  return remotepower;
}

function assertSystemControlFunction<K extends keyof SystemControlManager>(
  systemcontrol: SystemControlManager,
  key: K,
): NonNullable<SystemControlManager[K]> {
  const fn = systemcontrol[key];
  if (typeof fn !== 'function') {
    throw new Error(`SSSP SystemControl ${String(key)} API를 사용할 수 없습니다.`);
  }

  return fn as NonNullable<SystemControlManager[K]>;
}

function assertRemotePowerFunction<K extends keyof RemotePowerManager>(
  remotepower: RemotePowerManager,
  key: K,
): NonNullable<RemotePowerManager[K]> {
  const fn = remotepower[key];
  if (typeof fn !== 'function') {
    throw new Error(`SSSP RemotePower ${String(key)} API를 사용할 수 없습니다.`);
  }

  return fn as NonNullable<RemotePowerManager[K]>;
}

function ensureSsspRemoteConfigurationEnabled(remotepower: RemotePowerManager): 'ON' {
  const setRemoteConfiguration = assertRemotePowerFunction(remotepower, 'setRemoteConfiguration');
  const getRemoteConfiguration = assertRemotePowerFunction(remotepower, 'getRemoteConfiguration');

  setRemoteConfiguration.call(remotepower, 'ON');
  const remoteConfiguration = getRemoteConfiguration.call(remotepower);
  if (remoteConfiguration !== 'ON') {
    throw new Error(`SSSP RemotePower remote configuration 검증 실패: expected=ON, actual=${String(remoteConfiguration)}`);
  }

  return remoteConfiguration;
}

export function prepareSsspReboot(): DeferredSsspCommand {
  const systemcontrol = requireSystemControl();
  const rebootDevice = assertSystemControlFunction(systemcontrol, 'rebootDevice');

  return {
    handled: true,
    afterAck: () => {
      rebootDevice.call(systemcontrol);
    },
  };
}

export function prepareSsspPowerOff(): DeferredSsspCommand {
  const remotepower = requireRemotePower();
  ensureSsspRemoteConfigurationEnabled(remotepower);
  const powerOff = assertRemotePowerFunction(remotepower, 'powerOff');

  return {
    handled: true,
    afterAck: () => {
      powerOff.call(remotepower);
    },
  };
}

export function setSsspPanelMute(muted: boolean): void {
  const systemcontrol = requireSystemControl();
  const setPanelMute = assertSystemControlFunction(systemcontrol, 'setPanelMute');
  const getPanelMute = assertSystemControlFunction(systemcontrol, 'getPanelMute');
  const expected = muted ? 'ON' : 'OFF';

  setPanelMute.call(systemcontrol, expected);

  const actual = getPanelMute.call(systemcontrol);
  if (actual !== expected) {
    throw new Error(`SSSP SystemControl panel mute 검증 실패: expected=${expected}, actual=${String(actual)}`);
  }
}

export function configureSsspSignageForPlayer(): SsspSignageControlSnapshot {
  const systemcontrol = requireSystemControl();
  const remotepower = requireRemotePower();
  const setMessageDisplay = assertSystemControlFunction(systemcontrol, 'setMessageDisplay');
  const getMessageDisplay = assertSystemControlFunction(systemcontrol, 'getMessageDisplay');
  const getPanelMute = assertSystemControlFunction(systemcontrol, 'getPanelMute');

  setMessageDisplay.call(systemcontrol, 'OFF');
  const messageDisplay = getMessageDisplay.call(systemcontrol);
  if (messageDisplay !== 'OFF') {
    throw new Error(`SSSP SystemControl message display 검증 실패: expected=OFF, actual=${String(messageDisplay)}`);
  }

  const remoteConfiguration = ensureSsspRemoteConfigurationEnabled(remotepower);

  return {
    panelMute: getPanelMute.call(systemcontrol),
    messageDisplay,
    remoteConfiguration,
  };
}
