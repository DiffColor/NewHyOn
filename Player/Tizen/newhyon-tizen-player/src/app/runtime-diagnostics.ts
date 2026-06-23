export interface RuntimeDiagnostics {
  readonly webapis: boolean;
  readonly avplay: boolean;
  readonly avplaystore: boolean;
  readonly network: boolean;
  readonly productinfo: boolean;
  readonly systemcontrol: boolean;
  readonly remotepower: boolean;
  readonly tizen: boolean;
  readonly download: boolean;
  readonly filesystem: boolean;
  readonly tvinputdevice: boolean;
  readonly tvaudiocontrol: boolean;
}

function mark(value: boolean): string {
  return value ? 'OK' : 'MISS';
}

export function collectRuntimeDiagnostics(): RuntimeDiagnostics {
  return {
    webapis: Boolean(window.webapis),
    avplay: Boolean(window.webapis?.avplay),
    avplaystore: Boolean(window.webapis?.avplaystore),
    network: Boolean(window.webapis?.network),
    productinfo: Boolean(window.webapis?.productinfo),
    systemcontrol: Boolean(window.webapis?.systemcontrol),
    remotepower: Boolean(window.webapis?.remotepower),
    tizen: Boolean(window.tizen),
    download: Boolean(window.tizen?.download),
    filesystem: Boolean(window.tizen?.filesystem),
    tvinputdevice: Boolean(window.tizen?.tvinputdevice),
    tvaudiocontrol: Boolean(window.tizen?.tvaudiocontrol),
  };
}

export function formatRuntimeDiagnostics(diagnostics: RuntimeDiagnostics): string {
  return [
    `webapis=${mark(diagnostics.webapis)}`,
    `avplay=${mark(diagnostics.avplay)}`,
    `avplaystore=${mark(diagnostics.avplaystore)}`,
    `network=${mark(diagnostics.network)}`,
    `productinfo=${mark(diagnostics.productinfo)}`,
    `systemcontrol=${mark(diagnostics.systemcontrol)}`,
    `remotepower=${mark(diagnostics.remotepower)}`,
    `tizen=${mark(diagnostics.tizen)}`,
    `download=${mark(diagnostics.download)}`,
    `filesystem=${mark(diagnostics.filesystem)}`,
    `input=${mark(diagnostics.tvinputdevice)}`,
    `audio=${mark(diagnostics.tvaudiocontrol)}`,
  ].join(' ');
}
