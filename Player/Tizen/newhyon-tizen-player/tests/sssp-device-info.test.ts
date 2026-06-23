import { beforeEach, describe, expect, it, vi } from 'vitest';
import { readSsspDeviceNetworkInfo } from '../src/app/sssp-device-info';

describe('readSsspDeviceNetworkInfo', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('SSSP Network API에서 IP와 MAC을 읽고 MAC 형식을 정규화한다', () => {
    window.webapis = {
      network: {
        getIp: () => '192.168.50.180',
        getMac: () => 'aabbccddeeff',
        isConnectedToGateway: () => true,
        getActiveConnectionType: () => 'ETHERNET',
      },
    };

    expect(readSsspDeviceNetworkInfo()).toEqual({
      ipAddress: '192.168.50.180',
      macAddress: 'AA:BB:CC:DD:EE:FF',
      gatewayConnected: true,
      activeConnectionType: 'ETHERNET',
    });
  });

  it('SSSP Network API가 없으면 실패한다', () => {
    window.webapis = {};

    expect(() => readSsspDeviceNetworkInfo()).toThrow('SSSP Network API를 사용할 수 없습니다.');
  });
});
