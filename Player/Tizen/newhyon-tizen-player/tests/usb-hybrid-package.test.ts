import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const projectRoot = process.env.NEWHYON_TIZEN_PLAYER_ROOT ?? process.cwd();

describe('USB 하이브리드 패키지', () => {
  it('하이브리드 WGT의 실제 웹 시작 경로를 사용한다', () => {
    const publishScript = readFileSync(resolve(projectRoot, 'scripts/publish-usb.sh'), 'utf8');
    const packageScript = readFileSync(resolve(projectRoot, 'scripts/package-clean.sh'), 'utf8');
    const verifyScript = readFileSync(resolve(projectRoot, 'scripts/verify-hybrid-package.sh'), 'utf8');

    expect(publishScript).toContain('NEWHYON_TIZEN_CONTENT_PATH="res/wgt/index.html"');
    expect(publishScript).toContain('verify-hybrid-package.sh');
    expect(packageScript).toContain('NEWHYON_TIZEN_CONTENT_PATH');
    expect(packageScript).not.toContain('CONTENT_DIR=');
    expect(verifyScript).toContain('res/wgt/index.html');
    expect(verifyScript).toContain('ICON_PATH');
    expect(verifyScript).toContain('newhyon-app-icon.png');
    expect(verifyScript).toContain("build_type: Release");
    expect(readFileSync(resolve(projectRoot, 'scripts/postbuild.mjs'), 'utf8')).toContain("?? 'Release'");
  });
});
