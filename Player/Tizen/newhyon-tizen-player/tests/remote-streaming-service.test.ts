import { describe, expect, it } from 'vitest';
import { buildRemoteStreamingGatewayUrl, resolveRemoteStreamingGatewayUrl } from '../src/app/remote-streaming-service';

describe('remote streaming service', () => {
  it('관리자 HTTP 주소를 WebSocket /ws 주소로 변환한다', () => {
    expect(buildRemoteStreamingGatewayUrl('https://newhyon-web.local/admin')).toBe('wss://newhyon-web.local/ws');
    expect(buildRemoteStreamingGatewayUrl('http://127.0.0.1:5184')).toBe('ws://127.0.0.1:5184/ws');
  });

  it('프로토콜 없는 호스트는 /ws를 한 번만 붙인다', () => {
    expect(buildRemoteStreamingGatewayUrl('10.0.0.10:5183')).toBe('ws://10.0.0.10:5183/ws');
    expect(buildRemoteStreamingGatewayUrl('10.0.0.10:5183/ws/')).toBe('ws://10.0.0.10:5183/ws');
  });

  it('명시된 WebSocket 주소는 그대로 사용한다', () => {
    expect(buildRemoteStreamingGatewayUrl('wss://stream.example/ws')).toBe('wss://stream.example/ws');
  });

  it('플레이어 설정의 원격화면 주소를 자동 결정한다', () => {
    expect(resolveRemoteStreamingGatewayUrl({
      managerAddress: 'turtlesrv.ddns.net',
      remoteStreamingGatewayUrl: 'https://newhyon-remote.turtlelab.app',
    })).toBe('wss://newhyon-remote.turtlelab.app/ws');
    expect(resolveRemoteStreamingGatewayUrl({
      managerAddress: '10.0.0.10:5183',
      remoteStreamingGatewayUrl: '',
    })).toBe('ws://10.0.0.10:5183/ws');
    expect(resolveRemoteStreamingGatewayUrl({
      managerAddress: 'turtlesrv.ddns.net',
      remoteStreamingGatewayUrl: '',
    })).toBe('wss://newhyon-remote.turtlelab.app/ws');
  });
});
