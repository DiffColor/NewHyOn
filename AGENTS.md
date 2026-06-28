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
  --proj-dir=/Users/jazzlife/Documents/Workspaces/Products/NewHyOn/Player/Tizen/newhyon-tizen-player/Player \
  --serial=192.168.50.180:26101 \
  --debug-mode
```

`--proj-dir`는 repo 루트가 아니라 실제 Tizen 웹 프로젝트인 `Player/Tizen/newhyon-tizen-player/Player`여야 한다. repo 루트를 주면 `not a valid project`로 실패한다.

현재 QM32C에서는 위 기본 명령만으로는 `NewHyOnT01.Player` uninstall 단계에서 실패한다. 디버거를 실제로 붙일 때는 빌드된 WGT를 `--package-path`로 같이 넘겨야 한다.

```bash
/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/tizen-core/tz run-chain \
  --proj-dir=/Users/jazzlife/Documents/Workspaces/Products/NewHyOn/Player/Tizen/newhyon-tizen-player/Player \
  --package-path=/Users/jazzlife/Documents/Workspaces/Products/NewHyOn/Player/Tizen/newhyon-tizen-player/Build/NewHyOnTizenPlayer.wgt \
  --serial=192.168.50.180:26101 \
  --debug-mode
```

성공 기준은 `successfully launched ... with debug 1 port: <port>`이다. 이후 다음처럼 포워딩과 Web Inspector 타겟을 확인한다.

```bash
/Users/jazzlife/.tizen-extension-platform/server/sdktools/data/tools/sdb -s 192.168.50.180:26101 forward tcp:<port> tcp:<port>
curl http://127.0.0.1:<port>/json/list
```

### 2026-06-28 확인된 실패 원인

- `tz run-chain --debug-mode`는 현재 `uninstall NewHyOnT01.Player` 단계에서 `uninstall failed[132]`로 실패한다.
- `tz run-chain --proj-dir=.../Player --package-id=NewHyOnT01 --debug-mode`도 같은 `NewHyOnT01.Player` uninstall 단계에서 실패한다. `--package-id`만 추가하는 시도는 반복하지 않는다.
- 반면 실제 배포에 쓰는 `tz install -p Build/NewHyOnTizenPlayer.wgt -e 192.168.50.180:26101`는 `NewHyOnT01.NewHyOnTizenPlayer`를 uninstall/install 대상으로 처리하며 성공한다.
- `tz run-chain --proj-dir=.../Player --package-path=.../Build/NewHyOnTizenPlayer.wgt --serial=192.168.50.180:26101 --debug-mode`는 `NewHyOnT01.NewHyOnTizenPlayer`를 대상으로 uninstall/install 후 `debug 1 port: 36009`로 성공했다.
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

### 2026-06-28 플레이리스트 상태 확인 기록

- Web Inspector 포트 `36009`로 직접 확인한 현재 플레이어 상태는 앱 오류나 업데이트 실패가 아니라 `playing / 단일 / No active content`이다.
- `#status-update`는 `대기`, 통신은 `DB: 연결됨 / SignalR: 연결됨 / FTP: 연결됨 / Heartbeat: sent`, 인증은 `LicenseHub: authenticated`였다.
- 저장된 `newhyon-tizen-player:remote-manifest`의 플레이리스트 `단일`에는 영상 2개가 있고 `CIF_FileExist=true`였다.
- 두 콘텐츠 GUID의 기간 캐시가 모두 현재일 `2026-06-28` 기준 만료였다.
  - `a42b06ed-212d-47e2-8769-4a7108f68d29`: `2026-05-07` 종료
  - `3039db8c-2c4f-475b-8a99-c41e43e2b6f6`: `2026-06-27` 종료
- 따라서 플레이리스트 변경 실패가 아니라 `content-period.ts`의 `isContentPeriodAllowed()`가 두 콘텐츠를 제외하여 `createBlackoutPagePlans()`로 진입한 상태다.

### 2026-06-28 Tizen AVPlay 전환 구조 기록

- AVPlayStore는 총 4개 `getPlayer()`까지만 허용한다.
- NewHyOn Tizen Player는 AVPlay 플레이어 2개를 한 페어로 사용한다. 페어 안의 2개는 현재 영상 lane과 다음 영상 준비 lane이다.
- 스케줄-스케줄 전환 때문에 앱 시작 시 이 페어를 2세트 선확보해야 한다. 즉 `2 players x 2 pairs = 4 players`가 의도한 구조다.
- `pool`/`lease`로 AVPlay 세션을 빌려주고 반납하는 구조를 다시 만들지 않는다.
- 페이지-페이지 연결과 컨텐츠-컨텐츠 연결은 논리적 구분일 뿐 실제로는 각 슬롯의 `이미지->영상`, `영상->이미지`, `영상->영상`, `이미지->이미지` 콘텐츠 전환이다.
- 페이지 전환을 이유로 새 표면을 만들거나 `surface swap`/`detach surface` 경로를 추가하지 않는다.
- `src/app/newhyon-player-app.ts`는 고정 slot player를 만들고 `SlotPlayer.switchToSlotPlan()`으로 콘텐츠 계획만 바꾼다.
- `src/player/avplay-session.ts`의 `createAvplaySessionPair()`는 한 페어마다 정확히 AVPlayStore 플레이어 2개만 만든다.
- `SlotPlayer`의 컨텐츠 전환 상태(`preparedItemId`, `preparedImageId`, `preparedBoundaryImageId`)는 기존 구조처럼 `item.id` 기준을 유지한다. 여기까지 실제 소스 키 기준으로 바꾸면 페이지/컨텐츠 전환 구조가 깨진다.
- 실제 재생 소스 키(`contentType + sourceUrl`) 기준 합류는 `AvplaySession` 내부의 in-flight `prepareAsync` 직렬화 범위에만 둔다. 같은 파일이 다른 페이지/명령 id로 들어올 수 있어 AVPlay lane에 중복 `open()`을 넣지 않기 위한 장치이지, `SlotPlayer`의 논리 전환 키가 아니다.
- AVPlay 준비 절차는 레퍼런스 샘플 `Player/Tizen/avplay-seamless-still-mode alias` 기준을 따른다. player 하나에 대해 `open -> setListener -> setDisplayRect -> setDisplayMethod -> prepareAsync 완료 -> play` 순서를 깨지 않는다.
- `prepareAsync`가 진행 중인 lane에는 `stop()`, `close()`, 다른 `open()`을 끼워 넣지 않는다. `clearPrepared()`는 슬롯 메타 정리 요청이지 AVPlay 준비 작업 취소 명령이 아니므로 in-flight prepare를 폐기하지 않는다.
- 다른 소스를 준비해야 하면 진행 중인 `prepareAsync` 완료를 기다린 뒤 기존 준비 lane을 `stop/close`하고 새 소스를 `open/prepareAsync`한다.

### 2026-06-28 `2c8ed0bd` 원인 정리

- `2c8ed0bd`는 정상 기준점이 아니라 망가진 기준점이다. 이 커밋의 `boundaryPreparationProtected` 계열 변경을 되살리지 않는다.
- 문제 핵심은 스케줄 경계 첫 콘텐츠 준비를 `SlotPlayer`의 일반 컨텐츠 전환 루프 안에 보호 플래그로 끼워 넣은 것이다. 이로 인해 `syncToPageElapsed()`, `handleVideoEnded()`, `prepareNextContent()`가 조기 return 하며 현재 컨텐츠 전환과 신규 명령 처리를 막을 수 있다.
- `prepareUpcomingBoundaryFirstContent()`에서 경계 첫 콘텐츠가 영상이어도 미리 AVPlay prepare 하도록 바꾼 것도 되살리지 않는다. 현재 구조에서는 이미지 경계 준비만 선행하고, 영상은 실제 전환 시 AVPlay 절차대로 준비/재생한다.
- 다시 고칠 때는 `SlotPlayer` 전환 구조를 건드리기 전에 현재 코드와 이 기록을 먼저 확인한다. 컨텐츠 전환은 `이미지->영상`, `영상->이미지`, `영상->영상`, `이미지->이미지` 네 가지 실제 전환만 남겨야 한다.
