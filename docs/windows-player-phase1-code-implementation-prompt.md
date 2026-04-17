# Windows Player 1차 코드 구현 실행 프롬프트

## 문서 목적

이 문서는 `NewHyOn Player`의 Windows Player 1차 구현을 실제 코드 작업으로 시작할 때 바로 사용할 수 있는 **최종 실행 프롬프트와 체크리스트**를 제공한다.

이 문서는 아래 문서를 바탕으로 하지만, 실제 구현 시작 시에는 이 문서의 지시를 우선한다.

- `docs/windows-mpv-seamless-player-phase1-plan.md`

핵심 우선순위는 다음과 같다.

- `ScrollText`, `WelcomeBoard`는 1차 구현에서 완전 제거한다.
- Media 메인 재생 경로는 `MPV` 중심으로 유지한다.
- 구현은 부분 시범 적용이 아니라 상용 서비스 가능한 완성형 cut-over를 목표로 한다.
- 복잡한 분리보다 실제 동작하는 단순한 구조를 우선한다.
- 기존 fallback 경로에 의존하지 않는다.

---

## 코드 구현용 최종 실행 프롬프트

```md
현재 `NewHyOn Player`의 Windows Player 코드를 **현재 시점의 실제 코드 기준으로 다시 모두 읽고**, 1차 구현 범위를 아래 기준으로 **끝까지 실제 동작하는 코드로 구현하라.**

중요:
- 추측하지 말고 반드시 현재 코드를 다시 읽고 그 구조를 기준으로 수정한다.
- 문서 수정이 아니라 **실제 코드 구현**이 목적이다.
- 중간 확인 요청 없이, 가장 합리적인 방향으로 판단하여 끝까지 진행한다.
- 목표는 “부분 적용”이 아니라 **1차만으로 상용 서비스 가능한 완성형 cut-over**다.
- fallback은 넣지 않는다. 처음부터 메인 실행 경로가 새 기준으로 동작하게 만든다.
- 응답과 작업은 항상 한국어 기준으로 한다.

---

## 이번 작업의 최우선 목표

`ScrollText`, `WelcomeBoard`는 **1차 구현에서 완전 제거**한다.

이 말의 의미는 다음과 같다.

1. 실행 중 `ScrollTextPlayWindow`, `WelcomeBoardWindow`가 생성되지 않아야 한다.
2. `MainWindow.xaml.cs` 기준 메인 실행 경로에서 두 기능의 표시, 배치, 활성화, z-order 처리, 이동 처리, 타이밍 처리 코드가 제거되어야 한다.
3. 페이지 전환, 스케줄 전환, sync, on-air/off-air, media 재생은 **두 기능 없이도 정상 동작**해야 한다.
4. 기존 데이터에 `ScrollText` 또는 `WelcomeBoard` 타입 요소가 있어도 플레이어는 멈추지 않아야 하며, **해당 요소는 재생 대상에서 제외하고 Media 중심으로 계속 동작**해야 한다.
5. 이번 1차 범위에서는 두 기능을 “나중에 다시 붙이기 위한 유지 대상”으로 보지 않는다. **메인 서비스 경로에서 제거 대상**으로 본다.

---

## 반드시 지켜야 할 구현 원칙

### 1. 현재 코드 기준 분석 후 수정
반드시 먼저 아래 파일들을 다시 읽고, 실제 호출 경로와 영향 범위를 확인한 뒤 수정한다.

- `Player/Windows/NewHyOn_Player/MainWindow.xaml`
- `Player/Windows/NewHyOn_Player/MainWindow.xaml.cs`
- `Player/Windows/NewHyOn_Player/ContentsPlayWnd/ContentsPlayWindow.xaml.cs`
- `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml.cs`
- `Player/Windows/NewHyOn_Player/Services/ScheduleEvaluator.cs`
- `Player/Windows/NewHyOn_Player/Services/UdpSyncService.cs`
- `Player/Windows/NewHyOn_Player/Services/OnAirService.cs`
- `Player/Windows/SharedModels/PageModels.cs`
- `Player/Windows/SharedModels/PlayerModels.cs`
- `Player/Windows/SharedModels/Enums.cs`
- `Player/Windows/NewHyOn_Player/DataClass/DataShop.cs`

필요하면 실제 참조되는 추가 파일도 계속 읽고, 읽은 결과를 기준으로만 수정한다.

### 2. 구조는 단순해야 한다
- 복잡한 추상화보다 지금 코드에서 안정적으로 cut-over 되는 구조를 우선한다.
- 억지로 큰 설계 분리를 벌이지 말고, 실제 동작 경로를 단순하게 정리한다.
- 현재 1차 핵심은 `Media 재생 경로 안정화 + ScrollText/WelcomeBoard 제거`다.

### 3. MPV 중심 재생 경로 유지
- Media 메인 재생 경로는 `MPV` 기준으로 정리한다.
- `ScrollText`, `WelcomeBoard` 제거 때문에 Media 재생이 흔들리면 안 된다.
- 이미지/영상/페이지 전환 흐름은 제거 작업 이후에도 깨지지 않아야 한다.

### 4. fallback 금지
- 기존 제거 대상 기능을 조건부로 살려두는 우회 경로를 두지 않는다.
- “임시 유지”, “차후 사용”, “일단 남겨두고 미사용” 식의 메인 경로 의존을 만들지 않는다.
- 메인 실행 경로는 수정 후 하나의 명확한 경로여야 한다.

---

## 이번 작업에서 실제로 제거해야 하는 것

현재 코드 기준으로 아래 범주를 점검하고 제거/분리하라.

### A. 메인 실행 경로
- `ScrollTextPlayWindow` 생성
- `WelcomeBoardWindow` 생성
- 리스트 보관
- 표시/숨김
- 위치 이동
- 활성화
- 페이지 전환 시 갱신
- z-order 처리
- tick 또는 타이밍 연동
- sync/on-air/page switch 중 연계 호출

### B. 페이지 요소 처리 분기
- `DisplayType.ScrollText`
- `DisplayType.WelcomeBoard`
- 해당 타입 분기에서 호출되는 update/display 함수
- 관련 인덱스/카운터/윈도우 limit 처리

### C. 런타임 호환 정책
- 기존 페이지 데이터에 해당 타입이 있어도 플레이어는 죽지 않아야 한다.
- 가장 합리적인 정책은:
  - `Media`만 정상 재생
  - `ScrollText`, `WelcomeBoard` 요소는 무시
  - 로그에는 제외 사실을 남길 수 있음
- 페이지 전체를 스킵하지 말고, 제거 대상 요소만 제외하는 방향을 우선한다.

### D. 문서/코드 일치
- 이미 수정한 1차 계획 문서와 실제 코드 방향이 어긋나지 않게 맞춘다.
- 문서가 제거라고 되어 있는데 코드가 아직 유지 구조이면 안 된다.

---

## 데이터/호환 정책

이번 1차에서는 아래 정책으로 고정한다.

1. `DisplayType.ScrollText`, `DisplayType.WelcomeBoard`는 **입력 데이터에 남아 있을 수 있다**.
2. 그러나 플레이어 런타임은 이를 **재생 대상에서 제외**한다.
3. 서버/저장 모델/과거 데이터 호환 때문에 enum 자체를 즉시 삭제하는 것이 연쇄 영향이 크다면,
   **enum/계약은 유지할 수 있지만 실행 경로에서는 완전히 배제**한다.
4. 반대로 현재 코드상 안전하게 정리 가능하면, 메인 프로젝트 내부 분기와 의존은 적극 제거한다.
5. 중요한 것은 “데이터 호환”보다 “메인 플레이어가 안정적으로 동작하는 것”이다.

즉:
- **실행 경로 제거가 최우선**
- 데이터 계약 삭제는 영향 범위를 보고 합리적으로 판단
- 단, 어떤 경우에도 메인 실행 경로에서 해당 기능이 동작하면 안 된다

---

## 완료 기준

아래를 만족해야 이번 작업이 완료다.

1. `ScrollTextPlayWindow`, `WelcomeBoardWindow`가 메인 실행 흐름에서 더 이상 생성/사용되지 않는다.
2. `MainWindow.xaml.cs` 기준 페이지 전환 로직에서 두 기능의 갱신/표시/활성화 경로가 제거된다.
3. `DisplayType.ScrollText`, `DisplayType.WelcomeBoard` 데이터가 들어와도 플레이어는 죽지 않고 `Media` 중심으로 계속 동작한다.
4. Media 재생, 페이지 전환, 스케줄 전환, sync, on-air/off-air 흐름이 제거 후에도 유지된다.
5. 빌드가 통과한다.
6. 제거 후 남은 dead path, 불필요한 필드, 카운터, 리스트, 호출이 정리된다.
7. 변경 결과를 코드 기준으로 다시 읽어, 제거 대상 기능이 메인 경로에 남아 있지 않음을 확인한다.

---

## 작업 순서

1. 현재 코드 다시 읽기
2. `ScrollText`, `WelcomeBoard`의 실제 실행 경로와 참조 범위 파악
3. 메인 경로 제거 전략 확정
4. 코드 수정
5. 영향 범위 추가 정리
6. 빌드 검증
7. 수정 코드 재검토
8. 변경 사항 / 수정 파일 / 검증 결과 / 남은 이슈 보고

---

## 구현 시 추가 지침

- `MainWindow`에 남아 있는 제거 대상 리스트/카운터/초기화 코드가 있으면 함께 정리한다.
- 단순히 호출만 주석 처리하지 말고, **실제 실행 흐름이 깔끔하게 읽히도록 코드 구조를 정리**한다.
- 예외를 삼키지 말고, 제거 과정에서 필요한 로그나 방어 처리는 명확히 둔다.
- 코드를 수정할 때는 현재 시점의 코드를 다시 읽고 그것을 기준으로 한다.
- 문제가 발생하면 먼저 코드를 의심하고 실제 호출 순서를 추적한다.
- 최종 결과는 “돌아는 가지만 지저분한 상태”가 아니라, **서비스 가능한 명확한 실행 경로**여야 한다.

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

### 1. 사전 분석

- [ ] `MainWindow.xaml.cs`에서 `ScrollTextPlayWindow`, `WelcomeBoardWindow` 생성/보관/표시/이동/활성화 경로를 모두 찾는다.
- [ ] `PlayPage()`, `PopPage()`, `TickTask()` 등 페이지 전환 핵심 경로를 다시 읽는다.
- [ ] `DisplayType.ScrollText`, `DisplayType.WelcomeBoard` 분기 위치를 모두 찾는다.
- [ ] `ContentsPlayWindow.xaml.cs`의 현재 Media 재생 경로가 제거 작업 후에도 유지 가능한지 확인한다.
- [ ] `ScheduleEvaluator`, `UdpSyncService`, `OnAirService`에서 제거 대상 기능과 직접 연결된 경로가 있는지 확인한다.
- [ ] 데이터 계약과 런타임 실행 경로를 분리해서 다룰 지점을 정리한다.

### 2. 제거 대상 경로 정리

- [ ] `ScrollTextPlayWindow` 리스트, 카운터, limit, 초기화 코드를 정리한다.
- [ ] `WelcomeBoardWindow` 리스트, 카운터, limit, 초기화 코드를 정리한다.
- [ ] 페이지 전환 시 두 기능을 갱신하는 호출을 제거한다.
- [ ] 두 기능의 위치 이동 및 z-order 처리 코드를 제거한다.
- [ ] 두 기능 활성화/비활성화/표시 상태 제어 코드를 제거한다.

### 3. 페이지 요소 처리 정책 반영

- [ ] 페이지 요소 처리에서 `DisplayType.Media`만 메인 재생 대상으로 유지한다.
- [ ] `ScrollText`, `WelcomeBoard` 요소는 런타임에서 안전하게 무시하도록 정리한다.
- [ ] 제거 대상 요소가 있어도 페이지 전체가 실패하지 않도록 방어한다.
- [ ] 필요 시 제외 로그를 남기되, 재생 흐름을 막지 않게 한다.

### 4. Media 경로 안정성 유지

- [ ] 제거 작업 후에도 기존 Media 재생 경로가 깨지지 않는지 확인한다.
- [ ] 이미지/영상 전환, 페이지 전환, 스케줄 전환 흐름이 계속 동작하는지 확인한다.
- [ ] sync prepare/commit 경로가 제거 대상 기능 없이도 유지되는지 확인한다.
- [ ] on-air/off-air 전환이 제거 작업과 충돌하지 않는지 확인한다.

### 5. 코드 정리

- [ ] dead code, 불필요한 필드, 사용하지 않는 함수 호출을 정리한다.
- [ ] 단순 주석 처리 대신 메인 실행 경로가 명확히 읽히도록 정리한다.
- [ ] 데이터 호환 때문에 enum/모델을 남기더라도, 런타임 경로는 완전히 분리한다.
- [ ] 관련 문서와 실제 코드 방향이 어긋나지 않는지 확인한다.

### 6. 검증

- [ ] 솔루션 또는 대상 프로젝트 빌드가 통과한다.
- [ ] 제거 후 `ScrollTextPlayWindow`, `WelcomeBoardWindow`가 메인 경로에서 더 이상 생성되지 않는지 확인한다.
- [ ] `DisplayType.ScrollText`, `DisplayType.WelcomeBoard` 데이터가 있어도 플레이어가 죽지 않는지 확인한다.
- [ ] Media 재생, 페이지 전환, 스케줄 전환, sync, on-air/off-air가 유지되는지 확인한다.
- [ ] 변경 후 관련 코드를 다시 읽어 제거 대상 기능이 메인 경로에 남아 있지 않음을 확인한다.

---

## 구현 완료 후 보고 체크리스트

- [ ] 변경 사항을 기능 단위로 요약한다.
- [ ] 실제 수정 파일 목록을 빠짐없이 적는다.
- [ ] 빌드/정적 검토/실행 검증 결과를 구분해 적는다.
- [ ] 남은 리스크나 후속 작업이 있으면 숨기지 않고 적는다.

