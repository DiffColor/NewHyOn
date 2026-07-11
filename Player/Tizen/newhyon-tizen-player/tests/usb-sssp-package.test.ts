import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const projectRoot = process.env.NEWHYON_TIZEN_PLAYER_ROOT ?? process.cwd();

describe('SSSP USB 패키지', () => {
  it('WGT와 sssp_config.xml만 배포하고 하이브리드 패키지를 만들지 않는다', () => {
    const publishScript = readFileSync(resolve(projectRoot, 'scripts/publish-usb.sh'), 'utf8');
    const verifyScript = readFileSync(resolve(projectRoot, 'scripts/verify-sssp-package.sh'), 'utf8');

    expect(publishScript).toContain('npm run package:wgt');
    expect(publishScript).toContain('verify-sssp-package.sh');
    expect(publishScript).toContain('sssp_config.xml');
    expect(publishScript).toContain('"${PUBLISH_DIR}"/*.tpk');
    expect(publishScript).not.toContain(' -t tpk ');
    expect(publishScript).not.toContain('HYBRID_');
    expect(verifyScript).toContain('CONTENT_PATH}" != "index.html"');
    expect(verifyScript).toContain('res/(tpk|wgt)');
  });
});
