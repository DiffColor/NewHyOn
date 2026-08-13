# QUBER NTP 설정 앱

QUBER Android signage 장비의 **QUBER Signage Agent AIDL API**를 사용해 시스템 NTP 서버 주소를 조회하고 변경하는 최소 앱입니다.

## QUBER API 계약

- 서비스 action: `net.quber.qubersignageagent.QUBER_AGENT_SERVICE`
- 서비스 package: `net.quber.qubersignageagent`
- NTP 서버 읽기: `cmdCode=211046`
  - 성공 응답: `resultCode=2000`, `params.server`
- NTP 서버 설정: `cmdCode=213028`
  - 요청: `params.server`
  - QUBER Agent가 값을 저장한 직후 장비를 재부팅합니다.

앱은 `Settings.Global`을 직접 쓰지 않습니다. QUBER Agent API만 사용합니다.

## 빌드

프로젝트는 자체 Gradle wrapper를 포함합니다. QUBER Agent service는 장비에서
`exported=true`이고 별도 bind permission이 없으므로 API 사용을 위해 platform signing이
필요하지 않습니다. 로컬 debug/release와 GitHub Actions 산출물은 기존 QUBER 앱과 같은 QUBER platform key로 서명됩니다.

```bash
cd Player/Android/Quber/QuberNtpSettings
export ANDROID_HOME="$HOME/Library/Android/sdk"
export JAVA_HOME=$(/usr/libexec/java_home -v 11)
./gradlew assembleDebug assembleRelease
```

산출물:

```text
app/build/outputs/apk/debug/app-debug.apk
app/build/outputs/apk/release/app-release.apk
```

## 사용

1. 앱을 실행하면 QUBER Agent API로 현재 NTP 서버를 읽습니다.
2. IPv4 주소 또는 호스트 이름을 입력합니다. 예: `192.168.50.240`.
3. `NTP 서버 적용`을 누릅니다.
4. 재부팅 확인 후 `적용하고 재부팅`을 누릅니다.
5. QUBER Agent가 서버 주소를 저장하고 장비를 즉시 재부팅합니다.
6. Android의 `자동 날짜 및 시간`이 켜져 있어야 시스템 시간이 동기화됩니다.

`Android 날짜 및 시간 설정 열기` 버튼은 자동 날짜 및 시간 설정 화면을 엽니다.
