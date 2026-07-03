# NewHyOn Tizen Player

NewHyOn Tizen Web App의 빌드, 배포, 디버깅 절차를 기록한 문서입니다.

## 리모컨 조작

- 빨간 버튼: 플레이어 설정 열기
- 초록 버튼: HUD 표시 전환
- 방향키: 설정 항목 이동
- Enter: 설정 입력 키패드 열기, 토글, 적용
- Back, GoBack 또는 Return: 설정 닫기

HUD에는 마지막으로 들어온 리모컨 키, 앱 액션, 플랫폼 API 상태가 표시됩니다. 실디바이스에서 설정창이 열리지 않으면 먼저 초록 버튼으로 HUD를 켠 뒤, 빨간 버튼 입력이 `ColorF0Red -> open-settings`로 들어오는지 확인합니다.

실장비 런타임 상태는 Tizen filesystem `documents/newhyon-tizen-player-health.json`에도 기록합니다. 장비 파일 접근이 가능한 환경에서는 이 파일로 앱 내부 상태, 플랫폼 API 상태, 마지막 리모컨 입력을 확인할 수 있습니다.

## 플레이어 설정

설정은 앱 내부 `localStorage`에 저장됩니다.

- 기기 이름: 장비 식별값
- 연결 주소: 관리자 서버 주소
- HUD 표시: 앱 시작 시 HUD 표시 여부

문자 입력은 별도 키보드 없이 설정창 안의 리모컨 키패드로 처리합니다. 입력 필드에서 Enter를 누르면 키패드가 열리고, 방향키와 Enter로 문자를 넣은 뒤 Back 또는 `완료`로 필드로 돌아갑니다.

## WGT 패키징

로컬 서버 없이 WGT를 빌드하고 서명합니다.

```bash
cd Player/Tizen/newhyon-tizen-player
npm install
NEWHYON_TIZEN_PROFILE=your-signing-profile npm run package:wgt
```

Tizen CLI 실행 파일이 PATH에 없으면 `TIZEN_CLI`에 직접 지정합니다.

```bash
TIZEN_CLI=/path/to/tizen NEWHYON_TIZEN_PROFILE=your-signing-profile npm run package:wgt
```

빌드만 확인하려면 다음 명령을 사용합니다.

```bash
npm run typecheck
npm test
npm run build
```

`npm run build`는 Vite 산출물 `dist/`와 VS Code Tizen Extension이 직접 인식할 Tizen Web Project `NewHyOnTizenPlayer/`를 함께 생성합니다. VS Code에서 빌드/실행/디버그할 때는 repo 루트나 `Player/`가 아니라 `Player/Tizen/newhyon-tizen-player/NewHyOnTizenPlayer` 폴더를 Tizen Working Project로 선택합니다.

루트 `.vscode/settings.json`에는 `tizen.v2.working.project`가 위 경로로 고정되어 있습니다. 이 설정이 있어야 Tizen Actions 메뉴의 `Build Project`, `Run Project`, `Debug Project` 버튼이 `Working project is not set` 없이 동작합니다.

## QM32C 배포 절차

현재 실장비 배포는 Tizen SDK의 `tz`와 `sdb`를 직접 사용합니다. 같은 시행착오를 반복하지 않도록 아래 순서를 기준으로 합니다.

```bash
cd Player/Tizen/newhyon-tizen-player

export TIZEN_CLI=/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/tizen-core/tz
export NEWHYON_TIZEN_PROFILE=turtlelab-partner
export TIZEN_SERIAL=192.168.50.180:26101

npm run verify
TIZEN_CLI="$TIZEN_CLI" NEWHYON_TIZEN_PROFILE="$NEWHYON_TIZEN_PROFILE" npm run package:wgt
"$TIZEN_CLI" install -p Build/NewHyOnTizenPlayer.wgt -e "$TIZEN_SERIAL"
"$TIZEN_CLI" run -p NewHyOnT01 -e "$TIZEN_SERIAL"
```

배포 전 장비 연결과 설치된 AppID는 다음 명령으로 확인합니다.

```bash
sdb devices
sdb -s "$TIZEN_SERIAL" shell "0 applist" | grep "NewHyOn Tizen Player"
```

주의할 점:

- 이 환경의 CLI는 일반 `tizen` 명령이 아니라 `/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/tizen-core/tz`입니다.
- 현재 활성/사용 서명 프로필은 `turtlelab-partner`입니다.
- 장비 serial은 현재 `192.168.50.180:26101`, 모델은 `QM32C`입니다.
- 실제 AppID는 `NewHyOnT01.Player`이고 package id는 `NewHyOnT01`입니다.
- `tz run -p`는 AppID가 아니라 package id를 받으므로 `NewHyOnT01.Player`가 아니라 `NewHyOnT01`로 실행해야 합니다.
- 설치 로그에 `NewHyOnT01.NewHyOnTizenPlayer`가 보여도 실행에는 이 값을 쓰지 않습니다.
- `tz install`은 내부적으로 기존 앱을 uninstall 후 install하는 로그를 출력합니다. 앱 데이터 유지 검증이 필요할 때는 설치 후 설정/manifest 복원 상태를 반드시 확인합니다.

## QM32C 디버깅 주의사항

반복 시행착오 방지를 위해 아래 상태에서는 같은 명령을 반복하지 않습니다.

### 2026-06-28 확인 기록

- VS Code Insiders 확장 경로: `/Users/jazzlife/.vscode-insiders/extensions/tizen.vscode-tizen-csharp-10.3.8`
- 확장 구현 확인 위치: `dist/extension.js`의 `src/device-operations/debuggers/web/web-debugger.ts`
- Web App 디버그 버튼의 실제 실행 방식: `tz run-chain --proj-dir=<Tizen web project> --serial=<device> --debug-mode`
- repo 루트(`/Player/Tizen/newhyon-tizen-player`)로 `run-chain`을 실행하면 `not a valid project`로 실패합니다.
- 실제 Tizen 웹 프로젝트 디렉터리는 `/Player/Tizen/newhyon-tizen-player/NewHyOnTizenPlayer`입니다.
- 기존 `Player/`를 대상으로 `run-chain --debug-mode`를 실행하면 현재 장비에서는 `uninstall NewHyOnT01.Player` 다음 `uninstall failed[132]`로 실패했습니다.
- `--package-id=NewHyOnT01`를 추가해도 같은 `NewHyOnT01.Player` uninstall 단계에서 실패합니다.
- 성공한 디버그 연결 명령은 `--package-path=Build/NewHyOnTizenPlayer.wgt`를 함께 넘기는 방식입니다. 이때 `NewHyOnT01.NewHyOnTizenPlayer`를 uninstall/install 대상으로 처리하고 `debug 1 port: 36009`로 성공했습니다.
- 2026-07-03 수정: VS Code Extension이 `--package-path` 없이도 같은 대상명으로 패키징하도록 확장용 프로젝트 폴더명을 `NewHyOnTizenPlayer/`로 변경했습니다. `tz run-chain --proj-dir=.../NewHyOnTizenPlayer --serial=192.168.50.180:26101 --debug-mode`는 `NewHyOnTizenPlayer/Debug/NewHyOnTizenPlayer.wgt`를 만들고 `NewHyOnT01.NewHyOnTizenPlayer` 대상으로 성공했습니다.
- `/tmp`에서 `tz run --package-id=NewHyOnT01 --serial=192.168.50.180:26101 --debug-mode`를 실행하면 `successfully launched ... with debug 0`이 반환됩니다. 이것은 기존 일반 실행 인스턴스 재사용이며 디버거 연결이 아닙니다.
- 같은 시점에 `sdb -s 192.168.50.180:26101 shell "0 applist | grep -i NewHyOn"`은 `closed`를 반환했습니다. `sdb devices`에 장비가 보여도 shell 채널은 깨질 수 있습니다.
- 추가 확인: `tz install -p Build/NewHyOnTizenPlayer.wgt -e 192.168.50.180:26101`는 `NewHyOnT01.NewHyOnTizenPlayer`를 uninstall/install 대상으로 처리하며 성공합니다.
- 추가 확인: `Build/NewHyOnTizenPlayer.wgt`와 기존 실패 산출물 `Player/Debug/Player.wgt` 내부 `config.xml`은 모두 `<tizen:application id="NewHyOnT01.Player" package="NewHyOnT01" ... />`입니다.
- 결론: 기존 `Player/` 폴더 기준 디버거 실패의 핵심은 Web Inspector 포트가 아니라 `run-chain`이 제거하려는 app id(`NewHyOnT01.Player`)와 실제 설치 체인에서 처리되는 app id(`NewHyOnT01.NewHyOnTizenPlayer`)가 어긋나는 것입니다. 현재 확장용 프로젝트는 이 문제를 피하기 위해 `NewHyOnTizenPlayer/` 폴더명으로 생성합니다.
- `/tmp/sdb.log`가 root 소유일 경우 `sdb start-server`가 `failed to open '/tmp/sdb.log'`로 실패할 수 있습니다.

### VS Code Insiders 확장과 같은 디버그 실행 경로

- VS Code Insiders의 Tizen Extension `tizen.vscode-tizen-csharp-10.3.8`은 Web App 디버깅 시 직접 `0 debug`를 조립하지 않습니다. 확장 구현은 `dist/extension.js`의 `src/device-operations/debuggers/web/web-debugger.ts`에서 아래 `tz run-chain` 명령을 실행합니다.

```bash
/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/tizen-core/tz run-chain \
  --proj-dir=/Users/jazzlife/Documents/Workspaces/Products/NewHyOn/Player/Tizen/newhyon-tizen-player/NewHyOnTizenPlayer \
  --serial=192.168.50.180:26101 \
  --debug-mode
```

- `run-chain --debug-mode`는 Tizen 웹 프로젝트 디렉터리만 받습니다. repo 루트는 `not a valid project`로 실패하므로 `NewHyOnTizenPlayer/`를 지정해야 합니다.
- 기존 `Player/` 폴더로 실행하면 `NewHyOnT01.Player` uninstall 단계에서 실패합니다. 이 폴더는 더 이상 생성하지 않습니다.
- `run-chain --debug-mode`가 `uninstall NewHyOnT01.Player` 이후 `uninstall failed[132]`로 실패하면 Web Inspector 포트 이전 단계에서 막힌 것입니다. VS Code Working Project가 `NewHyOnTizenPlayer/`인지 먼저 확인합니다.
- `successfully launched ... with debug 1 port: <port>`가 나오면 아래처럼 포워딩 후 타겟을 확인합니다.

```bash
sdb -s 192.168.50.180:26101 forward tcp:<port> tcp:<port>
curl http://127.0.0.1:<port>/json/list
```

- `sdb devices`에 `192.168.50.180:26101 device QM32C`가 보여도 `sdb shell` stdout이 비거나 `closed`만 반환할 수 있습니다.
- 이 상태에서 `sdb -s "$TIZEN_SERIAL" shell "0 debug NewHyOnT01.Player"`를 반복하면 포트를 반환하지 않고 대기 상태로 멈춥니다.
- `sdb forward tcp:<port> tcp:<port>`를 잡아도 `curl http://127.0.0.1:<port>/json/list`가 connection refused이면 Web Inspector가 열린 것이 아닙니다. 같은 forward/debug 명령을 반복하지 않습니다.
- `tz run -p NewHyOnT01 -e "$TIZEN_SERIAL" -d`가 `debug 0`으로 반환하면 이미 일반 실행 인스턴스를 재사용한 것이며 디버거가 붙은 상태가 아닙니다.

### 다음 디버깅 시 판단 순서

- 먼저 VS Code 확장과 같은 `tz run-chain --debug-mode` 명령을 `NewHyOnTizenPlayer/` 디렉터리 대상으로 실행합니다.
- `not a valid project`가 나오면 경로를 repo 루트로 잘못 준 것입니다. 다른 디버그 방법을 시도하지 말고 `NewHyOnTizenPlayer/` 경로로 고칩니다.
- `uninstall failed[132]`가 나오면 디버거 포트 단계까지 가지 못한 것입니다. 이때 `sdb forward`, `json/list`, `0 debug`를 반복하지 않습니다.
- `debug 0`이 나오면 디버거 연결 성공이 아닙니다. 기존 일반 실행 인스턴스를 재사용한 상태로 판단합니다.
- `sdb shell`이 비거나 `closed`이면 장비가 목록에 보여도 shell 채널이 깨진 상태입니다. 앱 코드 문제가 아니라 장비 SDB/개발자 연결 상태를 먼저 의심합니다.
- 위 증상이 나오면 먼저 SDB를 재시작합니다.

```bash
sdb kill-server
sdb start-server
sdb connect 192.168.50.180:26101
sdb devices
```

- 그래도 `sdb shell "echo ok"` 출력이 비거나 `closed`가 반복되면 장비 쪽 SDB/개발자 연결이 깨진 상태로 보고, 장비 개발자 모드/SDB 연결을 재설정하거나 물리 재부팅 후 다시 시도합니다.
- 이 상태에서는 Web Inspector 확인 대신 앱 내 HUD와 `documents/newhyon-tizen-player-health.json` 헬스 스냅샷, 서버 `CommandQueue`/`CommandHistory` 상태를 우선 증거로 사용합니다.
