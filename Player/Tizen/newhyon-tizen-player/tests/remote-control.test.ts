import { describe, expect, it } from 'vitest';
import { resolveRemoteControlAction } from '../src/input/remote-control';

describe('remote control', () => {
  it('리모컨 특수키를 플레이어 명령으로 변환한다', () => {
    expect(resolveRemoteControlAction({ key: 'MediaPlayPause' })).toBe('toggle-playback');
    expect(resolveRemoteControlAction({ key: 'MediaStop' })).toBe('stop-playback');
    expect(resolveRemoteControlAction({ key: 'MediaFastForward' })).toBe('next-page');
    expect(resolveRemoteControlAction({ key: 'MediaRewind' })).toBe('previous-page');
    expect(resolveRemoteControlAction({ key: 'ColorF0Red' })).toBe('open-settings');
    expect(resolveRemoteControlAction({ key: 'ColorF1Green' })).toBe('toggle-hud');
    expect(resolveRemoteControlAction({ key: 'ColorF2Yellow' })).toBe('previous-page');
    expect(resolveRemoteControlAction({ key: 'ColorF3Blue' })).toBe('next-page');
  });

  it('실장비 keyCode와 기본 리모컨 키로 설정창을 연다', () => {
    expect(resolveRemoteControlAction({ keyCode: 403 })).toBe('open-settings');
    expect(resolveRemoteControlAction({ keyCode: 48 })).toBe('open-settings');
    expect(resolveRemoteControlAction({ key: '0' })).toBe('open-settings');
    expect(resolveRemoteControlAction({ code: 'Digit0' })).toBe('open-settings');
    expect(resolveRemoteControlAction({ key: 'Info' })).toBe('open-settings');
    expect(resolveRemoteControlAction({ key: 'Menu' })).toBe('open-settings');
    expect(resolveRemoteControlAction({ key: 'Tools' })).toBe('open-settings');
    expect(resolveRemoteControlAction({ keyCode: 18 })).toBe('open-settings');
    expect(resolveRemoteControlAction({ keyCode: 10135 })).toBe('open-settings');
    expect(resolveRemoteControlAction({ keyCode: 457 })).toBe('open-settings');
  });

  it('ABCD 표기 리모컨 키를 컬러키 동작으로 처리한다', () => {
    expect(resolveRemoteControlAction({ key: 'A' })).toBe('open-settings');
    expect(resolveRemoteControlAction({ key: 'B' })).toBe('toggle-hud');
    expect(resolveRemoteControlAction({ key: 'C' })).toBe('previous-page');
    expect(resolveRemoteControlAction({ key: 'D' })).toBe('next-page');
    expect(resolveRemoteControlAction({ keyCode: 65 })).toBe('open-settings');
    expect(resolveRemoteControlAction({ keyCode: 66 })).toBe('toggle-hud');
    expect(resolveRemoteControlAction({ keyCode: 67 })).toBe('previous-page');
    expect(resolveRemoteControlAction({ keyCode: 68 })).toBe('next-page');
  });

  it('실장비 미디어 keyCode를 플레이어 명령으로 변환한다', () => {
    expect(resolveRemoteControlAction({ keyCode: 10252 })).toBe('toggle-playback');
    expect(resolveRemoteControlAction({ keyCode: 413 })).toBe('stop-playback');
    expect(resolveRemoteControlAction({ keyCode: 417 })).toBe('next-page');
    expect(resolveRemoteControlAction({ keyCode: 412 })).toBe('previous-page');
    expect(resolveRemoteControlAction({ keyCode: 404 })).toBe('toggle-hud');
  });
});
