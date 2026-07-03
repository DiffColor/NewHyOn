# AGENTS.md

## 기본 응답/작업 원칙

- 응답은 항상 한글을 기준으로 한다.
- 코드를 수정할 때는 반드시 현재 시점의 코드를 다시 읽어서 그것을 기준으로 한다.
- 코드 분석을 할 때 추측이 아닌 현재 시점의 코드를 직접 읽어서 분석한다.
- 문제가 발생했을 때는 코드를 먼저 의심한다.
- 사소해보이는 코드라도 사소하게 여기지 않는다. 작은 차이가 큰 문제를 만들 수 있다.
- 목적에 맞게 실제로 동작하게 하는 것에 중점을 둔다.

## QM32C Tizen 플레이어 디버깅 기록

다음 문제는 이미 확인한 내용이므로 같은 시행착오를 반복하지 않는다.

- 대상 장비: `192.168.50.180:26101`, 모델 `QM32C`
- Tizen CLI: `/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/tizen-core/tz`
- SDB: `/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/sdb`
- 서명 프로필: `turtlelab-partner`
- VS Code Insiders Tizen Extension: `/Users/jazzlife/.vscode-insiders/extensions/tizen.vscode-tizen-csharp-10.3.8`

### 디버거 연결 방식

VS Code Insiders의 Tizen Extension은 Web App 디버깅 시 직접 `sdb shell "0 debug ..."`를 조립하지 않는다. 확장 구현은 `dist/extension.js`의 `src/device-operations/debuggers/web/web-debugger.ts`에서 아래 명령을 실행한다.

```bash
/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/tizen-core/tz run-chain \
  --proj-dir=/Users/jazzlife/Documents/Workspaces/Products/NewHyOn/Player/Tizen/newhyon-tizen-player/NewHyOnTizenPlayer \
  --serial=192.168.50.180:26101 \
  --debug-mode
```

`--proj-dir`는 repo 루트가 아니라 실제 Tizen 웹 프로젝트인 `Player/Tizen/newhyon-tizen-player/NewHyOnTizenPlayer`여야 한다. repo 루트를 주면 `not a valid project`로 실패한다.

2026-07-03 수정 전에는 확장용 프로젝트 폴더명이 `Player`라서 VS Code Extension 기본 명령이 `Player/Debug/Player.wgt`를 만들고 `NewHyOnT01.Player` uninstall 단계에서 실패했다. 현재는 확장용 프로젝트 폴더명을 `NewHyOnTizenPlayer`로 맞춰서 Extension 기본 `run-chain --debug-mode` 경로가 `NewHyOnTizenPlayer.wgt`를 만들도록 수정했다.

수정 후 디버거 직접 확인 명령은 다음과 같다.

```bash
/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/tizen-core/tz run-chain \
  --proj-dir=/Users/jazzlife/Documents/Workspaces/Products/NewHyOn/Player/Tizen/newhyon-tizen-player/NewHyOnTizenPlayer \
  --serial=192.168.50.180:26101 \
  --debug-mode
```

성공 기준은 `successfully launched ... with debug 1 port: <port>`이다. 이후 다음처럼 포워딩과 Web Inspector 타겟을 확인한다.

```bash
/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/sdb -s 192.168.50.180:26101 forward tcp:<port> tcp:<port>
curl http://127.0.0.1:<port>/json/list
```

### 2026-06-28 확인된 실패 원인

- `tz run-chain --debug-mode`는 기존 `Player` 폴더 기준이면 `uninstall NewHyOnT01.Player` 단계에서 `uninstall failed[132]`로 실패한다.
- `tz run-chain --proj-dir=.../Player --package-id=NewHyOnT01 --debug-mode`도 같은 `NewHyOnT01.Player` uninstall 단계에서 실패한다. `--package-id`만 추가하는 시도는 반복하지 않는다.
- 반면 실제 배포에 쓰는 `tz install -p Build/NewHyOnTizenPlayer.wgt -e 192.168.50.180:26101`는 `NewHyOnT01.NewHyOnTizenPlayer`를 uninstall/install 대상으로 처리하며 성공한다.
- `tz run-chain --proj-dir=.../Player --package-path=.../Build/NewHyOnTizenPlayer.wgt --serial=192.168.50.180:26101 --debug-mode`는 `NewHyOnT01.NewHyOnTizenPlayer`를 대상으로 uninstall/install 후 `debug 1 port: 36009`로 성공했다.
- `tz run-chain --proj-dir=.../NewHyOnTizenPlayer --serial=192.168.50.180:26101 --debug-mode`는 Extension 기본 방식 그대로 `NewHyOnTizenPlayer/Debug/NewHyOnTizenPlayer.wgt`를 만들고 `NewHyOnT01.NewHyOnTizenPlayer` 대상으로 성공한다.
- WGT 내부 `config.xml`에는 `<tizen:application id="NewHyOnT01.Player" package="NewHyOnT01" ... />`가 들어 있다.
- 즉 현재 디버거 실패의 핵심은 Web Inspector 포트 문제가 아니라, `run-chain`이 제거하려는 app id(`NewHyOnT01.Player`)와 장비/설치 체인에서 실제로 처리되는 app id(`NewHyOnT01.NewHyOnTizenPlayer`)가 어긋나는 것이다.
- `tz run -p NewHyOnT01 -e 192.168.50.180:26101 -d`가 `debug 0`을 반환하면 디버거 연결이 아니다. 기존 일반 실행 인스턴스를 재사용한 것이다.
- `sdb shell "0 debug NewHyOnT01.Player"`는 포트를 반환하지 않고 대기할 수 있다. 이 상태에서 같은 명령을 반복하지 않는다.
- `sdb devices`에 장비가 보여도 `sdb shell` 출력이 비거나 `closed`만 나올 수 있다. 이때는 장비 shell 채널이 깨진 상태로 본다.
- `/tmp/sdb.log`가 root 소유(`root wheel`)면 `sdb start-server`가 `failed to open '/tmp/sdb.log'`로 실패할 수 있다.

### 다음 에이전트의 우선순위

1. 플레이어 화면/상태를 확인하라는 요청이면 서버 조회로 새지 말고 먼저 플레이어 디버거 또는 장비 직접 증거를 확인한다.
2. 디버거 연결은 VS Code 확장과 같은 `tz run-chain --debug-mode` 경로를 기준으로 확인한다.
3. `uninstall failed[132]`가 나오면 `sdb forward`, `json/list`, `0 debug` 반복을 중단하고 app id 불일치부터 해결한다.
4. 디버거가 붙지 않은 상태에서 `debug 0` 실행 결과를 디버그 성공으로 판단하지 않는다.
5. 자세한 배포/디버깅 기록은 `Player/Tizen/newhyon-tizen-player/README.md`의 `QM32C 디버깅 주의사항`을 먼저 읽는다.
