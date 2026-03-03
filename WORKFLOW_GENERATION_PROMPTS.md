# GitHub Actions 워크플로우 생성 프롬프트 모음 (범용)

아래 프롬프트들은 특정 프로젝트명에 종속되지 않도록 일반화된 템플릿입니다.
각 프롬프트를 그대로 LLM에 전달한 뒤, 자리표시자만 실제 경로/명령으로 치환해서 사용하십시오.

## 공통 규칙 (모든 프롬프트에 공통 적용)
- 결과는 설명 없이 **완성된 YAML 코드만** 출력한다.
- YAML 문법 오류가 없어야 한다.
- 잡/스텝 이름은 역할이 명확해야 한다.
- 저장소가 Git LFS를 사용하면 `actions/checkout@v4`에서 `lfs: true`를 반드시 사용한다.
- Windows 빌드에서 레거시 .NET Framework 프로젝트를 다루면 `microsoft/setup-msbuild@v2` + `msbuild-architecture: x86` 조합을 우선 적용한다.
- Release 자산 첨부가 필요하면 `permissions: contents: write`를 설정한다.

---

## 프롬프트 1: 수동 단일 타깃 빌드 워크플로우 생성
아래 요구사항을 만족하는 `.github/workflows/manual-project-build.yml`을 작성해 주세요.

### 요구사항
- `workflow_dispatch` 기반 수동 실행
- 입력값:
  - `project` (choice, required): `<PROJECT_OPTIONS>`
  - `source_ref` (string, required, default `main`)
  - `release_tag` (string, optional, default `""`)
  - `release_name` (string, optional, default `""`)
  - `prerelease` (boolean, optional, default `false`)
- `permissions: contents: write`
- 각 타깃은 `if: ${{ inputs.project == '...' }}`로 분기
- 각 타깃별 빌드 전 준비:
  - 소스 체크아웃(`ref: ${{ inputs.source_ref }}`)
  - 패키지 복원
  - 필요 시 dependencies 복사
- 빌드 후:
  - `actions/upload-artifact@v4`로 산출물 업로드
  - Release 메타데이터 계산 step(`id: release_meta`)
    - `release_tag` 비어있으면 최신 `vX.Y.Z`에서 patch +1 자동 생성
    - `release_name` 비어있으면 저장소 이름 사용
  - `softprops/action-gh-release@v2`로 자산 첨부
- 자리표시자:
  - `<PROJECT_OPTIONS>`
  - `<RESTORE_COMMAND_BY_PROJECT>`
  - `<BUILD_COMMAND_BY_PROJECT>`
  - `<DEPENDENCY_COPY_BY_PROJECT>`
  - `<ARTIFACT_PATH_BY_PROJECT>`

---

## 프롬프트 2: 수동 전체 타깃(일괄) 빌드 워크플로우 생성
아래 요구사항을 만족하는 `.github/workflows/manual-all-build.yml`을 작성해 주세요.

### 요구사항
- `workflow_dispatch` 기반 수동 실행
- 입력값:
  - `source_ref` (string, required, default `main`)
  - `release_tag` (string, optional, default `""`)
  - `release_name` (string, optional, default `""`)
  - `prerelease` (boolean, optional, default `false`)
- `permissions: contents: write`
- `prepare-release` job에서 release 메타데이터를 1회 계산하고 output으로 공유
  - `tag`, `name`, `artifact_suffix`
- 빌드 job은 타깃별로 분리:
  - `build-<target-a>`
  - `build-<target-b>`
  - `build-<target-c>`
- 각 빌드 job은 산출물을 artifact로 업로드하고, release 첨부용 파일(zip/apk 등)도 별도 artifact로 업로드
- `publish-release` job에서 위 release asset artifact들을 다운로드하여 Release에 한 번에 첨부
- `publish-release`는 모든 빌드 job 성공 후 실행
- 자리표시자:
  - `<TARGET_JOB_MATRIX>`
  - `<BUILD_STEPS_BY_TARGET>`
  - `<RELEASE_ASSET_FILE_PATTERN_BY_TARGET>`

---

## 프롬프트 3: 태그 기반 선택 빌드 워크플로우 생성
아래 요구사항을 만족하는 `.github/workflows/tag-build.yml`을 작성해 주세요.

### 요구사항
- 트리거: `push.tags`
- 여러 태그 패턴 예시:
  - `<TARGET_A_TAG_PATTERN>`
  - `<TARGET_B_TAG_PATTERN>`
  - `<TARGET_C_TAG_PATTERN>`
- 각 job은 태그 prefix에 따라 `if: startsWith(github.ref_name, '...')`로 실행 제어
- 각 job은 빌드 후 artifact 업로드
- 필요 시 동일 태그 이름으로 GitHub Release 자산 첨부
- `permissions: contents: write`
- 자리표시자:
  - `<TAG_PATTERNS>`
  - `<IF_CONDITION_BY_JOB>`
  - `<BUILD_STEPS_BY_JOB>`
  - `<ARTIFACT_PATH_BY_JOB>`

---

## 프롬프트 4: 버전 태그(v*) 전체 빌드 워크플로우 생성
아래 요구사항을 만족하는 `.github/workflows/tag-build-all.yml`을 작성해 주세요.

### 요구사항
- 트리거: `push.tags: ['v*']`
- 태그 하나(`v1.2.3`)가 푸시되면 모든 타깃 빌드를 동시에 수행
- 타깃별 빌드 job 분리(Windows/Linux/Android 등)
- 각 빌드 job:
  - 산출물 artifact 업로드
  - Release 첨부용 파일 생성 및 업로드
- `publish-release` job:
  - 모든 빌드 job 결과를 모아서 해당 태그 Release에 일괄 첨부
- `permissions: contents: write`
- 자리표시자:
  - `<ALL_TARGET_BUILD_JOBS>`
  - `<DOWNLOAD_RELEASE_ASSET_ARTIFACT_STEPS>`
  - `<ATTACH_FILES_GLOB>`

---

## 플랫폼별 실무 체크리스트 (선택 반영)
- Windows/.NET Framework:
  - `nuget restore`와 MSBuild 플랫폼(`x64`/`Any CPU`) 정합성 확인
  - 외부 DLL 참조 경로와 dependencies 복사 경로 일치 확인
- Android:
  - `sdkmanager`는 최신 환경에서 JDK 17이 필요할 수 있음
  - Gradle/AGP 호환을 위해 빌드 시 JDK 11이 필요할 수 있음
  - 프로젝트가 특정 NDK 버전을 요구하면 `sdkmanager "ndk;<version>"`로 설치
  - 레거시 프로젝트에서 release lint 실패 시 CI에서만 `-x lintVitalRelease -x lint` 우회 가능
- Release:
  - 동일 태그 재실행 대비 `overwrite_files: true` 고려
  - 산출물 파일명은 `release_tag` 또는 `artifact_suffix` 포함 권장

---

## 빠른 사용 순서
1. 위 4개 프롬프트 중 필요한 유형 선택
2. 자리표시자를 프로젝트 실제 값으로 치환
3. 생성된 YAML을 `.github/workflows/`에 저장
4. 로컬에서 YAML 문법 검증 후 푸시
