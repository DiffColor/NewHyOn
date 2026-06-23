export interface SsspDeviceNetworkInfo {
  readonly ipAddress: string;
  readonly macAddress: string;
  readonly gatewayConnected: boolean;
  readonly activeConnectionType: string;
}

export function readSsspDeviceNetworkInfo(): SsspDeviceNetworkInfo {
  const network = window.webapis?.network;
  if (!network) {
    throw new Error('SSSP Network API를 사용할 수 없습니다.');
  }

  const ipAddress = network.getIp?.().trim() ?? '';
  if (!ipAddress) {
    throw new Error('SSSP Network API에서 IP 주소를 확인하지 못했습니다.');
  }

  const macAddress = normalizeMacAddress(network.getMac?.().trim() ?? '');
  if (!macAddress) {
    throw new Error('SSSP Network API에서 MAC 주소를 확인하지 못했습니다.');
  }

  return {
    ipAddress,
    macAddress,
    gatewayConnected: network.isConnectedToGateway?.() ?? false,
    activeConnectionType: String(network.getActiveConnectionType?.() ?? ''),
  };
}

function normalizeMacAddress(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return '';
  }

  const hexOnly = trimmed.replace(/[^0-9a-f]/gi, '').toUpperCase();
  if (hexOnly.length === 12) {
    return hexOnly.match(/.{1,2}/g)?.join(':') ?? '';
  }

  return trimmed.toUpperCase();
}
