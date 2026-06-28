# NewHyOn Tizen Player

Windows NewHyOn Player의 `PageInfoClass -> ElementInfoClass -> ContentsInfoClass` 재생 모델을 Tizen Web App으로 옮긴 플레이어입니다. Android/Windows 원본처럼 페이지 안의 Media 요소를 배치하고, 각 요소 안의 이미지/영상 콘텐츠를 지정 시간만큼 재생합니다.

## 리모컨 조작

- 빨간 버튼: 플레이어 설정 열기
- 초록 버튼: HUD 표시 전환
- 방향키: 설정 항목 이동
- Enter: 설정 입력 키패드 열기, 토글, 적용
- Back, GoBack 또는 Return: 설정 닫기
- MediaPlayPause: 재생/일시정지
- MediaStop: 정지
- MediaFastForward: 다음 페이지
- MediaRewind: 이전 페이지

HUD에는 마지막으로 들어온 리모컨 키, 앱 액션, 플랫폼 API 상태가 표시됩니다. 실디바이스에서 설정창이 열리지 않으면 먼저 초록 버튼으로 HUD를 켠 뒤, 빨간 버튼 입력이 `ColorF0Red -> open-settings`로 들어오는지 확인합니다. 영상이 나오지 않으면 `webapis`, `avplay`, `avplaystore`, `filesystem` 상태가 `OK`인지 먼저 확인합니다.

실장비 런타임 상태는 Tizen filesystem `documents/newhyon-tizen-player-health.json`에도 기록합니다. 장비 파일 접근이 가능한 환경에서는 이 파일로 마지막 페이지, 슬롯 상태, 플랫폼 API 상태, 마지막 리모컨 입력을 확인할 수 있습니다.

## 플레이어 설정

설정은 앱 내부 `localStorage`에 저장됩니다.

- 기기 이름: 장비 식별값
- 연결 주소: 관리자 서버 주소
- Manifest URL: 외부 `PlayerManifest` JSON 주소
- 화면 비율 유지: 이미지/영상 표시 비율 유지 여부
- 현재 콘텐츠 종료 후 전환: 영상 슬롯에서 타이머보다 영상 종료 이벤트를 우선해 다음 콘텐츠로 전환
- HUD 표시: 앱 시작 시 HUD 표시 여부

`Manifest URL`을 비워두면 `src/app/default-manifest.ts`의 기본 재생 데이터가 사용됩니다.

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
- 실제 Tizen 웹 프로젝트 디렉터리는 `/Player/Tizen/newhyon-tizen-player/Player`입니다.
- `Player/`를 대상으로 `run-chain --debug-mode`를 실행하면 현재 장비에서는 `uninstall NewHyOnT01.Player` 다음 `uninstall failed[132]`로 실패했습니다.
- `--package-id=NewHyOnT01`를 추가해도 같은 `NewHyOnT01.Player` uninstall 단계에서 실패합니다.
- 성공한 디버그 연결 명령은 `--package-path=Build/NewHyOnTizenPlayer.wgt`를 함께 넘기는 방식입니다. 이때 `NewHyOnT01.NewHyOnTizenPlayer`를 uninstall/install 대상으로 처리하고 `debug 1 port: 36009`로 성공했습니다.
- `/tmp`에서 `tz run --package-id=NewHyOnT01 --serial=192.168.50.180:26101 --debug-mode`를 실행하면 `successfully launched ... with debug 0`이 반환됩니다. 이것은 기존 일반 실행 인스턴스 재사용이며 디버거 연결이 아닙니다.
- 같은 시점에 `sdb -s 192.168.50.180:26101 shell "0 applist | grep -i NewHyOn"`은 `closed`를 반환했습니다. `sdb devices`에 장비가 보여도 shell 채널은 깨질 수 있습니다.
- 추가 확인: `tz install -p Build/NewHyOnTizenPlayer.wgt -e 192.168.50.180:26101`는 `NewHyOnT01.NewHyOnTizenPlayer`를 uninstall/install 대상으로 처리하며 성공합니다.
- 추가 확인: `Build/NewHyOnTizenPlayer.wgt`와 `Player/Debug/Player.wgt` 내부 `config.xml`은 모두 `<tizen:application id="NewHyOnT01.Player" package="NewHyOnT01" ... />`입니다.
- 결론: 현재 디버거 실패의 핵심은 Web Inspector 포트가 아니라 `run-chain`이 제거하려는 app id(`NewHyOnT01.Player`)와 실제 설치 체인에서 처리되는 app id(`NewHyOnT01.NewHyOnTizenPlayer`)가 어긋나는 것입니다.
- `/tmp/sdb.log`가 root 소유일 경우 `sdb start-server`가 `failed to open '/tmp/sdb.log'`로 실패할 수 있습니다.

### VS Code Insiders 확장과 같은 디버그 실행 경로

- VS Code Insiders의 Tizen Extension `tizen.vscode-tizen-csharp-10.3.8`은 Web App 디버깅 시 직접 `0 debug`를 조립하지 않습니다. 확장 구현은 `dist/extension.js`의 `src/device-operations/debuggers/web/web-debugger.ts`에서 아래 `tz run-chain` 명령을 실행합니다.

```bash
/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/tizen-core/tz run-chain \
  --proj-dir=/Users/jazzlife/Documents/Workspaces/Products/NewHyOn/Player/Tizen/newhyon-tizen-player/Player \
  --package-path=/Users/jazzlife/Documents/Workspaces/Products/NewHyOn/Player/Tizen/newhyon-tizen-player/Build/NewHyOnTizenPlayer.wgt \
  --serial=192.168.50.180:26101 \
  --debug-mode
```

- `run-chain --debug-mode`는 Tizen 웹 프로젝트 디렉터리만 받습니다. repo 루트는 `not a valid project`로 실패하므로 `Player/`를 지정해야 합니다.
- 현재 QM32C에서는 `--package-path` 없이 실행하면 `NewHyOnT01.Player` uninstall 단계에서 실패합니다. 위 명령처럼 배포 WGT를 직접 지정해야 Web Inspector 포트가 열립니다.
- `run-chain --debug-mode`가 `uninstall NewHyOnT01.Player` 이후 `uninstall failed[132]`로 실패하면 Web Inspector 포트 이전 단계에서 막힌 것입니다. 이때 `sdb forward`나 `json/list`를 반복하지 말고, 실행 중인 앱/장비 SDB 상태를 먼저 정상화해야 합니다.
- 단, 2026-06-28 현재는 단순 실행 상태 문제가 아니라 app id 불일치가 확인되었습니다. `run-chain`이 `NewHyOnT01.Player`를 제거하려는 동안 정상 배포 체인은 `NewHyOnT01.NewHyOnTizenPlayer`를 제거/설치합니다. 이 불일치를 해결하기 전에는 같은 디버그 명령을 반복하지 않습니다.
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

- 먼저 VS Code 확장과 같은 `tz run-chain --debug-mode` 명령을 `Player/` 디렉터리 대상으로 실행합니다.
- `not a valid project`가 나오면 경로를 repo 루트로 잘못 준 것입니다. 다른 디버그 방법을 시도하지 말고 `Player/` 경로로 고칩니다.
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

### 2026-06-28 플레이리스트 업데이트 후 상태 확인

Web Inspector로 현재 플레이어를 직접 확인한 결과, 앱은 업데이트 실패나 통신 장애 상태가 아니었습니다.

- 상태: `playing`
- 플레이리스트: `단일`
- 페이지: `1/1 No active content`
- 업데이트: `대기`
- 통신: `DB: 연결됨 / SignalR: 연결됨 / FTP: 연결됨 / Heartbeat: sent`
- 인증: `LicenseHub: authenticated`

저장된 `newhyon-tizen-player:remote-manifest`의 `단일` 플레이리스트에는 영상 2개가 있고 둘 다 `CIF_FileExist=true`입니다. 다만 `newhyon-tizen-player.content-periods.v1` 기준 두 콘텐츠가 현재일 `2026-06-28`에 모두 기간 만료입니다.

- `a42b06ed-212d-47e2-8769-4a7108f68d29`: `2026-05-07` 종료
- `3039db8c-2c4f-475b-8a99-c41e43e2b6f6`: `2026-06-27` 종료

따라서 이 상태의 직접 원인은 플레이리스트 파일 다운로드 실패가 아니라 `content-period.ts`의 `isContentPeriodAllowed()`가 모든 콘텐츠를 제외하여 `newhyon-player-app.ts`의 `createBlackoutPagePlans()`로 진입한 것입니다.

## Tizen 하드웨어 제약

다음 제약은 코드 작성 시 반드시 지켜야 합니다.

- `index.html`은 `$WEBAPIS/webapis/webapis.js`를 앱 모듈보다 먼저 로드해야 합니다. 이 스크립트가 빠지면 `webapis.avplay`/`webapis.avplaystore`를 안정적으로 사용할 수 없습니다.
- `webapis.avplaystore.getPlayer()`는 병렬 pre-buffering용 플레이어를 생성하며 동시에 최대 4개까지입니다.
- 제한 초과 시 `QUOTA_EXCEEDED_ERR`와 `Max player count reached`가 발생합니다.
- 앱 시작 시 영상 슬롯 수만큼 AVPlayStore 플레이어를 미리 만들면 QM32C에서 즉시 실패합니다.
- 현재 구현은 앱 시작 시 `createAvplaySessionPair()`로 AVPlay 플레이어 2개짜리 고정 페어 2세트를 정확히 확보합니다. 즉 `2 players x 2 pairs = 4 getPlayer()`가 의도한 보유량이자 절대 한도입니다.
- 페어 1개 안의 두 AVPlay 플레이어는 현재 영상 lane과 다음 영상 준비 lane입니다. 스케줄-스케줄 전환 때문에 이 페어가 2세트 필요합니다.
- `SlotPlayer`의 컨텐츠 전환 상태(`preparedItemId`, `preparedImageId`, `preparedBoundaryImageId`)는 콘텐츠 id 기준을 유지합니다. 페이지/스케줄 전환도 결국 슬롯 안의 이미지/영상 컨텐츠 전환이므로 이 논리 전환 키를 실제 소스 키로 바꾸지 않습니다.
- 실제 재생 소스(`contentType + sourceUrl`) 기준 합류는 `AvplaySession` 내부의 in-flight `prepareAsync` 직렬화 범위에서만 처리합니다. 같은 파일이 다른 페이지/명령 id로 들어오는 경우에도 동일 lane을 다시 `open()`하면 안 되기 때문입니다.
- AVPlay 준비 절차는 레퍼런스 샘플 `Player/Tizen/avplay-seamless-still-mode alias` 기준으로 `open -> setListener -> setDisplayRect -> setDisplayMethod -> prepareAsync 완료 -> play` 순서를 지킵니다.
- `prepareAsync`가 진행 중인 player에는 `stop()`, `close()`, 다른 `open()`을 끼워 넣지 않습니다. `clearPrepared()`는 준비 메타 정리 요청이며 in-flight prepare를 폐기하는 명령이 아닙니다.
- 다른 영상을 준비해야 하면 진행 중인 `prepareAsync`가 끝난 뒤 준비 lane을 `stop/close`하고 새 소스를 `open/prepareAsync`합니다.
- `pool`/`lease` 방식으로 세션을 빌려주거나 반납하는 구조를 다시 만들지 않습니다.
- 페이지-페이지 연결과 컨텐츠-컨텐츠 연결은 별도 표면 교체가 아니라 각 슬롯의 이미지/영상 콘텐츠 전환입니다.
- AVPlayStore 한도 초과가 예상되면 추가 `getPlayer()`를 호출하지 않습니다. 초과 영상 슬롯은 앱 전체 오류로 전파하지 않고 HUD 슬롯 상태에 `ERROR`로 남깁니다.
- 이미지 슬롯, 설정창, HUD 렌더링, snapshot, stop 처리에서는 AVPlayStore 세션을 새로 만들면 안 됩니다.
- Tizen AVPlay는 상대 경로를 직접 열 수 없습니다. 패키지 로컬 파일은 Tizen filesystem으로 절대 경로로 변환해야 합니다.
- AVPlay 표시 영역은 CSS만으로 끝나지 않습니다. `setDisplayRect()`를 1920x1080 기준 좌표로 다시 매핑해야 합니다.
- 영상 비율 유지 설정은 AVPlay에도 직접 적용해야 합니다. `화면 비율 유지=ON`은 `PLAYER_DISPLAY_MODE_LETTER_BOX`, `OFF`는 `PLAYER_DISPLAY_MODE_FULL_SCREEN`을 사용합니다.
- Tizen TV 오디오는 `tizen.tvaudiocontrol` 전역 제어입니다. Windows처럼 슬롯별 mute를 완전히 분리할 수 없어서 페이지에 unmuted 영상 슬롯이 하나라도 있으면 TV mute를 해제하고, 없으면 mute 처리합니다. 앱 정지/종료 시 시작 전 mute 상태를 복원합니다.
- 런타임 헬스 스냅샷을 남기려면 `filesystem.write` 권한이 필요합니다. 이 파일은 화면 검증을 대체하지 않지만, Web App 로그를 수집하지 못하는 QM32C에서 앱 내부 상태를 확인하는 보조 증거입니다.
- 일시정지/재개 시 페이지와 슬롯 콘텐츠 타이머는 남은 시간 기준으로 재개해야 합니다. 전체 시간을 다시 예약하면 원본 seamless 재생 타이밍과 어긋납니다.
- TV Web App package id는 10자 규칙을 지키는 편이 안전합니다. NewHyOn Tizen Player는 `NewHyOnT01.Player` / `NewHyOnT01`을 사용합니다.

### 2026-06-28 전환 회귀 주의

- `2c8ed0bd`의 `boundaryPreparationProtected` 계열 변경은 정상 구조가 아닙니다. 스케줄 경계 준비를 이유로 `syncToPageElapsed()`, `handleVideoEnded()`, `prepareNextContent()`가 조기 return 하게 만들면 현재 컨텐츠 전환과 신규 명령 처리가 멈출 수 있습니다.
- `prepareUpcomingBoundaryFirstContent()`에서 경계 첫 콘텐츠가 영상이어도 미리 AVPlay prepare 하는 변경을 되살리지 않습니다. 경계 선준비는 이미지에만 적용하고, 영상은 실제 전환 시 AVPlay 절차대로 준비/재생합니다.

## 주요 파일

- `src/app/default-manifest.ts`: 기본 재생 데이터
- `src/app/settings-overlay.ts`: 리모컨 설정창
- `src/app/player-settings.ts`: 설정 저장/로드
- `src/app/runtime-diagnostics.ts`: HUD 플랫폼 API 진단
- `src/app/runtime-health-reporter.ts`: 실장비 런타임 헬스 스냅샷 기록
- `src/domain/page-plan.ts`: Windows 모델을 Tizen 재생 플랜으로 변환
- `src/player/avplay-session.ts`: AVPlay/AVPlayStore 세션 관리
- `src/player/audio-policy.ts`: Tizen 전역 오디오 mute 정책
- `src/player/slot-player.ts`: 슬롯별 이미지/영상 전환
- `scripts/package-clean.sh`: WGT clean 패키징
