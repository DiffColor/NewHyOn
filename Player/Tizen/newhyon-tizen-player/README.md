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

## Tizen 하드웨어 제약

다음 제약은 코드 작성 시 반드시 지켜야 합니다.

- `index.html`은 `$WEBAPIS/webapis/webapis.js`를 앱 모듈보다 먼저 로드해야 합니다. 이 스크립트가 빠지면 `webapis.avplay`/`webapis.avplaystore`를 안정적으로 사용할 수 없습니다.
- `webapis.avplaystore.getPlayer()`는 병렬 pre-buffering용 플레이어를 생성하며 동시에 최대 4개까지입니다.
- 제한 초과 시 `QUOTA_EXCEEDED_ERR`와 `Max player count reached`가 발생합니다.
- 앱 시작 시 영상 슬롯 수만큼 AVPlayStore 플레이어를 미리 만들면 QM32C에서 즉시 실패합니다.
- 현재 구현은 `AvplaySessionPool.acquire()`에서 영상 재생이 실제로 필요한 순간에만 세션을 지연 할당합니다.
- AVPlayStore 한도 초과가 예상되면 추가 `getPlayer()`를 호출하지 않습니다. 초과 영상 슬롯은 앱 전체 오류로 전파하지 않고 HUD 슬롯 상태에 `ERROR`로 남깁니다.
- 이미지 슬롯, 설정창, HUD 렌더링, snapshot, stop 처리에서는 AVPlayStore 세션을 새로 만들면 안 됩니다.
- Tizen AVPlay는 상대 경로를 직접 열 수 없습니다. 패키지 로컬 파일은 Tizen filesystem으로 절대 경로로 변환해야 합니다.
- AVPlay 표시 영역은 CSS만으로 끝나지 않습니다. `setDisplayRect()`를 1920x1080 기준 좌표로 다시 매핑해야 합니다.
- 영상 비율 유지 설정은 AVPlay에도 직접 적용해야 합니다. `화면 비율 유지=ON`은 `PLAYER_DISPLAY_MODE_LETTER_BOX`, `OFF`는 `PLAYER_DISPLAY_MODE_FULL_SCREEN`을 사용합니다.
- Tizen TV 오디오는 `tizen.tvaudiocontrol` 전역 제어입니다. Windows처럼 슬롯별 mute를 완전히 분리할 수 없어서 페이지에 unmuted 영상 슬롯이 하나라도 있으면 TV mute를 해제하고, 없으면 mute 처리합니다. 앱 정지/종료 시 시작 전 mute 상태를 복원합니다.
- 런타임 헬스 스냅샷을 남기려면 `filesystem.write` 권한이 필요합니다. 이 파일은 화면 검증을 대체하지 않지만, Web App 로그를 수집하지 못하는 QM32C에서 앱 내부 상태를 확인하는 보조 증거입니다.
- 일시정지/재개 시 페이지와 슬롯 콘텐츠 타이머는 남은 시간 기준으로 재개해야 합니다. 전체 시간을 다시 예약하면 원본 seamless 재생 타이밍과 어긋납니다.
- TV Web App package id는 10자 규칙을 지키는 편이 안전합니다. NewHyOn Tizen Player는 `NewHyOnT01.Player` / `NewHyOnT01`을 사용합니다.

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
