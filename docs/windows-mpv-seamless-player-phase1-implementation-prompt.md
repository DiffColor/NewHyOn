# Windows Player MPV Seamless 1차 구현 실행 프롬프트

## 문서 목적

이 문서는 현재 코드 기준의 `NewHyOn Player` Windows Player를 **실제로 구현하기 위한 최종 실행 프롬프트**다.

목표는 문서 검토가 아니라 **실제 코드 cut-over**다.

이 문서는 아래 요구를 Codex가 바로 이해하고 흔들림 없이 구현할 수 있도록 고정한다.

- 재생 구조를 `재생 컨테이너`, `레이아웃`, `컨텐츠` 3개 모듈로 재구성한다.
- `Media` 재생 경로는 `MPV`만 사용한다.
- 컨테이너는 1개만 존재한다.
- 레이아웃은 총 2개만 존재한다.
  - 1개는 현재 화면 출력
  - 1개는 다음 스케줄 준비를 위한 완전한 standby
- 컨텐츠 모듈은 레이아웃당 총 6개만 고정 생성한다.
- 모든 모듈은 실행 단계에서 1회만 생성하고 이후에는 재사용한다.
- 레이아웃 to 레이아웃, 컨텐츠 to 컨텐츠 전환은 반드시 frame to frame 기준으로 이어진다.
- WPF 병목을 줄이기 위해 background 로직과 UI thread 경계를 분리한다.
- 현재 존재하는 컨트롤과 로직을 **실제로 다시 분석한 뒤** 새 로직과 새 컨트롤을 만든다.
- 구조는 복잡한 추상화보다 **simple is best** 원칙으로 정리한다.

이 문서는 아래 문서를 기반으로 하되, 실제 구현 시작 시에는 이 문서의 지시를 우선한다.

- `docs/windows-mpv-seamless-player-phase1-plan.md`

---

## 현재 코드 기준 구현 전제

반드시 아래 전제를 현재 코드 기준으로 다시 확인하고 구현을 시작한다.

### 1. 현재 오케스트레이션은 `MainWindow`에 몰려 있다

현재 `Player/Windows/NewHyOn_Player/MainWindow.xaml.cs`는 아래를 직접 다룬다.

- 플레이어 초기화
- 플레이리스트 로드
- 페이지 전환
- 스케줄 전환
- sync 처리
- on-air / off-air 처리
- 재생 윈도우 풀 관리
- 화면 배치와 z-order 반영

즉, 목표 구조의 `재생 컨테이너` 책임이 아직 분리되어 있지 않다.

### 2. 현재 `ContentsPlayWindow`는 책임이 너무 많다

현재 `Player/Windows/NewHyOn_Player/ContentsPlayWnd/ContentsPlayWindow.xaml.cs`는 아래를 동시에 가진다.

- 개별 영역 재생
- 컨텐츠 인덱스 순환
- tick 처리
- sync hold 대응
- 파일 유효성 검사
- 영상/이미지 타입 분기

즉, 목표 구조의 `컨텐츠 모듈`과 `레이아웃 동기화` 경계가 아직 없다.

### 3. 현재 `MPVLibControl`은 재생 엔진 래퍼이지 심리스 오케스트레이터는 아니다

현재 `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml.cs`는 MPV 래퍼 역할을 수행하지만, 아래 기능은 구현 구조로 분리되어 있지 않다.

- playlist prebuild
- 다음 항목 preload
- first-frame-ready 관측
- frame to frame 전환 보장
- layout standby 준비 완료 신호

즉, 심리스 구현을 위해 새 runtime 계층이 필요하다.

### 4. 이번 작업은 제거가 아니라 새 심리스 구조 구현이다

`ScrollText`, `WelcomeBoard` 제거는 이미 방향이 정해져 있지만, 이번 문서의 핵심은 그것만이 아니다.

이번 작업의 핵심은 아래다.

- 새 재생 컨테이너 런타임 도입
- 새 레이아웃 double-buffer 도입
- 새 MPV 기반 컨텐츠 런타임 도입
- 기존 `MainWindow` 중심 구조에서 새 모듈 중심 구조로 cut-over

---

## Codex용 최종 실행 프롬프트

```md
현재 `NewHyOn Player`의 Windows Player 코드를 **현재 시점의 실제 코드 기준으로 다시 모두 읽고**, 아래 목표 구조를 기준으로 1차 심리스 구현을 **끝까지 실제 동작하는 코드로 구현하라.**

중요:
- 추측하지 말고 반드시 현재 코드를 다시 읽고 그 구조를 기준으로 수정한다.
- 문서 정리가 아니라 **실제 코드 구현**이 목적이다.
- 중간 확인 요청 없이 끝까지 진행한다.
- fallback은 넣지 않는다.
- 단순히 기존 경로를 감싸는 방식이 아니라 **새 구조를 메인 실행 경로로 cut-over** 한다.
- simple is best 원칙을 유지한다.
- 응답과 작업은 항상 한국어 기준으로 한다.

---

## 이번 작업의 최우선 목표

재생 구조를 아래 3개 모듈로 재구성한다.

### 1. 재생 컨테이너 모듈

역할:
- 플레이리스트, 화면구성, 스케줄을 체크한다.
- 현재 활성 레이아웃과 standby 레이아웃을 관리한다.
- 레이아웃 재생 시간과 레이아웃 seamless를 책임진다.
- on-air / off-air / sync / 스케줄 전환 / 로컬 보정 흐름을 상위에서 총괄한다.
- background 로직과 UI thread 경계를 분리한다.

수량:
- 컨테이너는 정확히 1개만 존재한다.

### 2. 레이아웃 모듈

역할:
- 화면구성을 기준으로 컨텐츠 객체들을 배치한다.
- 레이아웃 내부 컨텐츠들의 재생 시간 동기화를 책임진다.
- 현재 화면 출력 상태와 standby 준비 상태를 가진다.
- 다음 스케줄의 레이아웃을 off-screen에서 완전 준비한 뒤 활성화한다.

수량:
- 레이아웃은 정확히 2개만 존재한다.
  - 1개는 active
  - 1개는 standby

### 3. 컨텐츠 모듈

역할:
- 레이아웃의 영역 객체다.
- 각 컨텐츠의 정확한 시간 재생을 책임진다.
- 레이아웃 내 다른 객체들과 시간 동기화를 맞춘다.
- 컨텐츠 to 컨텐츠 전환을 frame to frame으로 이어야 한다.
- 이미지/영상 모두 MPV만 사용한다.

수량:
- 컨텐츠 모듈은 레이아웃당 정확히 6개만 존재한다.
- 필요한 슬롯만 사용하고, 나머지는 비활성 상태로 유지한다.

공통 제약:
- 모든 모듈은 실행 단계에서 1번만 동적 생성한다.
- 이후에는 생성한 모듈만 재사용한다.
- 실행 중 임시로 새 레이아웃/새 슬롯/새 윈도우를 추가 생성하지 않는다.

---

## 반드시 지켜야 할 구현 원칙

### 1. 현재 코드 기준으로 먼저 전부 다시 읽는다

최소한 아래 파일들을 반드시 다시 읽고 시작한다.

- `Player/Windows/NewHyOn_Player/MainWindow.xaml`
- `Player/Windows/NewHyOn_Player/MainWindow.xaml.cs`
- `Player/Windows/NewHyOn_Player/ContentsPlayWnd/ContentsPlayWindow.xaml`
- `Player/Windows/NewHyOn_Player/ContentsPlayWnd/ContentsPlayWindow.xaml.cs`
- `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml`
- `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml.cs`
- `Player/Windows/NewHyOn_Player/ContentControls/TransImageControl.xaml.cs`
- `Player/Windows/NewHyOn_Player/Services/ScheduleEvaluator.cs`
- `Player/Windows/NewHyOn_Player/Services/UdpSyncService.cs`
- `Player/Windows/NewHyOn_Player/Services/OnAirService.cs`
- `Player/Windows/SharedModels/PageModels.cs`
- `Player/Windows/SharedModels/PlayerModels.cs`
- `Player/Windows/SharedModels/Enums.cs`
- `Player/Windows/NewHyOn_Player/DataClass/DataShop.cs`

필요하면 실제 참조되는 추가 파일도 더 읽는다.

### 2. 새 구조는 실제 런타임 경계가 분명해야 한다

- `MainWindow`는 최종적으로 진입점과 상위 연결 책임만 남긴다.
- 페이지/레이아웃/컨텐츠 전환의 실제 상태 관리는 새 모듈이 맡는다.
- 기존 `ContentsPlayWindow`는 제거 또는 축소 여부를 현재 코드 기준으로 판단하되, 새 구조의 핵심 책임을 계속 품게 두지 않는다.

### 3. MPV only 원칙을 지킨다

- `Media` 재생은 `MPV`만 사용한다.
- 이미지도 MPV 기반 재생 경로로 넣는다.
- 기존 `TransImageControl` 기반 메인 경로는 새 구조의 주 경로로 유지하지 않는다.
- `MediaElement`, `DirectShow`, 기타 레거시 재생 경로를 새 메인 경로에 다시 섞지 않는다.

### 4. frame to frame 전환을 설계 목표가 아니라 구현 완료 기준으로 본다

- layout to layout 전환은 standby layout이 완전히 준비된 뒤 active와 교체해야 한다.
- content to content 전환은 stop-hide-load-play 방식이 아니라 frame continuity를 목표로 해야 한다.
- first-frame-ready 없이 activation 하지 않는다.

### 5. WPF 병목을 줄이기 위해 thread 경계를 분리한다

- 스케줄 평가, 재생 준비, 파일 검증, preload 가능 작업은 background에서 처리한다.
- UI thread는 화면 노출, 실제 visual activation, dispatcher 필수 작업으로 제한한다.
- Dispatcher 안에서 반복 탐색, 파일 시스템 작업, 무거운 상태 계산을 수행하지 않는다.

### 6. simple is best

- 과도한 추상화나 불필요한 패턴은 피한다.
- 이해 가능한 고정 구조를 우선한다.
- 컨테이너 1, 레이아웃 2, 슬롯 6 x 2 구조가 코드에서 바로 읽혀야 한다.

---

## 구현 목표 구조

권장 구현 폴더:

- `Player/Windows/NewHyOn_Player/PlaybackModes`

권장 핵심 파일:

- `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackCoordinator.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessPlaybackContainer.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessLayoutRuntime.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessContentSlot.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessLayoutHost.xaml`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessLayoutHost.xaml.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessMpvSurface.xaml`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessMpvSurface.xaml.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackContracts.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackDiagnostics.cs`

파일명은 현재 코드와 충돌하지 않게 합리적으로 조정해도 되지만, 역할 경계는 유지한다.

### 재생 컨테이너

필수 책임:
- 현재 플레이리스트 로드 상태 관리
- 현재 페이지와 다음 페이지 결정
- 스케줄 평가 결과 반영
- active layout / standby layout 전환
- sync prepare / commit 상위 orchestration
- on-air / off-air 상태 반영
- 누락 파일 검출과 재준비 트리거
- 레이아웃 활성 시간 관리

### 레이아웃

필수 책임:
- 페이지 정의를 받아 slot 6개에 매핑
- 각 slot의 배치, 크기, 위치, z-order 유지
- layout ready 상태 판단
- 내부 slot들의 시작 기준 시간 동기화
- off-screen prepare 완료 후 active 전환

### 컨텐츠 슬롯

필수 책임:
- MPV surface 1개 보유
- 재생 가능한 item sequence 준비
- 정확한 재생 시간 적용
- 다음 item preload
- first-frame-ready 신호 관리
- sync 인덱스/시간 반영
- slot ready / active / idle / error 상태 유지

---

## 현재 코드에서 반드시 정리해야 할 것

### A. `MainWindow`의 과도한 책임 축소

- 직접 페이지를 구성하고 윈도우를 조작하는 경로를 새 컨테이너 호출로 이동
- tick 기반 직접 제어를 새 런타임으로 이동
- sync, schedule, on-air 연결은 남기되 실제 재생 제어는 새 모듈로 위임

### B. `ContentsPlayWindow` 중심 구조 해소

- 현재 `ContentsPlayWindow`가 가진 컨텐츠 순환/타이밍/분기 로직을 새 컨텐츠 슬롯 구조로 이동
- 필요하면 host view 수준으로 축소하거나 교체

### C. `MPVLibControl` 확장 또는 새 wrapper 도입

아래 중 필요한 방식을 선택한다.

- `MPVLibControl`을 확장
- 새 mode 전용 MPV surface wrapper 생성

반드시 필요한 기능:
- playlist 구성
- preload 또는 다음 아이템 준비
- first-frame-ready 또는 동등한 준비 신호
- 재생 상태 관측
- item changed / end / error 상태 신호

### D. 제거 대상 기능

- `ScrollText`
- `WelcomeBoard`

이 둘은 새 심리스 메인 경로에 포함하지 않는다.
기존 데이터에 남아 있어도 재생 대상에서 제외한다.

---

## 구현 순서

1. 현재 코드 다시 읽기
2. 기존 메인 실행 경로와 실제 책임 분포 정리
3. 새 컨테이너 / 레이아웃 / 슬롯 구조 설계
4. 고정 수량 런타임 생성 구조 구현
5. MPV only 슬롯 재생 경로 구현
6. 레이아웃 standby 준비와 active 전환 구현
7. 스케줄 / sync / on-air 연결
8. `MainWindow` cut-over
9. 제거 대상 경로 정리
10. 빌드와 코드 재검토

---

## 완료 기준

아래를 모두 만족해야 완료다.

1. 컨테이너는 정확히 1개만 생성된다.
2. 레이아웃은 정확히 2개만 생성되고 active/standby로 재사용된다.
3. 각 레이아웃은 slot 6개만 고정 생성하고 재사용한다.
4. 새 메인 Media 경로는 MPV only로 동작한다.
5. 페이지 전환은 standby layout 준비 완료 후 active 교체로 수행된다.
6. 컨텐츠 전환은 frame to frame 기준의 심리스 전환을 목표 상태로 구현한다.
7. `MainWindow`는 재생 세부 제어를 직접 품지 않는다.
8. `ScrollText`, `WelcomeBoard`는 메인 재생 경로에서 제거된다.
9. 스케줄 전환, sync prepare/commit, on-air/off-air, 로컬 보정이 새 구조에서 유지된다.
10. 빌드가 통과한다.
11. 변경 후 코드를 다시 읽었을 때 구조가 단순하고 책임이 명확하다.

---

## 구현 시 추가 지침

- 기존 코드의 문제를 주석으로 덮지 말고 구조로 해결한다.
- dead code는 적극 정리한다.
- 새 구조와 무관한 임시 fallback은 두지 않는다.
- 로그는 장애 원인 추적이 가능하도록 남긴다.
- 장시간 재생과 반복 전환을 고려해 리소스 누수 가능성을 점검한다.
- 현재 코드 기준으로 실제 사용하는 경로만 남기고, 새 구조 중심으로 읽히게 만든다.

---

## 최종 보고 형식

반드시 아래 형식으로 보고한다.

[목표]
- 현재 코드 기준으로 이번 작업의 목표와 기대 결과를 한눈에 이해되게 정리합니다.

[계획]
- 영향 범위: 수정하거나 확인할 파일, 기능, 검증 범위를 적습니다.
- 작업 단계:
  1. ...
  2. ...
  3. ...

[진행 내역]
- 실제로 확인한 코드와 수행한 작업만 짧게 누적합니다.

[최종 보고]
- 변경 사항:
- 수정 파일:
- 검증 결과:
- 남은 이슈:
```

---

## 구현 시작 체크리스트

### 1. 현재 코드 재분석

- [ ] `MainWindow.xaml.cs`의 현재 재생 진입점, tick 경로, 페이지 전환 경로, 스케줄 경로, sync 경로를 다시 읽었다.
- [ ] `ContentsPlayWindow.xaml.cs`의 컨텐츠 순환, 시간 처리, sync hold, 타입 분기를 다시 읽었다.
- [ ] `MPVLibControl.xaml.cs`의 현재 지원 기능과 부족한 기능을 분리했다.
- [ ] `TransImageControl`, 레거시 재생 컨트롤, 제거 대상 기능의 현재 참조 경로를 다시 확인했다.
- [ ] `ScheduleEvaluator`, `UdpSyncService`, `OnAirService`가 새 구조와 연결될 지점을 정리했다.
- [ ] `PageModels`, `PlayerModels`, `Enums`, `DataShop`의 계약 영향 범위를 확인했다.

### 2. 모듈 경계 확정

- [ ] 재생 컨테이너 책임과 `MainWindow` 잔여 책임을 분리했다.
- [ ] 레이아웃 runtime의 상태 모델을 정의했다.
- [ ] 컨텐츠 slot runtime의 상태 모델을 정의했다.
- [ ] active / standby 전환 기준을 명확히 정했다.
- [ ] slot 6개 제한과 layout 2개 제한이 코드 구조에서 보장되게 설계했다.

### 3. 새 구조 구현

- [ ] 새 폴더와 새 핵심 파일을 생성했다.
- [ ] 컨테이너 1개 생성 및 재사용 구조를 구현했다.
- [ ] 레이아웃 2개 생성 및 재사용 구조를 구현했다.
- [ ] 레이아웃당 slot 6개 생성 및 재사용 구조를 구현했다.
- [ ] layout 준비, ready, active, standby 상태 전환을 구현했다.
- [ ] slot 준비, preload, active, complete, error 상태 전환을 구현했다.

### 4. MPV only 경로 구현

- [ ] 이미지와 영상을 MPV 기반 경로로 통합했다.
- [ ] MPV playlist 또는 동등한 준비 구조를 구현했다.
- [ ] 다음 item preload 또는 동등한 사전 준비 구조를 구현했다.
- [ ] first-frame-ready 또는 동등한 activation 기준을 구현했다.
- [ ] content to content frame continuity를 보장하는 경로를 구현했다.

### 5. 레이아웃 seamless 구현

- [ ] 다음 페이지를 standby layout에서 오프스크린 준비하도록 구현했다.
- [ ] standby layout이 완전히 준비되기 전에는 화면 전환하지 않도록 구현했다.
- [ ] layout to layout 전환을 active 교체 방식으로 구현했다.
- [ ] 전환 후 이전 layout을 다음 standby 용도로 재사용하도록 구현했다.

### 6. 운영 기능 연결

- [ ] 스케줄 평가 결과가 새 컨테이너로 연결된다.
- [ ] sync prepare / commit이 새 구조에서 동작한다.
- [ ] on-air / off-air 상태가 새 구조에 반영된다.
- [ ] 로컬 재생 보정과 누락 파일 대응이 새 구조에서 유지된다.
- [ ] 장시간 재생 중 상태 추적 로그가 가능하도록 정리했다.

### 7. 기존 경로 cut-over

- [ ] `MainWindow`의 직접 재생 제어 코드를 새 컨테이너 호출 중심으로 정리했다.
- [ ] `ContentsPlayWindow`에 남아 있던 핵심 재생 책임을 새 구조로 이동했다.
- [ ] 메인 재생 경로에서 `TransImageControl` 의존을 제거했거나 보조 경로로만 격리했다.
- [ ] `ScrollText`, `WelcomeBoard`는 메인 재생 경로에서 제거했다.
- [ ] dead code, 불필요한 필드, 임시 경로를 정리했다.

### 8. 검증

- [ ] 대상 프로젝트 빌드가 통과한다.
- [ ] 컨테이너 1 / 레이아웃 2 / 슬롯 6 x 2 구조가 실제 코드에 반영되었다.
- [ ] active / standby 전환 경로가 코드에서 명확히 읽힌다.
- [ ] MPV only 재생 경로가 메인 경로로 동작한다.
- [ ] 페이지 전환, 스케줄 전환, sync, on-air/off-air가 유지된다.
- [ ] 변경된 코드를 다시 읽어도 구조가 단순하고 책임 경계가 명확하다.

---

## 구현 완료 후 보고 체크리스트

- [ ] 변경 사항을 모듈 단위로 요약했다.
- [ ] 실제 수정 파일 목록을 빠짐없이 정리했다.
- [ ] 빌드 결과와 코드 검토 결과를 구분해 적었다.
- [ ] 남은 리스크와 후속 작업을 숨기지 않고 기록했다.
- [ ] 새 구조가 왜 기존보다 안정적인지 코드 기준으로 설명 가능하다.
