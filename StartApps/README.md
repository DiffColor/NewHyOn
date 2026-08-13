# StartApps

> Windows에서 반복적으로 실행하는 앱을 즉시/순차 영역으로 나누어 관리하고, 단일 EXE로 배포할 수 있는 WPF 런처입니다.

![StartApps preview](docs/images/startapps-preview.png "UI Preview") <!-- 필요 시 실제 스크린샷으로 교체 -->

## ✨ 핵심 특징
- **즉시 실행 영역**: 여러 앱을 동시에 실행하고 상태를 한 눈에 확인합니다.
- **순차 실행 영역**: 카드 좌상단 순번에 맞춰 하나씩 실행되며, 이전 단계가 끝나야 다음 단계가 시작됩니다.
- **FTP 카드 설정**: FileZilla Server 설정(계정, 비밀번호, 포트, 권한 등)을 카드별로 저장하고 실행 시 XML에 자동 반영합니다.
- **NTP 서버 카드**: 프로젝트의 `ntpserver.zip`을 내장하고 필요한 경우 다른 기본 앱과 동일하게 자동 압축 해제합니다.
- **드래그 & 드롭 구성**: 실행 영역 간 이동, 순서 변경, 항목 삭제를 마우스로 처리합니다.
- **트레이 아이콘**: 최소화해도 백그라운드에서 동작하며 트레이에서 즉시 복구할 수 있습니다.
- **단일 파일 배포**: Self-contained, PublishSingleFile 옵션을 사용해 의존성 없는 단일 EXE 생성.

## 📁 폴더 구조
```
StartApps/
├─ Assets/                 # 아이콘 및 정적 리소스
├─ Services/               # AppManager, AppDependencyService 등 실행 로직
├─ ViewModels/             # MVVM ViewModel 계층
├─ Views/                  # MainWindow 및 설정 다이얼로그
├─ publish-single-file.bat # 단일 EXE 게시 스크립트
├─ StartApps.csproj
└─ .github/workflows/      # CI/CD 파이프라인 (release.yml 등)
```

## 🚀 빠른 시작
```bash
# 1) 의존성 복원
 dotnet restore

# 2) 디버그 빌드
 dotnet build --configuration Debug

# 3) 실행
dotnet run --project StartApps.csproj
```

### 프로필별 실행
`StartApps`는 실행 인자 또는 실행 파일명으로 프로필을 구분합니다.

```bash
# 매니저용
dotnet run --project StartApps.csproj -- --profile manager

# 플레이어용
dotnet run --project StartApps.csproj -- --profile player
```

또는 게시 후 실행 파일명을 아래처럼 나눠도 자동 인식합니다.

- `StartApps.Manager.exe`
- `StartApps.Player.exe`

프로필이 달라지면 다음이 분리됩니다.
- 앱 제목/트레이 표시명
- `%LocalAppData%` 저장 폴더
- 등록 앱 목록 DB
- 내장 의존 앱 추출 경로

## 📦 단일 EXE 게시
### CLI 직접 실행
```bash
dotnet publish StartApps.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  /p:PublishSingleFile=true \
  /p:PublishReadyToRun=true \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:EnableCompressionInSingleFile=true
```
생성 위치: `bin/Release/net10.0-windows7.0/win-x64/publish/StartApps.exe`

### 배치 스크립트 사용 (Windows CMD)
```cmd
publish-single-file.bat
```

배치 실행 후 아래 3개가 생성됩니다.
- `StartApps.exe`
- `StartApps.Manager.exe`
- `StartApps.Player.exe`

## 🔁 GitHub Actions (release.yml)
태그(`v*`)를 푸시하면 워크플로가 실행되어 다음을 수행합니다.
1. .NET 10 SDK 기반으로 솔루션 빌드
2. 단일 EXE 생성 후 ZIP으로 압축
3. 커밋 로그로 체인지로그를 만들고, 한국 시간 정보를 포함한 릴리즈 본문 작성
4. ZIP 파일을 아티팩트 및 릴리즈 자산으로 업로드

## 🤝 기여
이슈나 PR로 의견을 남겨 주세요. 버그 제보, 기능 제안, UI 개선 모두 환영입니다.

## 📝 라이선스
프로젝트 루트의 라이선스 파일(예: MIT)을 참고하세요.
