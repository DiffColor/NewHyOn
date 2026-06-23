import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const projectRoot = process.env.NEWHYON_TIZEN_PLAYER_ROOT ?? process.cwd();

describe('config.xml', () => {
  it('실장비 재생과 헬스 스냅샷에 필요한 Tizen 권한을 선언한다', () => {
    const config = readFileSync(resolve(projectRoot, 'public/config.xml'), 'utf8');

    expect(config).toContain('http://tizen.org/privilege/internet');
    expect(config).toContain('http://tizen.org/privilege/appmanager.launch');
    expect(config).toContain('http://tizen.org/privilege/download');
    expect(config).toContain('http://tizen.org/privilege/filesystem.read');
    expect(config).toContain('http://tizen.org/privilege/filesystem.write');
    expect(config).toContain('http://tizen.org/privilege/tv.inputdevice');
    expect(config).toContain('http://tizen.org/privilege/tv.audio');
    expect(config).toContain('http://developer.samsung.com/privilege/avplay');
    expect(config).toContain('http://developer.samsung.com/privilege/network.public');
    expect(config).toContain('http://developer.samsung.com/privilege/productinfo');
    expect(config).toContain('http://developer.samsung.com/privilege/remotepower');
    expect(config).toContain('http://developer.samsung.com/privilege/systemcontrol');
    expect(config).toContain('http://developer.samsung.com/privilege/widgetdata');
  });

  it('실디바이스에서 설치 확인된 10자 package id를 유지한다', () => {
    const config = readFileSync(resolve(projectRoot, 'public/config.xml'), 'utf8');

    expect(config).toContain('id="https://newhyon.local/tizen-player"');
    expect(config).not.toContain('nado.local');
    expect(config).toContain('id="NewHyOnT01.Player"');
    expect(config).toContain('package="NewHyOnT01"');
  });
});
