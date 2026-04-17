# Windows Player MPV Seamless Playback 1차 적용 방안

## 문서 목적

이 문서는 현재 `Player/Windows/NewHyOn_Player` 코드 기준으로 NewHyOn Player를 다음 목표 구조로 개편하기 위한 **상용 서비스 가능 수준의 1차 구현 범위**를 확정한다.

중요 원칙은 다음과 같다.

- 이번 1차 구현은 연구용 프로토타입이나 부분 시범 적용이 아니다.
- 이번 1차 구현만으로도 실제 현장에 바로 배포 가능한 완성형 Windows Player여야 한다.
- 따라서 구조 개선과 함께 1차 서비스 범위에 남는 상용 기능을 안정적으로 유지해야 하며, 운영에 필요한 안정성·관측성·복구 가능성까지 포함해야 한다.
- 재생 구조를 `재생 컨테이너`, `레이아웃`, `컨텐츠` 3개 모듈로 재구성한다.
- 재생 컨테이너는 플레이리스트, 화면구성, 스케줄을 기준으로 레이아웃 재생 시간과 레이아웃 seamless를 책임진다.
- 레이아웃은 화면구성에 따라 컨텐츠 객체를 배치하고, 레이아웃 내부 컨텐츠들의 재생 시간 동기화를 책임진다.
- 컨텐츠는 레이아웃의 영역 객체로서 정확한 시간 재생, 다른 컨텐츠와의 시간 동기화, 컨텐츠 간 frame to frame seamless 전환을 책임진다.
- 메인 컨테이너는 1개만 존재한다.
- 레이아웃은 총 2개만 유지한다.
  - 1개는 현재 화면 출력
  - 1개는 다음 스케줄 준비를 위한 완전한 standby 상태
- 컨텐츠 모듈은 레이아웃당 총 6개를 고정 생성하고 필요한 것만 사용한다.
- 모든 모듈은 실행 단계에서 1회만 동적 생성하고 이후에는 재사용한다.
- 레이아웃 to 레이아웃, 컨텐츠 to 컨텐츠 전환은 반드시 frame to frame 기준으로 연결한다.
- WPF 병목을 줄이기 위해 background 로직과 UI thread 경계를 분리한다.
- 새 구조의 Media 주 재생 경로는 MPV만 사용한다.
- `ScrollText`, `WelcomeBoard`는 이번 1차 구현 범위에서 완전 제거한다.

이 문서의 목적은 구현 전에 범위를 고정하고, 현재 코드와 충돌 없이 새 구조를 어디까지 1차에서 바꿀지 명확히 하는 것이다.

---

## 이번 1차의 완료 정의

이번 1차는 아래 조건을 모두 만족해야 완료로 본다.

1. Windows Player 전체가 새 구조 기준으로 빌드되고 실행된다.
2. 기존 현장 운영 기능이 깨지지 않는다.
   - 플레이리스트 재생
   - 페이지 전환
   - 스케줄 전환
   - OnAir / OffAir 처리
   - Sync prepare / commit
   - 로컬 재생 보정
   - 누락 파일 감지 및 재다운로드 요청
3. Media 주 재생 경로가 새 컨테이너/레이아웃/컨텐츠 구조로 완전 전환된다.
4. 상용 운영 기준의 로그, 예외 처리, 상태 추적, 복구 흐름이 반영된다.
5. 기존 구조를 임시 fallback으로 남겨 두지 않는다.
6. 장시간 연속 재생, 스케줄 전환, sync 상황에서 운영 가능한 수준의 안정성을 확보한다.

즉, 이번 1차는 “아키텍처 기반 완성형 cut-over”여야 하며, “일부만 새 구조로 옮기고 나머지는 다음 단계” 방식이 아니다.

---

## 현재 코드 기준 직접 분석한 핵심 파일

### 진입점과 오케스트레이션

- `Player/Windows/NewHyOn_Player/MainWindow.xaml`
- `Player/Windows/NewHyOn_Player/MainWindow.xaml.cs`

### 현재 컨텐츠 재생 윈도우/컨트롤

- `Player/Windows/NewHyOn_Player/ContentsPlayWnd/ContentsPlayWindow.xaml`
- `Player/Windows/NewHyOn_Player/ContentsPlayWnd/ContentsPlayWindow.xaml.cs`
- `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml`
- `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml.cs`
- `Player/Windows/NewHyOn_Player/ContentControls/TransImageControl.xaml`
- `Player/Windows/NewHyOn_Player/ContentControls/TransImageControl.xaml.cs`

### 현재 스케줄/동기화/방송시간 로직

- `Player/Windows/NewHyOn_Player/Services/ScheduleEvaluator.cs`
- `Player/Windows/NewHyOn_Player/Services/UdpSyncService.cs`
- `Player/Windows/NewHyOn_Player/Services/OnAirService.cs`

### 현재 데이터 계약

- `Player/Windows/SharedModels/PageModels.cs`
- `Player/Windows/SharedModels/PlayerModels.cs`
- `Player/Windows/NewHyOn_Player/DataClass/DataShop.cs`
- `Player/Windows/NewHyOn_Player/DataClass/InformaionClass.cs`

---

## 현재 코드 기준 사실 정리

### 1. 현재 컨테이너 책임이 `MainWindow`에 과도하게 몰려 있다

`MainWindow.xaml.cs`는 현재 아래 책임을 모두 직접 가진다.

- 플레이어 초기화
- 플레이리스트 로드
- 페이지 전환
- 페이지 시간 타이머
- 스케줄 평가와 플레이리스트 전환
- OnAir 처리
- UDP Sync prepare/commit 처리
- 컨텐츠 재생 윈도우 풀 생성
- 서브 윈도우 위치 이동
- z-order 재정렬

즉, 목표 구조의 `재생 컨테이너` 역할이 아직 독립 모듈이 아니고 `MainWindow` 내부 로직으로 흩어져 있다.

### 2. 현재 레이아웃 개념은 페이지 정의일 뿐, 런타임 layout double-buffer가 없다

현재 페이지 전환 경로는 사실상 아래 흐름이다.

- `PopPage()`
- `PlayPage()`
- `HideAllContentsPlayWindow()`
- `UpdateContentsPlayingWindow()` / `UpdateSubTitleWindow()` / `UpdateWelcomeBoardDispWindow()`
- 각 윈도우의 표시/이동/재생 재개

즉, 현재는 다음 레이아웃을 off-screen에서 미리 완성해 두는 구조가 아니라, 페이지 전환 시점에 현재 화면을 숨기고 다시 구성하는 구조다.

### 3. 현재 컨텐츠 재생은 MPV only 구조가 아니다

`ContentsPlayWindow`는 현재 아래처럼 혼합 구조다.

- 비디오: `MPVLibControl`
- 이미지: `TransImageControl`

즉, 현재 `DisplayType.Media`의 주 재생 경로가 MPV only가 아니며, 이미지와 비디오가 서로 다른 surface에서 번갈아 처리된다.

### 4. 현재 컨텐츠 재사용 개수와 목표 개수가 다르다

현재 `MainWindow`는 실행 시 풀을 1회 생성하지만, 수량과 목적이 목표 구조와 다르다.

- `DisplayLimit = 10`
- `WelcomeLimit = 10`
- `ScrollLimit = 2`

즉, 현재는 “레이아웃 2개 x 컨텐츠 6개”가 아니라 “타입별 다수 윈도우 풀” 구조다.

### 5. 현재 sync는 컨텐츠 인덱스 prepare/commit까지만 구현되어 있다

현재 sync는 `UdpSyncService`와 `ContentsPlayWindow.TryApplySyncIndex()`를 통해 다음 컨텐츠 인덱스를 맞추는 수준이다.

- Leader가 `Prepare(nextIndex)` 전송
- Leader가 `Commit(nextIndex)` 전송
- Follower가 `TryApplySyncIndex(index)` 호출

즉, 컨텐츠 인덱스 동기화는 있으나, 레이아웃 standby 준비 완료 상태까지 포함하는 상위 상태 머신은 아직 없다.

### 6. `ScrollText` / `WelcomeBoard`는 현재 코드에 남아 있지만 1차 제거 대상이다

현재 코드에서 Media 외에도 아래 창이 실제 재생 경로에 포함되어 있다.

- `ScrollTextPlayWindow`
- `WelcomeBoardWindow`

하지만 이번 1차 구현에서는 이 두 기능을 상용 범위에서 완전히 제거한다. 즉, 새 구조는 이 창들을 생성·배치·활성화하지 않는 방향으로 정리해야 한다.

### 7. 레거시 재생 컨트롤이 프로젝트에 남아 있다

현재 프로젝트에는 아래 레거시 경로가 소스 레벨로 남아 있다.

- `DirectShowControl`
- `DirectShowEVRControl`
- `DirectShowPlayer`
- `QuartzControl`
- `MEDisplayElement`
- `WMKitMediaElement`
- `MKitDisplayElement`

현재 메인 경로는 아니지만, 구조 개편 시 유지 대상과 제거 대상을 명확히 구분해야 한다.

---

## 현재 구조의 핵심 문제

### 문제 1. 컨테이너/레이아웃/컨텐츠 경계가 없다

현재 `MainWindow`와 `ContentsPlayWindow`가 서로의 책임을 침범하고 있다.

- `MainWindow`가 페이지와 컨텐츠 윈도우를 직접 조작한다.
- `ContentsPlayWindow`가 영역 재생, 컨텐츠 순환, 일부 sync hold까지 직접 들고 있다.

### 문제 2. frame to frame 전환을 보장할 준비 버퍼가 없다

현재는 다음 레이아웃과 다음 컨텐츠를 off-screen에서 완성해 두는 고정 standby 구조가 아니므로, 레이아웃 전환과 컨텐츠 전환 모두 “재구성 후 표시”에 가깝다.

### 문제 3. WPF 병목이 UI thread에 집중된다

현재 타이머 tick, 페이지 전환, 윈도우 이동, 표시/숨김, 일부 재생 전환 호출이 `Dispatcher` 안에서 연쇄적으로 처리된다.

### 문제 4. 이미지/비디오 surface가 분리되어 있다

`MPV + TransImageControl` 혼합 구조는 단기적으로는 동작하지만, 사용자가 요구한 MPV only 기반의 일관된 content runtime과는 어긋난다.

### 문제 5. 고정 재사용 수량이 설계로 보장되지 않는다

현재는 타입별 여러 윈도우를 여유 있게 만들어 두는 방식이며, “컨테이너 1 / 레이아웃 2 / 슬롯 6 x 2”가 코드 구조로 보장되지 않는다.

### 문제 6. 상용 운영 기준의 cut-over 정의가 부족하다

아키텍처를 바꾸는 것만으로는 부족하다. 실제 상용 서비스 기준에서는 아래가 필요하다.

- 장애 발생 시 로그로 원인 추적 가능
- 일부 파일 누락, 스케줄 변경, sync 지연 상황에서도 플레이어가 멈추지 않음
- 장시간 재생 중 메모리/핸들/윈도우 리소스가 안정적임
- 제거 대상 기능이 메인 경로에 잔존하지 않음

---

## 1차 적용 목표

1차에서는 아래를 목표 상태로 고정한다.

### A. 재생 컨테이너는 단일 런타임 1개

역할:

- 현재 플레이리스트와 스케줄 평가 결과를 반영한다.
- 현재 활성 레이아웃과 standby 레이아웃을 관리한다.
- 페이지 시간과 레이아웃 전환 시점을 결정한다.
- sync leader/follower 동작을 상위에서 관리한다.
- UI thread와 background 작업을 분리한다.
- Media / schedule / sync / on-air 중심의 전체 재생 상태를 총괄한다.

### B. 레이아웃 런타임은 정확히 2개

역할:

- 현재 페이지에 필요한 Media 영역 배치를 보유한다.
- layout 준비, ready, active, standby 상태를 가진다.
- layout 내부 컨텐츠 6개의 재생 시간을 동기화한다.
- 다음 layout은 off-screen에서 완전 준비 후 활성화한다.

### C. 컨텐츠 런타임은 레이아웃당 정확히 6개

역할:

- 각 영역은 고정 MPV surface 1개만 가진다.
- 이미지/비디오 모두 MPV 기준으로 재생한다.
- 컨텐츠 정확한 재생 시간과 다음 항목 준비를 책임진다.
- layout 내부 다른 슬롯과 시간 동기화 가능해야 한다.
- content to content 전환은 frame to frame 기준으로 이어져야 한다.

### D. 전체 플레이어는 1차 목표 기능만 남긴 채 cut-over 되어야 한다

역할:

- Media path는 새 구조로 완전 전환한다.
- `ScrollText`, `WelcomeBoard`는 1차 서비스 범위에서 제거하고 관련 오케스트레이션도 남기지 않는다.
- 스케줄, sync, on-air, local recovery, heartbeat, remote command 흐름은 남겨야 하는 상용 범위 안에서 기존과 동등하거나 더 안정적으로 동작해야 한다.

---

## 1차 구현 범위 확정

### 포함 범위

#### 1. `DisplayType.Media` 주 재생 경로를 새 구조로 전환

- 새 컨테이너 모듈 도입
- 새 레이아웃 2중 버퍼 도입
- 새 MPV content slot 6개 x 2 도입
- 페이지/스케줄/sync와 연결
- 기존 `ContentsPlayWindow` 중심 Media 경로 대체

#### 2. 메인 화면 표시 방식을 `서브 윈도우 다중 운용`에서 `메인 윈도우 내부 host control` 중심으로 전환

- 현재처럼 media 영역마다 별도 `Window`를 띄우지 않는다.
- `MainWindow` 내부에 새 playback host를 두고, 그 안에 layout 2개를 유지한다.
- layout 활성/비활성은 visibility, z-index, opacity, bounds 변경으로만 처리한다.

#### 3. 모듈 1회 생성 + 재사용 구조를 코드로 고정

- 앱 시작 시 1회만 생성
- 이후 페이지 전환, 스케줄 전환, sync 전환마다 재사용
- page마다 컨트롤/런타임 new 하지 않는다.

#### 4. sync를 컨테이너/레이아웃/컨텐츠 구조로 재배치

- 현재 UDP `Prepare/Commit` 메시지 형식은 유지한다.
- 그러나 메시지 적용 지점은 `ContentsPlayWindow`가 아니라 새 컨테이너 런타임이 된다.
- follower는 standby layout 또는 standby content state를 먼저 준비하고 commit에서 전환한다.

#### 5. background 준비와 UI commit을 분리

- background: 페이지 spec 계산, 파일 유효성, duration probe, playlist materialize
- UI thread: MPV handle 연결, bounds 적용, visibility 변경, activation commit

#### 6. `ScrollText` / `WelcomeBoard` 완전 제거

- 이번 1차에서는 `ScrollText`, `WelcomeBoard`를 표시하지 않는다.
- 새 재생 컨테이너는 두 기능을 위한 창 생성, 배치, 활성화, 타이밍 제어 책임을 갖지 않는다.
- 기존 코드에 남아 있는 관련 윈도우/호출 경로는 메인 실행 흐름에서 분리하거나 제거한다.

#### 7. 상용 운영에 필요한 관측성과 장애 복구

- 주요 state transition 로그 추가
- standby 준비 실패 원인 로그 추가
- slot/layout prepare timeout 감지
- 재생 불가 콘텐츠 skip 규칙 명확화
- 파일 누락 시 기존 다운로드 재시도 흐름과 자연스럽게 연결
- 예외 발생 시 전체 플레이어가 멈추지 않고 다음 재생 흐름을 이어갈 수 있어야 함

### 1차 제외 범위

#### 1. `ScrollText` / `WelcomeBoard`는 1차 범위에서 완전 제외한다

이번 1차는 상용 서비스 가능한 완성형 cut-over가 목적이지만, 그 상용 범위에는 `ScrollText`, `WelcomeBoard`가 포함되지 않는다.

즉, 이번 1차에서 반드시 보장할 것은 아래다.

1. `ScrollText`, `WelcomeBoard` 창이 실행 중 생성되지 않는다.
2. 페이지 전환/스케줄 전환/sync 흐름이 두 기능 없이도 안정적으로 동작한다.
3. 관련 호출과 오케스트레이션이 메인 실행 경로에 남지 않는다.

#### 2. 레거시 DirectShow/Quartz 파일의 즉시 물리 삭제는 필수가 아니다

삭제보다 더 중요한 것은 main path에서 완전히 분리되고, 신규 구조가 유일한 상용 경로가 되는 것이다.

따라서 1차에서 꼭 보장할 것은 아래다.

- 레거시 경로가 메인 실행 경로에서 호출되지 않는다.
- 새 구조가 실제 서비스 경로가 된다.
- 물리 삭제는 안정화 후 정리 작업으로 갈 수 있다.

---

## 목표 아키텍처

## 1. 재생 컨테이너 모듈

### 제안 타입

- `Player/Windows/NewHyOn_Player/SeamlessPlayback/PlaybackContainerRuntime.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/PlaybackContainerState.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/PlaybackSchedulerBridge.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/PlaybackSyncCoordinator.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/PlaybackSurfaceHost.xaml`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/PlaybackSurfaceHost.xaml.cs`

### 책임

- 앱 시작 시 layout 2개 생성
- 각 layout 안에 content slot 6개 생성
- 현재 플레이리스트와 다음 스케줄 결정
- standby layout 준비 요청
- page end / content end / immediate timing 반영
- sync prepare/commit 반영
- layout activation commit
- OnAir / schedule / local recovery와 충돌하지 않도록 전체 흐름 총괄

### 내부 상태 예시

- `Idle`
- `PreparingStandbyLayout`
- `StandbyReady`
- `SwitchingLayout`
- `Running`
- `Recovering`

---

## 2. 레이아웃 모듈

### 제안 타입

- `Player/Windows/NewHyOn_Player/SeamlessPlayback/LayoutRuntime.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/LayoutState.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/LayoutSceneControl.xaml`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/LayoutSceneControl.xaml.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/LayoutBindingPlan.cs`

### 책임

- 페이지 정의를 받아 레이아웃 슬롯 매핑 계산
- 6개 content slot 중 필요한 슬롯만 활성화
- 페이지 기준 duration과 slot 동기화 barrier 유지
- ready 전까지 off-screen 유지
- commit 시 active로 승격
- prepare 실패 시 원인을 컨테이너에 전달

### 내부 상태 예시

- `Empty`
- `Preparing`
- `Ready`
- `Active`
- `Standby`
- `Faulted`

---

## 3. 컨텐츠 모듈

### 제안 타입

- `Player/Windows/NewHyOn_Player/SeamlessPlayback/MpvContentSlotRuntime.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/ContentSlotState.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/MpvContentSlotControl.xaml`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/MpvContentSlotControl.xaml.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/MpvPlaylistBuilder.cs`
- `Player/Windows/NewHyOn_Player/SeamlessPlayback/PlaybackProbeService.cs`

### 책임

- MPV surface 1개를 영구 소유
- element/content 리스트를 MPV 재생 sequence로 변환
- 첫 프레임 준비 여부 추적
- 다음 항목 prewarm
- commit 시점의 정확한 재생 시작
- content boundary 이벤트 발행
- 재생 불가 항목 skip 및 상태 보고

### 내부 상태 예시

- `Empty`
- `Binding`
- `Preparing`
- `Ready`
- `Playing`
- `Holding`
- `Faulted`

---

## 새 WPF 화면 구조

### 현재 구조

- `MainWindow`
- `ContentsPlayWindow` 여러 개
- `ScrollTextPlayWindow` 여러 개
- `WelcomeBoardWindow` 여러 개

현재는 위와 같지만, 이번 1차 목표는 Media 경로 외 보조 창 중 `ScrollTextPlayWindow`, `WelcomeBoardWindow`를 런타임에서 제외하는 것이다.

### 1차 목표 구조

- `MainWindow`
  - `PlaybackSurfaceHost` 1개
    - `LayoutSceneControl A`
      - `MpvContentSlotControl` 6개
    - `LayoutSceneControl B`
      - `MpvContentSlotControl` 6개

즉, Media 주 재생 경로는 별도 서브 윈도우 풀 대신 메인 윈도우 내부의 고정 컨트롤 트리로 전환하고, `ScrollTextPlayWindow`, `WelcomeBoardWindow`는 1차 런타임에서 제거한다.

---

## 현재 코드에서 새 구조로 옮길 책임 매핑

| 현재 위치 | 현재 책임 | 새 위치 |
| --- | --- | --- |
| `MainWindow.PopPage()` | 페이지 선택과 즉시 적용 | `PlaybackContainerRuntime` |
| `MainWindow.PlayPage()` | 페이지별 element 배치 | `LayoutRuntime.BindPage()` |
| `MainWindow.TickTask()` | 페이지 tick + 전환 조건 | `PlaybackContainerRuntime.Tick()` |
| `MainWindow.TryApplyScheduledSwitch()` | 스케줄 전환 | `PlaybackSchedulerBridge` + `PlaybackContainerRuntime` |
| `MainWindow.OnSyncMessageReceived()` | sync prepare/commit 적용 | `PlaybackSyncCoordinator` |
| `MainWindow.HideAllContentsPlayWindow()` | media reset | 새 layout/container deactivate 흐름 |
| `ContentsPlayWindow.OrderingCanvasBGContents()` | content 순환 | `MpvContentSlotRuntime` |
| `ContentsPlayWindow.Tick()` | content boundary 판정 | `MpvContentSlotRuntime.Tick()` |
| `ContentsPlayWindow.DisplayVideo()` | 비디오 재생 | `MpvContentSlotRuntime` |
| `ContentsPlayWindow.DisplayImage()` | 이미지 surface 전환 | MPV playlist 기반 이미지 재생으로 치환 |
| `ScrollTextPlayWindow` / `WelcomeBoardWindow` 직접 제어 | 제거 대상 보조 화면 orchestration | 메인 실행 경로에서 제거 |
| `MPVLibControl` | MPV 래퍼 | 유지하되 state/observe API 확장 |

---

## MPV only 적용 원칙

이번 1차에서는 Media 재생 경로에 대해 아래를 강제한다.

- 이미지도 MPV item으로 취급한다.
- 비디오도 MPV item으로 취급한다.
- slot마다 MPV host는 1개만 존재한다.
- 이미지 전환을 위해 `TransImageControl`에 의존하지 않는다.
- `DisplayVideo()` / `DisplayImage()` 분기 대신 `playlist item commit` 중심 구조로 재편한다.
- content 간 prepare / ready / commit 시점을 명시적으로 상태화한다.

필요 시 `MPVLibControl` 확장 항목:

- 현재 playlist index 조회
- 첫 프레임 준비 완료 신호
- item changed 신호
- preload / replace playlist API
- loop-file / image-display-duration / pause / seek / playlist-pos 제어 보강
- error / stalled / end-file reason 추적

---

## WPF 병목 완화 원칙

### background thread로 보내는 작업

- 스케줄 평가
- 다음 페이지 element spec 계산
- 콘텐츠 파일 존재/용량 검증
- 재생 길이 probe
- MPV용 재생 시퀀스 구성
- standby layout 준비 상태 계산
- prepare 실패 원인 정리

### UI thread에 남기는 작업

- 컨트롤 생성 1회
- MPV handle 연결
- bounds, opacity, visibility, z-index 반영
- active/standby layout commit
- 실제 재생 시작/일시정지/seek

### 금지 사항

- tick마다 layout rebuild
- page 전환 때 컨트롤 재생성
- UI thread에서 파일 스캔/DB 조회/복잡한 LINQ 반복
- 예외를 숨긴 채 조용히 실패하는 처리

---

## 상용 서비스 기준 추가 요구사항

### 1. 기능 호환성

- 1차 서비스 범위에 남는 운영 기능이 동등하게 유지되어야 한다.
- 기존 설정과 데이터 계약을 최대한 깨지 않게 유지해야 한다.
- 기존 플레이리스트/페이지 데이터로 바로 동작해야 한다.

### 2. 장애 복구

- 일부 콘텐츠가 손상되거나 누락되어도 플레이어 전체가 멈추면 안 된다.
- standby layout prepare 실패 시 원인 로그를 남기고 다음 가능한 재생 흐름으로 복구해야 한다.
- sync 지연이나 follower 늦은 준비 시에도 무한 대기 상태에 빠지면 안 된다.

### 3. 운영 관측성

- 현재 active layout id / standby layout id / active page / next page / active content index를 로그와 디버그 정보로 확인 가능해야 한다.
- schedule switch reason, sync prepare/commit reason, content skip reason을 로그로 남겨야 한다.

### 4. 장시간 안정성

- 24시간 이상 연속 재생 시 메모리와 핸들 누수가 없어야 한다.
- layout/slot 재사용 중 event handler 중복 등록이 없어야 한다.
- hidden window/control이 누적 생성되지 않아야 한다.

### 5. cut-over 안정성

- 새 구조가 실제 main path가 되어야 한다.
- 기존 Media 경로 fallback을 남겨 두지 않는다.
- 운영자가 “어느 경로가 실제 재생 중인지” 혼동하지 않도록 경로를 단일화한다.

---

## 1차 단계별 구현 계획

### Phase 1. 새 런타임/컨트롤 뼈대 추가

- `SeamlessPlayback` 폴더 생성
- 컨테이너/레이아웃/컨텐츠 상태 타입 추가
- `MainWindow`에 새 host control 자리 추가
- 앱 시작 시 layout 2개 x slot 6개를 1회 생성
- 상태 로그/디버그용 기본 telemetry 추가

### Phase 2. 페이지 배치와 standby layout 준비 구현

- `PageInfoClass.PIC_Elements`를 layout slot spec으로 변환
- layout A/B 중 standby layout에 다음 페이지를 off-screen에서 준비
- ready barrier 완성 전까지 활성화 금지
- prepare timeout / prepare fault 처리 추가

### Phase 3. MPV content slot runtime 구현

- 기존 `ContentsPlayWindow`의 content 순환 로직을 slot runtime으로 이동
- 이미지/비디오를 MPV playlist item으로 통합
- slot boundary와 layout boundary 이벤트 정리
- skip / retry / fault logging 추가

### Phase 4. 스케줄/sync/on-air 연결

- `ScheduleEvaluator` 결과를 새 컨테이너에 연결
- `UdpSyncService` prepare/commit 적용 지점을 새 컨테이너로 이동
- follower는 commit 전에 standby slot/layout을 ready 상태로 맞춤
- OnAir / OffAir와의 상호작용 검증

### Phase 5. `ScrollText` / `WelcomeBoard` 제거 및 기존 Media 경로 cut-over

- `ScrollTextPlayWindow`, `WelcomeBoardWindow` 생성/표시/배치 경로를 메인 실행 흐름에서 제거
- `PlayPage()`와 `UpdateContentsPlayingWindow()`의 Media 경로를 새 runtime 진입으로 교체
- `ContentsPlayWindow`는 더 이상 main media path에서 사용하지 않음
- `TransImageControl`도 main media path에서 제거
- 레거시 경로와 제거 대상 기능이 main path에서 호출되지 않는지 검증

### Phase 6. 상용 안정화

- 장시간 재생 검증
- schedule churn 검증
- sync leader/follower 검증
- 파일 누락/손상 데이터 검증
- 예외/로그/복구 흐름 검증

---

## 구현 프롬프트

```md
현재 `Player/Windows/NewHyOn_Player`의 Windows Player 재생 구조를 1차 목표 구조로 개편하라.

중요 전제:

- 이번 1차 구현만으로도 실제 상용 서비스에 바로 투입 가능한 완성형 프로젝트여야 한다.
- 부분 시범 적용, 임시 브리지, 추후 2차에서 핵심 기능 지원 예정 같은 상태는 허용하지 않는다.
- 기존 상용 기능은 깨지면 안 된다.

필수 요구사항:

1. 재생 컨테이너는 1개만 존재해야 한다.
2. 레이아웃 런타임은 2개만 유지해야 한다.
   - 1개는 현재 active
   - 1개는 다음 스케줄/page용 standby
3. 컨텐츠 런타임은 layout당 6개만 고정 생성해야 한다.
4. 실행 단계에서 컨트롤과 런타임은 1회만 생성하고, 이후 page/schedule/content 전환마다 재사용해야 한다.
5. 레이아웃 to 레이아웃 전환은 standby layout이 완전히 준비된 뒤 commit되어야 한다.
6. 컨텐츠 to 컨텐츠 전환은 frame to frame 기준으로 이어져야 한다.
7. Media 주 재생 경로는 MPV only여야 한다.
8. `ContentsPlayWindow`의 `MPV + TransImageControl` 혼합 구조를 main path에서 제거해야 한다.
9. 새 Media 경로는 별도 서브 윈도우가 아니라 `MainWindow` 내부 host control 계층에서 동작해야 한다.
10. background 준비와 UI commit을 분리해 WPF UI thread 병목을 줄여야 한다.
11. 기존 sync `Prepare/Commit` 의미는 유지하되 적용 지점은 새 컨테이너/runtime 구조로 이동해야 한다.
12. OnAir, schedule, local recovery 등 1차 서비스 범위의 기존 상용 기능은 유지되어야 한다.
13. `ScrollText`, `WelcomeBoard`는 이번 1차 범위에서 제거되어 메인 실행 경로에 남아 있으면 안 된다.
14. fallback으로 기존 Media 윈도우 경로를 계속 타지 말고, cut-over 이후 main path는 새 구조 하나만 사용해야 한다.
15. 로그, 예외 처리, prepare timeout, skip reason, recovery 흐름까지 포함해 운영 가능한 상태여야 한다.

현재 코드 기준으로 반드시 먼저 읽을 파일:

- `Player/Windows/NewHyOn_Player/MainWindow.xaml`
- `Player/Windows/NewHyOn_Player/MainWindow.xaml.cs`
- `Player/Windows/NewHyOn_Player/ContentsPlayWnd/ContentsPlayWindow.xaml.cs`
- `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml.cs`
- `Player/Windows/NewHyOn_Player/Services/ScheduleEvaluator.cs`
- `Player/Windows/NewHyOn_Player/Services/UdpSyncService.cs`
- `Player/Windows/NewHyOn_Player/Services/OnAirService.cs`
- `Player/Windows/SharedModels/PageModels.cs`
- `Player/Windows/SharedModels/PlayerModels.cs`

구현 지침:

- `MainWindow`는 최종적으로 화면 shell과 상위 진입점 역할만 남긴다.
- 새 폴더 `Player/Windows/NewHyOn_Player/SeamlessPlayback` 아래에 컨테이너/레이아웃/컨텐츠 런타임과 host control을 만든다.
- layout은 2개를 모두 앱 시작 시 생성하고, active/standby 역할만 교대한다.
- 각 layout 안의 mpv slot 6개도 앱 시작 시 생성하고, 필요한 slot만 바인딩한다.
- `DisplayType.Media`는 새 구조로 완전 전환한다.
- `ScrollText`, `WelcomeBoard`는 이번 1차 구현 범위에서 제거하고 메인 실행 경로에서 분리한다.
- 이미지/비디오는 모두 MPV 기준 playlist item으로 정리한다.
- slot runtime은 첫 프레임 준비와 commit 시점을 추적해야 한다.
- UI thread에서는 handle 연결, 시각 속성 적용, commit만 수행하고, 파일 검증/다음 spec 준비는 background로 이동한다.
- 구현 후에는 기존 Media path에서 `ContentsPlayWindow`와 `TransImageControl` 의존이 사라졌는지 확인한다.
- 상용 운영 관점에서 상태 추적 로그와 예외 복구 흐름을 반드시 넣는다.

산출물:

1. 새 runtime/control 구조
2. active/standby layout 상태 머신
3. slot 6개 고정 재사용 구조
4. page/schedule/content/sync 연결
5. 제거 대상 기능 분리 결과
6. `MainWindow` 단순화
7. Media main path cut-over
8. 빌드 검증 결과
9. 운영 로그/복구 전략 반영 결과
10. 변경 요약 및 남은 리스크 정리
```

---

## 작업 체크리스트

### 1. 사전 분석

- [x] `MainWindow.xaml.cs`의 초기화, page 전환, tick, schedule, sync 흐름을 읽고 역할을 정리한다.
- [x] `ContentsPlayWindow.xaml.cs`의 content 순환, image/video 분기, sync hold 로직을 읽는다.
- [x] `MPVLibControl.xaml.cs`의 현재 MPV 래퍼 API를 확인한다.
- [x] 현재 Media 경로가 `MPV + TransImageControl` 혼합 구조임을 확인한다.
- [x] 현재 풀 생성 개수가 `DisplayLimit=10`, `WelcomeLimit=10`, `ScrollLimit=2`임을 확인한다.
- [x] `ScrollText` / `WelcomeBoard`가 현재 코드에 남아 있는 제거 대상 기능임을 확인한다.

### 2. 새 구조 뼈대

- [ ] `SeamlessPlayback` 폴더를 만든다.
- [ ] 재생 컨테이너 상태/계약 타입을 정의한다.
- [ ] 레이아웃 상태/계약 타입을 정의한다.
- [ ] 컨텐츠 슬롯 상태/계약 타입을 정의한다.
- [ ] `MainWindow` 내부 host control 구조를 추가한다.
- [ ] 상태 추적용 로그 포인트를 정의한다.

### 3. 생성/재사용 규칙

- [ ] layout 2개를 앱 시작 시 1회 생성한다.
- [ ] layout당 slot 6개를 앱 시작 시 1회 생성한다.
- [ ] page 전환마다 layout/slot new 하지 않는다.
- [ ] 전환마다 `Window`를 새로 띄우지 않는다.
- [ ] 이벤트/타이머 중복 등록이 없는지 보장한다.

### 4. 레이아웃 준비/전환

- [ ] 다음 page를 standby layout에서 off-screen 준비한다.
- [ ] 준비 완료 전에는 active 승격하지 않는다.
- [ ] layout to layout commit 시 bounds/visibility/z-index만 바꾼다.
- [ ] page duration 종료 시점에 standby ready 상태를 우선 확인한다.
- [ ] prepare timeout과 fault 상태를 처리한다.

### 5. MPV content slot

- [ ] slot마다 MPV host 1개만 유지한다.
- [ ] 이미지와 비디오를 MPV item으로 통합한다.
- [ ] 컨텐츠 재생 시간 계산과 다음 item 준비를 slot runtime이 담당한다.
- [ ] content to content 전환이 frame to frame 기준으로 이어지는지 검증한다.
- [ ] `TransImageControl` 의존을 main Media path에서 제거한다.
- [ ] skip / error / end-of-file reason을 기록한다.

### 6. 스케줄/Sync/OnAir

- [ ] `ScheduleEvaluator` 결과를 새 컨테이너가 소비하도록 연결한다.
- [ ] `SwitchTiming`에 따라 immediate / content end / page end 전환을 반영한다.
- [ ] `Prepare/Commit` sync를 standby layout/slot 구조에 맞게 재배치한다.
- [ ] follower가 commit 전에 ready 상태를 갖추도록 한다.
- [ ] OnAir / OffAir 전환이 새 구조와 충돌하지 않는지 확인한다.

### 7. `ScrollText` / `WelcomeBoard` 제거

- [ ] `ScrollTextPlayWindow`, `WelcomeBoardWindow` 생성 경로를 메인 실행 흐름에서 제거한다.
- [ ] 페이지 전환 시 두 기능을 위한 표시/배치/activate 호출이 남아 있지 않게 한다.
- [ ] 제거 후에도 Media / schedule / sync / OnAir 흐름이 깨지지 않는지 확인한다.
- [ ] 관련 로그와 상태 추적에서 제거 대상 기능이 더 이상 핵심 런타임 상태로 취급되지 않게 한다.

### 8. MainWindow 단순화

- [ ] `PopPage()`의 page orchestration을 새 컨테이너로 이동한다.
- [ ] `PlayPage()`의 Media 경로를 새 layout binding으로 이동한다.
- [ ] `TickTask()`의 Media tick 책임을 새 컨테이너/runtime으로 이동한다.
- [ ] `MainWindow`에는 shell, lifecycle, 상위 서비스 연결만 남긴다.

### 9. 상용 cut-over 확인

- [ ] Media path가 새 구조 하나로만 동작하는지 확인한다.
- [ ] 기존 Media fallback 경로가 운영 경로에 남아 있지 않은지 확인한다.
- [ ] 레거시 DirectShow/Quartz 계열이 main path에서 더 이상 호출되지 않는지 확인한다.
- [ ] 장애 상황 로그만으로 문제 원인 추적이 가능한지 확인한다.

### 10. 검증

- [ ] 솔루션 빌드가 통과한다.
- [ ] 앱 시작 후 layout 2개, slot 12개만 생성되는지 확인한다.
- [ ] 일반 page 전환이 standby -> active commit으로 동작하는지 확인한다.
- [ ] schedule 전환이 standby 준비 후 자연스럽게 적용되는지 확인한다.
- [ ] sync leader/follower에서 prepare/commit 타이밍이 유지되는지 확인한다.
- [ ] 제거 후에도 `ScrollText` / `WelcomeBoard` 관련 경로가 실행되지 않는지 확인한다.
- [ ] 파일 누락/손상 상황에서 전체 플레이어가 멈추지 않는지 확인한다.
- [ ] 장시간 재생 시 WPF UI thread 병목과 메모리 증가가 안정적인지 확인한다.
- [ ] 상용 운영에 필요한 로그와 상태 추적이 충분한지 확인한다.

---

## 최종 범위 선언

이번 1차 적용은 **Windows Player의 Media 주 재생 경로를 MPV only 기반의 고정 재사용 구조로 전환하고, `ScrollText` / `WelcomeBoard`를 제거한 1차 서비스 범위만으로도 바로 배포 가능한 완성형 프로젝트로 cut-over 하는 것**에 집중한다.

따라서 이번 1차의 완료 기준은 아래다.

- 재생 컨테이너 1개가 존재한다.
- 레이아웃 2개가 active/standby로 고정 재사용된다.
- 레이아웃당 MPV content slot 6개가 고정 재사용된다.
- layout to layout, content to content 전환이 prepare 후 commit 구조로 바뀐다.
- Media main path가 기존 `ContentsPlayWindow` 중심 구조에서 새 구조로 넘어간다.
- `ScrollText` / `WelcomeBoard` 없이도 Media / OnAir / schedule / sync / local recovery를 포함한 전체 플레이어가 상용 수준으로 동작한다.
- 운영 로그, 장애 복구, 장시간 안정성까지 포함해 바로 서비스 가능한 상태가 된다.

이 범위를 기준으로 실제 구현을 시작한다.
