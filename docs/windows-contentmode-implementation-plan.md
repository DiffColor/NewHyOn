# Windows Player ContentMode 구현 계획

## 문서 목적

이 문서는 현재 `Player/Windows/NewHyOn_Player` 코드 기준으로 다음 요구사항을 구현하기 위한 상세 계획과 체크리스트를 정의한다.

- 플레이어 설정 항목으로 `ContentMode`를 추가한다.
- `ContentMode`는 `Mode1`, `Mode2` 중 하나를 선택한다.
- `Mode1`은 영역별로 `MPV`만 사용하여 이미지와 영상을 하나의 재생 엔진에서 플레이리스트 방식으로 무중단 심리스 재생한다.
- `Mode2`는 영역별로 `MediaElement`와 `TransImageControl`을 사용하여 자연스러운 페이드 전환 중심의 심리스 재생을 한다.
- `Sync` 기능이 활성화된 경우 실제 동작 모드는 무조건 `Mode1`이다.
- `Mode1` 구현을 위해 레이아웃 모듈과 컨텐츠 재생 모듈을 분리하고, 하나의 전용 폴더 아래에 배치한다.
- 레이아웃 모듈은 레이아웃 간 무중단 심리스 전환 수단을 제공해야 한다.
- 컨텐츠 재생 모듈은 `MPV`만 사용하여 무중단 심리스 재생 수단을 제공해야 한다.

---

## 현재 코드 기준 핵심 진입점

### 현재 설정 저장/표시 경로

- `Player/Windows/SharedModels/PlayerModels.cs`
  - `LocalPlayerSettings`에 `SwitchTiming`, `IsSyncEnabled`, `IsLeading`, `SyncClientIps`가 저장된다.
  - 현재 `ContentMode`는 아직 없다.
- `Player/Windows/NewHyOn_Player/DataManager/LocalSettingsManager.cs`
  - 로컬 설정을 `LiteDB`에서 로드/저장한다.
- `Player/Windows/NewHyOn Player Settings/Models/ConfigPlayerSnapshot.cs`
  - 설정 앱이 편집하는 스냅샷 모델이다.
- `Player/Windows/NewHyOn Player Settings/Services/PlayerConfigurationService.cs`
  - 스냅샷과 실제 저장 모델 간 매핑을 담당한다.
- `Player/Windows/NewHyOn Player Settings/MainWindow.xaml`
  - 플레이어 옵션 UI와 동기화 옵션 UI가 있다.
- `Player/Windows/NewHyOn Player Settings/MainWindow.xaml.cs`
  - 설정 UI 로드, 저장, 동기화 UI enable/disable을 담당한다.

### 현재 재생/전환 경로

- `Player/Windows/NewHyOn_Player/MainWindow.xaml.cs`
  - 페이지 로드, 페이지 전환, 스케줄 전환, 동기화, 서브 윈도우 생성/재사용을 모두 담당한다.
- `Player/Windows/NewHyOn_Player/ContentsPlayWnd/ContentsPlayWindow.xaml.cs`
  - 현재는 하나의 윈도우가 `MPVLibControl`과 `TransImageControl`을 함께 들고 있고, 컨텐츠 순환/타이머/재생 타입 분기를 모두 담당한다.
- `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml.cs`
  - 현재 `MPV` 래퍼이며 `Load`, `Play`, `Stop`, `Playlist` 관련 기본 기능은 있으나 gapless 재생 제어를 위한 오케스트레이션 계층은 없다.
- `Player/Windows/NewHyOn_Player/ContentControls/TransImageControl.xaml.cs`
  - 현재 이미지 전환 효과를 담당한다.
- `Player/Windows/NewHyOn_Player/ContentControls/MEDisplayElement.xaml(.cs)`
  - 기존 `MediaElement` 기반 재생 구조가 남아 있으나 현재 메인 경로로 쓰이지 않는다.
- `Player/Windows/NewHyOn_Player/Services/UdpSyncService.cs`
  - `Prepare`, `Commit` 기반 컨텐츠 동기화 메시지를 처리한다.

---

## 현재 구조의 문제 요약

### 1. 설정 레벨 문제

- `ContentMode`가 없어 재생 엔진 선택이 불가능하다.
- `Sync` 활성화 시 모드를 강제하는 규칙이 저장/표시/UI에 없다.

### 2. 구조 분리 문제

- 현재 `MainWindow`가 페이지, 레이아웃, 스케줄, 동기화, 타이머, 윈도우 풀 관리까지 모두 들고 있다.
- 현재 `ContentsPlayWindow`가 레이아웃 배치의 일부와 컨텐츠 재생 로직을 동시에 품고 있다.
- 결과적으로 `Mode1`과 `Mode2`를 깔끔하게 병행할 수 있는 엔진 경계가 없다.

### 3. 레이아웃 전환 문제

- 현재 페이지 전환은 `HideAllContentsPlayWindow()` 후 새 페이지를 다시 구성하는 방식이다.
- 즉, 레이아웃 준비와 활성화가 분리되지 않아 페이지 단위 완전 심리스 구조가 아니다.

### 4. 컨텐츠 전환 문제

- 현재 재생은 `MPV + TransImageControl` 혼합 구조다.
- 이미지는 페이드가 되지만, 영상-영상 gapless 구조와 페이지 간 오프스크린 준비 구조는 없다.
- 영역별 재생 방식이 하나의 윈도우 클래스 안에 고정되어 있어 `Mode1`과 `Mode2`의 동시 지원이 어렵다.

### 5. Sync 적용 문제

- 현재 Sync는 컨텐츠 인덱스 기준의 `Prepare/Commit` 구조를 이미 갖고 있으나, 엔진 레벨에서 `Mode1` 전용으로 강제되는 구조가 아니다.
- 앞으로 `Mode2`가 추가되면 Sync 중 `Mode2`가 선택되어도 내부적으로 `Mode1`만 사용하도록 강제해야 한다.

---

## 목표 동작 정의

## 1. ContentMode

### 설정 값

- `Mode1`
- `Mode2`

### 실제 적용 규칙

- `IsSyncEnabled == false`
  - 저장된 `ContentMode`를 그대로 사용한다.
- `IsSyncEnabled == true`
  - 저장값과 관계없이 실제 동작 모드는 무조건 `Mode1`이다.
  - 설정 UI에는 저장값은 유지하되, 실행 중 유효 모드는 `Mode1`임을 명시한다.

### 기본값 제안

- 기존 플레이어의 현재 재생 체감과 저장 호환성을 고려해 기본 저장값은 `Mode2`를 권장한다.
- 단, 신규 설치 정책을 `Mode1`로 바꾸고 싶다면 본 문서의 체크리스트에서 기본값만 조정하면 된다.

## 2. Mode1 목표

- 영역별 재생 엔진은 `MPV` 하나만 사용한다.
- 이미지와 영상 모두 `MPV` 플레이리스트 항목으로 취급한다.
- 각 영역은 하나의 재생 엔진에서 playlist advance 방식으로 다음 항목으로 넘어간다.
- 컨텐츠 전환 시 stop-hide-load-play 패턴이 아니라, 미리 구성된 playlist 기반 전환으로 gapless를 목표로 한다.
- 페이지 단위 레이아웃 전환은 오프스크린에서 다음 레이아웃을 완전히 준비한 뒤 활성 레이아웃과 교체한다.
- Sync 활성화 시 `Mode1`의 컨텐츠 인덱스/준비 상태를 기준으로만 동작한다.

## 3. Mode2 목표

- 영역별로 `MediaElement` 계열과 `TransImageControl`을 사용한다.
- 이미지와 영상 전환은 opacity/transition 기반으로 자연스럽게 이어진다.
- `Mode1`처럼 완전한 gapless를 보장하는 모드는 아니며, 시각적으로 자연스러운 페이드 심리감을 목표로 한다.
- 페이지 전환은 `Mode1`과 동일한 레이아웃 모듈을 사용하되, 영역 내부 컨텐츠 엔진만 `Mode2` 구현체를 사용한다.

---

## 권장 구현 폴더 구조

`Mode1` 구현 요구사항에 맞춰, 레이아웃 모듈과 컨텐츠 재생 모듈을 한 폴더 아래에 모은다.

권장 폴더:

- `Player/Windows/NewHyOn_Player/PlaybackModes`

권장 파일 구조:

- `Player/Windows/NewHyOn_Player/PlaybackModes/ContentModeType.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackModeResolver.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackCoordinator.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/LayoutSceneRuntime.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/LayoutSceneSlot.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessLayoutEngine.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/IContentPlaybackEngine.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/ContentPlaybackContext.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/Mode1MpvPlaybackEngine.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/Mode1PlaylistBuilder.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/Mode1RegionRuntime.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/Mode2FadePlaybackEngine.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/Mode2RegionRuntime.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/Mode2TransitionDirector.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackContracts.cs`

중요 원칙:

- 기존 `MainWindow`는 최종적으로 orchestration 진입점만 남기고, 레이아웃/재생 로직의 대부분을 `PlaybackModes` 폴더 아래로 이동한다.
- 기존 `ContentsPlayWindow`는 최종적으로 직접 재생 로직을 가지지 않거나, 새 엔진이 사용하는 view host 수준으로 축소한다.
- 새 구조가 완성되면 기존 방식과 병렬 유지하지 않고, `ContentMode` 기반 진입점으로 정리한다.

---

## 제안 아키텍처

## 1. 상위 오케스트레이션 계층

### `PlaybackCoordinator`

역할:

- 현재 유효 모드 계산
- `Sync` 상태를 고려한 강제 `Mode1` 적용
- 페이지/스케줄 전환 요청을 레이아웃 엔진으로 전달
- 타이머 tick과 sync prepare/commit을 하위 엔진에 전달
- 현재 활성 scene, 준비 scene, 다음 scene 정보를 관리

계산 규칙:

- 저장된 `ContentMode` 조회
- `IsSyncEnabled == true`면 `Mode1` 강제
- `Sync` UI/로그/디버그 표시에도 유효 모드 반영

## 2. 레이아웃 모듈

### `SeamlessLayoutEngine`

역할:

- 페이지 전체를 scene 단위로 다룬다.
- 최소 2개의 layout scene을 유지한다.
  - `ActiveScene`
  - `PreparedScene`
- 다음 페이지는 항상 `PreparedScene`에서 오프스크린 준비를 끝낸다.
- 모든 영역 런타임이 준비 완료 상태가 된 뒤에만 scene activation을 허용한다.
- scene activation 시 현재 scene을 바로 destroy하지 않고 inactive 상태로 내린 뒤 다음 준비용으로 재사용한다.

권장 scene 구조:

- scene 당 고정 미디어 영역 슬롯 풀
- scene 당 고정 스크롤 텍스트 슬롯 풀
- scene 당 고정 웰컴보드 슬롯 풀
- scene 단위 z-order, opacity, visibility, topmost 제어

핵심 목표:

- 현재 `HideAllContentsPlayWindow()` 후 rebuild 방식 제거
- 다음 페이지 레이아웃을 준비 완료 전까지 화면에 노출하지 않음
- 활성화는 scene 교체로 끝내고, 구성은 오프스크린에서만 수행

## 3. 컨텐츠 재생 모듈

### 공통 인터페이스 `IContentPlaybackEngine`

필수 역할:

- 영역 초기화
- 영역별 playlist/sequence 준비
- prepare 완료 신호
- activation 시점 시작
- tick 처리
- next item 준비
- sync prepare/commit 적용
- dispose/clear

### `Mode1MpvPlaybackEngine`

핵심 원칙:

- 영역당 재생 엔진은 `MPV` 하나만 사용한다.
- 이미지와 영상을 모두 `MPV` 재생 아이템으로 취급한다.
- 이미지 재생 시간은 `image-display-duration`과 playlist metadata를 사용한다.
- `Load -> Stop -> Hide -> Show`가 아니라 playlist prebuild 후 advance 한다.
- 다음 아이템/다음 페이지 첫 아이템 준비 상태를 엔진이 직접 관리한다.

필요한 확장 포인트:

- `MPVLibControl`에 playlist replace, preload, 현재 인덱스 조회, 파일 준비 상태 조회, 첫 프레임 준비 신호, playlist item changed 신호가 필요하다.
- 필요한 경우 `Mpv.NET` 래퍼에도 property observe 기능을 추가한다.
- mode1 전용 playlist builder는 `ContentsInfoClass` 리스트를 `mpv playable item` 리스트로 바꾼다.

### `Mode2FadePlaybackEngine`

핵심 원칙:

- 영역당 `MediaElement` 기반 video surface와 `TransImageControl` 기반 image surface를 조합한다.
- 자연스러운 페이드 전환을 위해 opacity transition director를 둔다.
- 영상-영상 전환까지 안정적으로 페이드를 하려면 단일 `MediaElement`로는 부족할 수 있으므로, 실제 구현 시 두 개의 `MediaElement`를 교대 사용하는 구조를 우선 검토한다.
- 사용자 요구의 핵심은 `MediaElement + TransImageControl` 조합이므로, 이미지 surface는 반드시 `TransImageControl`을 사용한다.

---

## 구현 단계 계획

## Phase 1. 설정 계약 추가

목표:

- `ContentMode`를 저장하고 설정 앱에서 편집 가능하게 만든다.
- `Sync` 활성화 시 실제 모드가 강제로 `Mode1`이라는 규칙을 UI와 런타임에 반영한다.

작업:

- `LocalPlayerSettings`에 `ContentMode` 속성 추가
- `ConfigPlayerSnapshot`에 `ContentMode` 속성 추가
- `PlayerConfigurationService.Load/Save` 매핑 추가
- 설정 앱 `MainWindow.xaml`에 `ContentMode` 선택 UI 추가
- 설정 앱 `MainWindow.xaml.cs`에 options 바인딩 및 snapshot 반영 추가
- `SyncEnabledCheckBox` 상태에 따라 유효 모드 안내 문구 추가

## Phase 2. 재생 엔트리포인트 분리

목표:

- `MainWindow`에서 직접 재생 로직을 돌리지 않고 `PlaybackCoordinator`를 통해 진입하게 만든다.

작업:

- `PlaybackModes` 폴더 생성
- `ContentModeType`, `PlaybackModeResolver`, `PlaybackCoordinator` 추가
- `MainWindow_Loaded`, `PopPage`, `TickTask`, `ApplySyncIndex` 진입부를 coordinator로 이관
- 기존 `MainWindow`의 상태 필드 중 재생 엔진 관련 상태를 coordinator/engine 계층으로 이동

## Phase 3. 레이아웃 모듈 구현

목표:

- 페이지 전환을 scene prepare/activate 구조로 바꾼다.

작업:

- `SeamlessLayoutEngine` 구현
- `LayoutSceneRuntime` 2세트 이상 생성
- 각 scene이 고정 window slot 풀을 보유하도록 구성
- 현재 page data로 inactive scene을 준비하는 API 구현
- 모든 slot 준비 완료 후 activate 하는 API 구현
- scene activation 전에는 active scene 유지
- activation 이후 old scene을 recycle 상태로 되돌림

## Phase 4. Mode1 컨텐츠 엔진 구현

목표:

- 영역별 `MPV` playlist 기반 gapless 재생 구현
- `Sync`와 동일 기준으로 동작 가능한 엔진 만들기

작업:

- `Mode1PlaylistBuilder` 구현
- `ContentsInfoClass` 리스트를 `MPV` playlist item으로 변환
- 이미지 duration을 `MPV` 설정으로 주입
- `Mode1MpvPlaybackEngine`이 영역별 runtime 생성/준비/시작/정지 처리
- 다음 아이템 advance, 다음 scene 첫 아이템 preload 처리
- sync prepare/commit 시 인덱스와 page activation 시점을 일관되게 적용

## Phase 5. Mode2 컨텐츠 엔진 구현

목표:

- `MediaElement`/`TransImageControl` 기반 자연스러운 페이드 재생 엔진 구현

작업:

- `Mode2FadePlaybackEngine` 구현
- 영역별 surface 구성 정리
- 이미지, 영상, 이미지->영상, 영상->이미지, 영상->영상 전환 케이스별 transition director 구현
- 기존 `MEDisplayElement` 재사용 여부 결정
- 재사용하지 않는 경우 `PlaybackModes` 폴더 내 mode2 전용 runtime으로 재정리

## Phase 6. Sync 강제 Mode1 연결

목표:

- `Sync` 활성화 시 런타임이 무조건 `Mode1`을 사용하도록 강제한다.

작업:

- `PlaybackModeResolver`에서 유효 모드 계산 구현
- `MainWindow`와 debug/log에 실제 유효 모드 출력
- Sync 중 `Mode2` 선택 저장값이 있어도 `Mode1` runtime만 생성
- Sync prepare/commit이 `Mode1MpvPlaybackEngine`만 대상으로 호출되도록 정리

## Phase 7. 기존 재생 경로 제거 및 정리

목표:

- `ContentsPlayWindow` 중심의 구형 재생 로직 의존성을 제거한다.

작업:

- `MainWindow`의 직접 재생 로직 제거
- `ContentsPlayWindow`의 재생/타이머/전환 역할 제거 또는 host 역할만 남김
- `HideAllContentsPlayWindow()` 기반 전환 제거
- 새 구조 기준으로 불필요해진 상태값과 메서드 삭제

## Phase 8. 검증 및 안정화

목표:

- 실제 사용 시나리오 기준으로 동작을 검증한다.

작업:

- 단일 플레이어 `Mode1`, `Mode2` 검증
- `Sync on + leader`, `Sync on + follower` 검증
- 페이지 전환, 스케줄 전환, 리로드 전환 검증
- 누락 파일, 0바이트 파일, 기간 제한 컨텐츠 검증
- 메모리/핸들 누수와 윈도우 잔상 검증

---

## 구현 세부 결정 사항

## 1. `ContentMode` 저장 타입

권장:

- 문자열 저장
- 허용값: `Mode1`, `Mode2`

이유:

- 현재 `SwitchTiming`과 동일한 저장 패턴을 유지하기 쉽다.
- LiteDB 기존 데이터와 호환성이 단순하다.
- 설정 앱 바인딩이 간단하다.

## 2. Sync 중 유효 모드 표기

권장:

- 저장값은 유지한다.
- 실제 실행 모드는 `EffectiveContentMode`로 별도 계산한다.
- 설정 UI 하단 또는 Sync 영역에 다음 문구를 노출한다.
  - `동기화가 활성화되어 실제 재생 모드는 Mode1로 강제됩니다.`

## 3. Layout Scene 개수

권장:

- 우선 2개 scene으로 시작
  - `SceneA`
  - `SceneB`

이유:

- 현재 요구사항의 핵심은 페이지 간 seamless 준비/전환이다.
- 일반 페이지/다음 페이지 prepare 용도로는 2개면 충분하다.
- 특별 스케줄 선준비 요구가 Windows에도 확대되면 3번째 scene이 필요할 수 있으나, 현재 사용자 요구 범위에서는 2개로 먼저 정리하는 것이 안전하다.

## 4. Mode1 영역 재생 기준

권장:

- 영역별 `MPVLibControl` 1개를 scene마다 1세트 둔다.
- scene prepare 단계에서 playlist 전체를 구성한다.
- activation 직전 첫 항목 ready 여부를 확인한다.
- active scene에서는 `MPV` playlist advance만 사용한다.

## 5. Mode2 영역 재생 기준

권장:

- 영역별 `TransImageControl` 1개 + `MediaElement` surface 1~2개 사용
- 자연스러운 전환이 목표이므로 opacity animation을 적극 사용
- `Mode2`는 시각 품질 우선, `Mode1`은 gapless/Sync 우선으로 구분

---

## 상세 체크리스트

## A. 사전 분석

- [ ] `MainWindow.xaml.cs`에서 페이지 전환, tick, sync, playlist reload, schedule switch 호출 경로를 모두 표로 정리한다.
- [ ] `ContentsPlayWindow.xaml.cs`에서 현재 이미지/영상 재생 분기와 타이머 흐름을 메서드 단위로 분해한다.
- [ ] `MPVLibControl.xaml.cs`의 현재 playlist 지원 범위를 점검한다.
- [ ] `TransImageControl.xaml.cs`의 현재 fade 전환 사용 지점을 목록화한다.
- [ ] `MEDisplayElement.xaml.cs`, `WMKitMediaElement.xaml.cs` 중 `Mode2` 후보를 결정한다.
- [ ] `Sync` 관련 상태값이 `MainWindow` 어디에 분산돼 있는지 목록화한다.

## B. 설정 모델 추가

- [ ] `LocalPlayerSettings`에 `ContentMode` 추가
- [ ] 기본값 결정 및 코드 반영
- [ ] 설정 로드 시 null/empty 방어값 반영
- [ ] `ConfigPlayerSnapshot`에 `ContentMode` 추가
- [ ] 설정 저장 서비스에 `ContentMode` load/save 매핑 추가
- [ ] 기존 저장 데이터와 호환되도록 migration 경로 확인

## C. 설정 UI 추가

- [ ] `MainWindow.xaml` 플레이어 옵션 카드에 `ContentMode` 선택 UI 배치
- [ ] `Mode1`, `Mode2` 선택 옵션 바인딩 추가
- [ ] `BuildSnapshot()`에 `ContentMode` 반영
- [ ] `LoadSnapshot()`에 `ContentMode` 반영
- [ ] `SyncEnabledCheckBox` 상태에 따라 유효 모드 안내 텍스트 추가
- [ ] `IsLeadingCheckBox`와 함께 UI enable/disable 동작 검토

## D. 모드 해석 계층 추가

- [ ] `ContentModeType` 정의
- [ ] 문자열 <-> enum 변환 유틸 추가
- [ ] `PlaybackModeResolver` 구현
- [ ] `IsSyncEnabled`면 `Mode1` 강제 로직 구현
- [ ] `MainWindow` 디버그 표시용 `EffectiveContentMode` 제공
- [ ] 로그에 저장 모드와 실제 유효 모드를 함께 남기기

## E. 레이아웃 엔진 설계

- [ ] `SeamlessLayoutEngine` 클래스 생성
- [ ] scene runtime 구조 설계
- [ ] scene당 미디어/스크롤/웰컴 슬롯 풀 구조 설계
- [ ] 각 scene 준비 상태 플래그 설계
- [ ] active/prepared scene 교체 상태 머신 정의
- [ ] scene 준비 완료 전 activation 차단 로직 설계

## F. 레이아웃 준비 구현

- [ ] page definition으로부터 scene 배치 정보 생성
- [ ] inactive scene에 새 페이지 요소 바인딩 구현
- [ ] 스케일 계산을 scene 단위로 이동
- [ ] window 위치/크기 반영을 scene 단위 API로 이동
- [ ] z-order 재정렬을 scene activation 단계로 이동
- [ ] 현재 active scene 유지 상태에서 prepare가 가능하도록 정리

## G. 레이아웃 전환 구현

- [ ] 새 scene 완전 준비 완료 신호 정의
- [ ] activation 시점에 현재 active scene은 그대로 유지되도록 구현
- [ ] next scene activate 후 old scene deactivate/recycle 구현
- [ ] hide-all 후 rebuild 코드 제거
- [ ] 페이지 전환, 스케줄 전환, playlist reload 전환이 모두 같은 scene switch API를 타도록 정리

## H. Mode1 MPV 엔진 구현

- [ ] `Mode1PlaylistBuilder` 생성
- [ ] 이미지 항목을 `MPV` playlist item으로 표현하는 규칙 확정
- [ ] 영상 항목을 `MPV` playlist item으로 표현하는 규칙 확정
- [ ] play duration 계산과 `image-display-duration` 적용 정책 정의
- [ ] `MPVLibControl` 확장 포인트 설계
- [ ] playlist replace API 추가
- [ ] current index / next index 조회 API 추가
- [ ] first frame ready / file loaded / item switched 이벤트 추가
- [ ] mode1 runtime이 오프스크린에서 first item ready까지 준비하도록 구현
- [ ] mode1 runtime이 active scene에서 gapless advance하도록 구현

## I. Mode1 Sync 연결

- [ ] sync prepare 시 다음 컨텐츠 인덱스를 `Mode1` 엔진 기준으로 계산
- [ ] sync commit 시 해당 인덱스를 정확히 적용
- [ ] follower가 prepare 미수신 상태에서 임의 advance하지 않도록 막기
- [ ] leader의 next index와 실제 `MPV` playlist 상태가 어긋나지 않도록 검증
- [ ] scene activation과 sync commit의 순서를 명확히 정의

## J. Mode2 Fade 엔진 구현

- [ ] `Mode2FadePlaybackEngine` 생성
- [ ] `TransImageControl`과 `MediaElement` surface 조합 정의
- [ ] 이미지 재생 경로 구현
- [ ] 영상 재생 경로 구현
- [ ] 이미지->이미지 페이드 구현
- [ ] 이미지->영상 페이드 구현
- [ ] 영상->이미지 페이드 구현
- [ ] 영상->영상 페이드 구현 전략 확정
- [ ] outgoing surface와 incoming surface 정리 타이밍 정의

## K. 기존 클래스 정리

- [ ] `ContentsPlayWindow`의 책임을 host/runtime 수준으로 축소할지 결정
- [ ] `MainWindow`에서 재생 타이머/컨텐츠 순환 책임 제거
- [ ] 새 engine 진입점으로 치환 후 사용하지 않는 필드 제거
- [ ] `HideAllContentsPlayWindow`, `OrderingCanvasBGContents` 중심 흐름 정리
- [ ] dead code가 된 `MEDisplayElement`, `WMKitMediaElement` 처리 방향 확정

## L. 스케줄/리로드 연결

- [ ] `TryApplyScheduledSwitch()`가 scene prepare/activate 기반으로 바뀌도록 정리
- [ ] `TryApplyPendingPlaylistReload()`가 동일 엔진을 사용하도록 정리
- [ ] `ApplyScheduleTransition()`의 역할을 새 레이아웃 전환 모델 기준으로 재정의
- [ ] page boundary, content boundary, immediate 정책이 새 엔진에서도 동일하게 동작하는지 검토

## M. 예외/데이터 검증

- [ ] 누락 파일 건너뛰기 정책을 mode1/mode2 공통 계층으로 이동
- [ ] 0바이트 파일 처리 유지
- [ ] 기간 제한 컨텐츠 검사 유지
- [ ] playlist 전체가 비어 있을 때 scene activation 차단
- [ ] fallback 없이도 다음 유효 컨텐츠/페이지를 찾는 규칙 명확화

## N. 검증 체크리스트

- [ ] `Mode1`, `Sync off`, 단일 이미지 playlist 검증
- [ ] `Mode1`, `Sync off`, 이미지+영상 혼합 playlist 검증
- [ ] `Mode1`, `Sync on`, leader/follower 혼합 playlist 검증
- [ ] `Mode1`, page change 시 레이아웃 무중단 전환 검증
- [ ] `Mode2`, 이미지->이미지 페이드 검증
- [ ] `Mode2`, 이미지->영상 페이드 검증
- [ ] `Mode2`, 영상->이미지 페이드 검증
- [ ] `Mode2`, 영상->영상 전환 검증
- [ ] 스케줄 전환 + content mode 조합 검증
- [ ] playlist reload + content mode 조합 검증
- [ ] 앱 재시작 후 `ContentMode` 보존 검증
- [ ] `Sync enabled` 상태에서 저장값이 `Mode2`여도 실제 `Mode1` 동작 검증

## O. 완료 기준

- [ ] 설정 앱에서 `ContentMode`를 저장/로드할 수 있다.
- [ ] 실제 플레이어는 `EffectiveContentMode`를 계산해 엔진을 선택한다.
- [ ] `Sync`가 켜지면 무조건 `Mode1` 엔진만 사용한다.
- [ ] 레이아웃 전환은 active scene을 유지한 채 prepared scene activate로 수행된다.
- [ ] `Mode1`은 영역별 `MPV` playlist 기반으로 stop-hide-load-play 구조를 제거한다.
- [ ] `Mode2`는 `MediaElement`/`TransImageControl` 기반 자연스러운 페이드 전환을 제공한다.
- [ ] 기존 `MainWindow`/`ContentsPlayWindow`의 과도한 책임이 분리된다.
- [ ] 최종 진입점은 `ContentMode` 기반 새 구조로 정리된다.

---

## 우선 구현 순서 제안

1. 설정 모델과 UI에 `ContentMode` 추가
2. `PlaybackModes` 폴더 및 모드 해석 계층 추가
3. `MainWindow`에서 coordinator 호출 구조 먼저 세움
4. 레이아웃 엔진부터 분리
5. `Mode1` 구현
6. Sync를 `Mode1`에 강제 연결
7. `Mode2` 구현
8. 기존 구조 정리 및 제거
9. 전체 통합 검증

---

## 리스크 메모

- `Mode1`의 진짜 gapless 품질은 `MPVLibControl`과 내부 `Mpv.NET` 래퍼에서 어느 수준까지 property/event를 노출할 수 있는지에 크게 좌우된다.
- `Mode2`의 영상-영상 페이드는 단일 `MediaElement`만으로는 한계가 있어 실제 구현 시 surface 2면 교대가 필요할 가능성이 높다.
- 현재 `MainWindow`가 너무 많은 상태를 직접 소유하고 있으므로, 중간 단계에서 책임 분리를 서두르지 않으면 구조가 다시 엉킬 수 있다.
- 레이아웃 심리스와 컨텐츠 심리스를 분리해서 구현하지 않으면 문제 원인 추적이 어려워진다.

