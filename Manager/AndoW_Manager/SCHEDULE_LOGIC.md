# 스케줄 로직 상세 설명

이 문서는 현재 저장소에 존재하는 스케줄 관련 로직을 코드 기준으로 정리한 것이다. 실제 재생 판단 로직(플레이어 구현)은 이 데이터 구조와 검증 규칙을 그대로 해석해야 한다.

## 1) 범위와 참고 파일
- 특별 스케줄
  - `DataManager/SpecialScheduleInfoManager.cs`
  - `Pages/Page5.xaml`
  - `Pages/Page5.xaml.cs`
  - `DataClass/SpecialScheduleViewData.cs`
  - `UserControl/SpecialCtrl.xaml.cs`
  - `SubWindow/EditSpecialWindow.xaml.cs`
- 주간 방송 시간(Weekly schedule)
  - `DataManager/WeeklyInfoManagerClass.cs`
  - `SubWindow/EditOnAirTimeWindow.xaml.cs`
  - `Page3UserControl/WeekSchInfoElement.xaml.cs`
- 그룹/플레이어 보조
  - `DataManager/PlayerGroupManager.cs`
  - `DataClass/PlayerGroupClass.cs`
  - `DataManager/PlayerInfoManager.cs`
- 저장/전송
  - `TurtleTools/RethinkDbManagerBase.cs`
  - `TurtleTools/SelectUSBWindow.xaml.cs`
- 페이지 스케줄 UI(정의 클래스는 저장소에 없음)
  - `Page1UserControl/PageScheduleElement.xaml.cs`
  - `SubWindow/EditPageScheduleWindow.xaml.cs`

## 2) 스케줄 종류 개요
- 특별 스케줄: 플레이어/그룹에 대해 특정 기간+요일+시간에 플레이리스트를 재생하도록 등록.
- 주간 방송 시간: 플레이어별로 요일별 방송 가능 시간(시작/종료)과 OnAir 여부를 관리.
- 페이지 스케줄: 페이지 재생 예약/일반 스케줄 UI가 존재하지만, 핵심 데이터 클래스(`ScheduleDataInfoClass`)가 현재 저장소에 없어 상세 저장/판정 로직은 확인 불가.

## 3) 특별 스케줄 데이터 모델
### 3.1 SpecialScheduleInfoClass (특별 스케줄 레코드)
저장 파일: `DataManager/SpecialScheduleInfoManager.cs`

- 식별자
  - `GUID` 필드가 JSON의 `id`로 저장됨.
  - `Id` 프로퍼티는 `GUID` 래핑.
- 대상
  - `PlayerNames`: 실제 대상 플레이어 목록(필수).
  - `GroupNames`: 그룹 선택 시 선택한 그룹명 목록(표시/메타 정보).
  - `LegacyGroupName`: JSON 속성 `"GroupName"`을 로딩할 때 `GroupNames`로 흡수하기 위한 호환 처리.
- 재생 대상
  - `PageListName`: 재생할 플레이리스트 이름.
- 요일 플래그
  - `DayOfWeek1` = 일요일, `DayOfWeek2` = 월요일, ..., `DayOfWeek7` = 토요일.
  - `BuildDays`/`BuildDaysKey`에서는 월→일 순서로 배열/키를 구성함.
- 기간/시간
  - `IsPeriodEnable`: 기간 사용 여부.
  - `PeriodStartYear/Month/Day`, `PeriodEndYear/Month/Day`.
  - `DisplayStartH/M`, `DisplayEndH/M`.
- 기본값
  - 생성자에서 `GUID` 생성.
  - 요일 기본값은 월~금 true, 일/토 false.

### 3.2 SpecialScheduleViewData (리스트 표시용)
저장 파일: `DataClass/SpecialScheduleViewData.cs`

- `GroupNames`, `GroupName`, `TargetPlayers`, `TargetDisplayName` 등 UI 표시용 데이터 보유.
- `StartDateText`, `EndDateText`, `StartTimeText`, `EndTimeText`는 `yyyy-MM-dd`, `HH:mm` 포맷으로 출력됨.

### 3.3 저장 방식 (RethinkDB)
저장 파일: `TurtleTools/RethinkDbManagerBase.cs`

- 각 매니저는 테이블 이름을 클래스명으로 사용.
  - 특별 스케줄: `SpecialScheduleInfoManager` 테이블.
- `Upsert`는 `conflict=replace`로 동작하며 동일 `id`는 덮어씀.

## 4) 특별 스케줄 등록 흐름 (Page5)
저장 파일: `Pages/Page5.xaml.cs`

### 4.1 입력 UI
- 대상 토글: `GroupRadio`(그룹) / `SingleRadio`(개별).
- 그룹/플레이어 선택 리스트:
  - `TargetListBox`(그룹), `TargetPlayerListBox`(플레이어)
  - 검색 필터: `GroupFilterTextBox`, `PlayerFilterTextBox`
  - 전체 선택 체크박스는 **현재 보이는 항목만** 선택/해제.
- 일정 입력:
  - `StartDatePicker`, `EndDatePicker`
  - `StartTimePicker`, `EndTimePicker`
  - 요일 토글: `DayToggleStack` (월→일 순서)
- 플레이리스트 선택: `SelectPlaylistCombo`

### 4.2 등록 시 검증
`AddSchduleBtn_Click`에서 다음 순서로 검증:
1. 그룹/플레이어가 하나 이상 선택되어야 함.
2. 플레이리스트 선택 필수.
3. 그룹 모드일 때 선택된 그룹에 플레이어가 없으면 등록 불가.
4. 시작/종료 DateTime 생성 실패 시 중단.
5. 종료가 시작보다 이르면 종료 날짜를 +1일.
6. 종료 시간이 현재보다 이전이면 등록 불가.
7. `TryGetScheduleInput`에서 날짜/시간/요일 입력 확인.

### 4.3 일정 입력 보정
- `StartDatePicker_SelectedDateChanged` / `EndDatePicker_SelectedDateChanged`:
  - 시작 날짜 > 종료 날짜이면 종료 날짜를 시작 날짜로 보정.
  - 같은 날이면 `CheckSelectedTime()` 호출.
- `CheckSelectedTime()`:
  - 동일 날짜에서 종료 시간이 시작 시간보다 이르면 종료 날짜를 +1일로 보정.
- `TryGetScheduleInput()`:
  - `endDate == startDate && endTime < startTime`이면 `endDate += 1일`.

### 4.4 저장 로직 (AddSpecialSchedule)
`AddSpecialSchedule`에서 그룹/개별 모드가 분리됨.

#### 그룹 모드
1. 선택 그룹명 중복 제거.
2. 각 그룹의 플레이어 목록을 합집합으로 생성.
   - 비어 있는 그룹이 있으면 `addedAll = false`.
3. `BuildScheduleInfo`로 새 스케줄 생성.
4. `newSchedule.GroupNames = 선택 그룹 목록`.
5. `FilterOverlappedPlayers`로 기존 일정과 겹치는 플레이어 제거.
6. 남은 플레이어가 있으면 `PlayerNames`에 저장하고 Upsert.
7. `updateschedule` 메시지를 대상 플레이어들에게 전송.

#### 개별 모드
1. 선택 플레이어명 중복 제거.
2. `BuildScheduleInfo`로 새 스케줄 생성.
3. `FilterOverlappedPlayers`로 겹치는 플레이어 제거.
4. 남은 플레이어가 있으면 `PlayerNames`에 저장하고 Upsert.
5. `updateschedule` 메시지 전송.

#### 공통 사항
- 스케줄 **1건에 여러 플레이어**가 묶여 저장됨.
- `GroupNames`는 **메타 정보**, 실 대상은 `PlayerNames`.
- 등록 후 `ReloadSpecials()`로 리스트 갱신.

## 5) 특별 스케줄 중복/겹침 판정
저장 파일: `DataManager/SpecialScheduleInfoManager.cs`, `Pages/Page5.xaml.cs`

### 5.1 중복 제외 흐름
- `FilterOverlappedPlayers` → `HasOverlappingSchedule` → `schedule.CheckOverlappedPeriod(candidate)`
- 특정 플레이어가 겹치는 스케줄을 가진다면 해당 플레이어만 제외됨.

### 5.2 CheckOverlappedPeriod 로직
1. 날짜 문자열 `yyyyMMdd`와 시간 문자열 `HHmm`를 int로 변환.
2. **둘 다 기간 설정**이면 날짜 겹침 확인 후 시간 겹침 확인.
3. 하나라도 기간 미설정이면 날짜 겹침 없이 시간/요일만 확인.

### 5.3 날짜 겹침 (CheckIsDateOverlapped)
- 시작/종료 4개의 날짜를 리스트에 담고, 중복이 있으면 바로 겹침으로 판단.
- 중복이 없으면 정렬 후 순서를 비교.
- 현재 구현은 **정렬된 순서가 `thisStart, thisEnd, newStart, newEnd`일 때만 비겹침(false)**.
  - 그 외 순서는 모두 겹침(true)으로 처리.

### 5.4 요일+시간 겹침
`IsCheckOverlappingTimeOnDayofWeek`:
- 요일별로 두 스케줄의 해당 요일 플래그가 모두 true일 때만 시간 겹침을 검사.

`CheckIsTimeOverlapped`:
- `HHmm`을 분 단위로 변환.
  - 유효하지 않으면 겹침으로 처리(true).
- `start == end`이면 **24시간 전체 범위**로 간주.
- `end < start`이면 **종료 시간을 +24시간**으로 확장하여 한 구간으로 비교.
- 구간 겹침은 `[start, end]` 포함 비교로 판정.

## 6) 특별 스케줄 리스트 구성 및 필터
### 6.1 리스트 구성 (BuildSpecialViewItems)
저장 파일: `Pages/Page5.xaml.cs`

- 각 `SpecialScheduleInfoClass`마다 개별 `SpecialScheduleViewData` 생성.
- `PlayerNames`와 `GroupNames`는 중복 제거 후 정렬.
- 표시 이름:
  - 그룹이 있으면 `"그룹: A, B"` 형식.
  - 그룹이 없으면 `"플레이어: P1, P2"` 형식.
- 정렬 키:
  - 그룹이 있으면 첫 번째 그룹명.
  - 그룹이 없으면 첫 번째 플레이어명.
- 정렬 순서: `GroupName` → `StartDate` → `StartTime` → `Playlist`.

### 6.2 ScheduleKey
`BuildScheduleKey`:
- `PageListName | StartDate | EndDate | StartTime | EndTime | DaysKey`
- 동일한 `ScheduleKey`는 수정 창에서 **같은 스케줄 그룹**으로 취급됨.

### 6.3 필터 (ApplySpecialFilters)
- 날짜: 선택 날짜가 `StartDate~EndDate` 안에 있으면 표시.
  - `StartDate`/`EndDate`가 null이면 날짜 필터는 통과.
- 그룹:
  - `GroupNames`가 있으면 해당 그룹명 포함 여부로 판단.
  - `GroupNames`가 없으면 `Player -> Group` 매핑을 만들어 대상 플레이어가 해당 그룹에 속하는지 확인.
- 플레이어:
  - `TargetPlayers` 목록에 포함 여부로 판단.
- 리스트명: `Playlist`와 일치해야 표시.

## 7) 특별 스케줄 수정 (EditSpecialWindow)
저장 파일: `SubWindow/EditSpecialWindow.xaml.cs`

### 7.1 초기 데이터 구성
- 같은 `ScheduleKey`를 가진 스케줄들을 `sSameData`로 전달받음.
- 그룹 목록: `sSameData`의 `GroupNames`를 모아 중복 제거 후 정렬.
- 플레이어 목록: 스케줄에 들어있는 플레이어를 `PlayerInfoManager.GetOrderedPlayers()` 순서로 정렬.

### 7.2 대상 선택
- 그룹 모드: 선택한 그룹명이 포함된 스케줄만 대상.
- 개별 모드: 선택한 플레이어가 포함된 스케줄만 대상.

### 7.3 중복 확인 및 업데이트
- `HasDuplicateSchedule`로 기존 스케줄과 겹치는지 검사.
  - 대상 스케줄의 GUID는 제외.
  - 동일 플레이어가 포함되면서 시간 겹침이면 중복으로 판단.
- 업데이트 시 `PlayerNames`, `GroupNames`는 **기존 값을 유지**하고 시간/기간/요일/플레이리스트만 갱신.
- 수정 완료 후 대상 플레이어에게 `updateschedule` 전송.

## 8) 특별 스케줄 삭제
저장 파일: `Pages/Page5.xaml.cs`

- 선택 삭제: 체크된 스케줄의 실제 `SpecialScheduleInfoClass`를 모아 삭제.
- 전체 삭제: 전체 스케줄 삭제.
- 만료 삭제: `IsExpiredSchedule`로 **종료일이 오늘보다 이전**인 스케줄 삭제.
- 삭제 후 대상 플레이어에게 `updateschedule` 전송.

## 9) 주간 방송 시간(Weekly schedule)
저장 파일: `DataManager/WeeklyInfoManagerClass.cs`

### 9.1 데이터 모델
- `WeeklyPlayScheduleInfo`:
  - 플레이어 ID/이름 + 요일별 `DaySchedule`.
- `DaySchedule`:
  - 시작/종료 시각(시/분)과 OnAir 여부.
- `WeeklyDayScheduleInfo`:
  - 요일 문자열(`SUN`, `MON`, ...), 시간, OnAir.
  - `CopyData(onlyTime)`로 시간만 복사 가능.

### 9.2 저장/로드 흐름
- `InitPlayerInfoListFromDataTable(playerId, playerName)`:
  - 플레이어 ID로 DB에서 스케줄을 찾고, 없으면 기본값 생성.
  - `BuildWeekList`로 `PIF_WPS_InfoList`(일별 리스트) 구성.
- `SaveWeeklySchedule`:
  - `PIF_WPS_InfoList` 값을 `CurrentSchedule`에 반영하고 Upsert.
- `BuildWeekList`는 요일 순서를 **SUN→MON→...→SAT**로 생성.

### 9.3 UI 연동 (EditOnAirTimeWindow)
저장 파일: `SubWindow/EditOnAirTimeWindow.xaml.cs`

- 창 로드 시 `InitPlayerInfoListFromDataTable` 호출.
- 요일 클릭으로 OnAir 토글:
  - 요일 사각형/텍스트 클릭 시 `IsOnAir` 반전 → 저장.
- 시간 편집:
  - `WeekSchInfoElement`에서 시작/종료 시간 변경 시 이벤트로 반영.
- 전체 적용:
  - “모든 플레이어에 적용” 버튼은 모든 플레이어에게 동일 주간 스케줄 저장.
- 업데이트 버튼은 `updateschedule` 메시지를 플레이어에게 전송.

### 9.4 WeekSchInfoElement 동작
저장 파일: `Page3UserControl/WeekSchInfoElement.xaml.cs`

- 편집/저장 토글 방식으로 시간 변경.
- 변경 시 `EventUpdateWIFD` 이벤트를 통해 상위 윈도우로 전달.
- `IsOnAir` 값에 따라 “방송/방송안함” 표시 변경.

## 10) USB 내보내기와 스케줄 포함
저장 파일: `TurtleTools/SelectUSBWindow.xaml.cs`

- 선택한 플레이리스트를 현재 재생 중인 플레이어 목록을 기준으로 Export.
- 스케줄 파일:
  - `weekly_schedule.bin`: 플레이어별 `WeeklyPlayScheduleInfo` + `WeeklyDayScheduleInfo` 목록.
  - `special_schedule.bin`: 플레이어별 `SpecialScheduleInfoClass` 목록.
- JSON 저장은 `SecureJsonTools.WriteEncryptedJson`으로 암호화됨.

## 11) 플레이어 구현 시 해석 포인트 (코드 기준)
1. 대상 판정은 `PlayerNames` 기준이며 **대소문자 무시** 비교가 기본.
2. 그룹명은 표시/필터용 메타 정보로 쓰이며, 실제 적용은 `PlayerNames`로 판정.
3. 날짜 범위:
   - `IsPeriodEnable == false`면 날짜 조건이 없음.
   - true면 `PeriodStart~PeriodEnd` 범위 내에서만 유효.
4. 요일 매핑:
   - `DayOfWeek1=일`, `DayOfWeek2=월`, ..., `DayOfWeek7=토`.
   - 배열/키는 월→일 순서.
5. 시간 범위:
   - `start == end`면 24시간 전체로 처리.
   - `end < start`는 다음날로 넘어가는 구간으로 처리(종료를 +24시간 확장).
6. 스케줄 1건에 여러 플레이어가 포함될 수 있음.

## 12) 확인 불가/누락된 부분
- `ScheduleDataInfoClass` 정의가 현재 저장소에 없어, 페이지 스케줄의 저장/판정 로직은 확인 불가.
- 이 문서에 없는 우선순위/병합 규칙(특별 스케줄 vs 주간 스케줄)은 **현재 코드 범위에서는 확인되지 않음**.
