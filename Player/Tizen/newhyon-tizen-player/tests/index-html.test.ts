import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const projectRoot = process.env.NEWHYON_TIZEN_PLAYER_ROOT ?? process.cwd();

describe('index.html', () => {
  it('Samsung Product WebAPI 스크립트를 앱 모듈보다 먼저 로드한다', () => {
    const html = readFileSync(resolve(projectRoot, 'index.html'), 'utf8');
    const webapiIndex = html.indexOf('src="$WEBAPIS/webapis/webapis.js"');
    const appIndex = html.indexOf('src="./src/main.ts"');

    expect(webapiIndex).toBeGreaterThan(-1);
    expect(appIndex).toBeGreaterThan(-1);
    expect(webapiIndex).toBeLessThan(appIndex);
  });

  it('로딩 화면은 민감한 endpoint 대신 카드형 stepper를 제공한다', () => {
    const html = readFileSync(resolve(projectRoot, 'index.html'), 'utf8');

    expect(html).toContain('id="loading-overlay" class="loading-overlay loading-overlay--hidden"');
    expect(html).toContain('class="loading-stepper"');
    expect(html).toContain('서버 확인');
    expect(html).toContain('실시간 연결');
    expect(html).toContain('콘텐츠 저장소');
    expect(html).toContain('기기 인증');
    expect(html).not.toContain('id="loading-lottie"');
    expect(html).not.toContain('<dt>DB</dt>');
    expect(html).not.toContain('<dt>SignalR</dt>');
    expect(html).not.toContain('<dt>FTP</dt>');
  });
});
