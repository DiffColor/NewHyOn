# NtpServer

폐쇄망에서 Windows 호스트의 시스템 시각을 NTPv3/NTPv4로 배포하는 경량 UDP 서버입니다. StartApps Manager에는 RDB·FTP와 같은 **NtpServer 기본 앱 카드**로 등록됩니다.

> 이 서버는 호스트 시각을 배포할 뿐 호스트 시각을 보정하지 않습니다. 정확한 RTC, GNSS, 제한된 상위 NTP 또는 관리자 기준으로 Windows 호스트 시각을 먼저 유지해야 합니다.

## 실행·배포 구조

- `NtpServer.exe` 하나가 NTP packet serving, Windows Time 충돌 해소, 방화벽 구성 및 원복을 수행합니다.
- 외부 Go runtime, 외부 Go package, PowerShell runtime script가 필요하지 않습니다.
- StartApps 프로젝트의 `ntpserver.zip`에는 Windows x64 NTP EXE가 들어 있으며, Manager 단일 파일에 리소스로 내장됩니다.
- StartApps는 다른 내장 앱과 동일하게 실행 파일이 없을 때 `%LocalAppData%\StartApps.Manager\Apps\NtpServer`에 ZIP을 압축 해제합니다.
- Manager profile에 NTP가 없는 기존 DB는 시작 시 NTP 기본 정의를 추가합니다.
- 활성 NTP 프로세스가 사라지면 StartApps의 1분 process monitor가 기존 시작 경로로 자동 재시작합니다.

## 특성

- Windows x64 단일 EXE, CGO 비활성
- NTPv3/NTPv4 client mode 3 요청에 48바이트 server mode 4 응답
- 요청당 goroutine, unbounded queue/state 및 요청별 로그 없음
- 재사용 수신 버퍼와 UDP 임시 오류 exponential backoff
- malformed/non-client 패킷 drop 및 통계 집계
- 호스트 시각 역보정 시 reference timestamp 재기준화
- 기본 `stratum 10`, reference ID `LOCL`, root dispersion `1s`
- Ctrl+C/SIGTERM에서는 정상 종료 및 최종 통계 기록
- 크기 제한 순환 파일 로그(기본 10 MiB, 백업 3개)
- stdout 쓰기 실패와 독립적인 파일 로그 및 startup fallback 로그

## 빌드

Go 1.23 이상이 설치된 환경에서:

```powershell
./build-windows.ps1
```

생성 파일:

```text
bin/NtpServer.exe
```

배포본을 갱신할 때 `build-windows.ps1`로 CGO 없는 Windows x64 EXE를 만든 뒤, 생성된 `bin/NtpServer.exe` 하나를 `StartApps/ntpserver.zip`에 넣습니다. StartApps 빌드와 publish는 Go 도구chain 없이 프로젝트 소스의 ZIP을 그대로 내장합니다.

## StartApps Manager 기본값

| 설정 | 값 |
|---|---|
| 이름 | `NtpServer` |
| 유형 | `Ntp` |
| 실행 파일 | `ntpserver.zip` 내장 EXE 자동 추출 |
| 인자 | `-listen :123 -firewall-remote-address Any -stratum 10 -ref-id LOCL -root-dispersion 1s -log-interval 10m -log-file logs\ntp-server.log` |
| 작업 디렉터리 | 자동 추출된 `NtpServer` 폴더 |
| 실행 영역 | 즉시/병렬 |
| 활성화 | 켜기 |
| 창 표시 | 끄기 |
| 관리자 권한 | 켜기 |

NtpServer 카드와 설정 화면은 RDB·FTP와 구분되는 전용 `AppType.Ntp`를 사용합니다. enum은 기존 LiteDB 숫자 호환성을 위해 마지막 값으로 추가했습니다.

## Windows host 자동 준비

Windows에서 기본 실행하면 EXE가 서버 socket을 열기 전에 다음 작업을 수행합니다.

1. 관리자 권한 확인
2. Windows Time(`W32Time`)의 원래 startup type과 실행 상태를 `HKLM\SOFTWARE\TurtleLab\NtpServer`에 최초 1회 보존
3. W32Time 중지 및 비활성화
4. UDP 포트가 실제로 반환됐는지 확인
5. `NtpServer` inbound UDP 규칙 생성
6. UDP/123 bind 및 NTP serving 시작

StartApps가 프로세스를 재시작해도 저장된 원래 상태를 덮어쓰지 않으며 setup은 반복 실행 가능합니다.

원복은 관리자 권한으로 다음처럼 실행합니다.

```powershell
NtpServer.exe -restore-host
```

원복은 managed firewall rule을 삭제하고 저장된 W32Time startup type/실행 상태를 복구한 다음 저장 상태를 제거합니다.

`prepare-host.ps1`, `configure-firewall.ps1`, `restore-host.ps1`은 수동 진단·복구용 보조 도구입니다. StartApps NTP 카드의 runtime 의존성이 아닙니다.

## 방화벽 remote address

기본값은 요청대로 `Any`입니다. subnet 제한은 NTP 프로토콜 동작에 필수적이지 않습니다.

- `Any`: 모든 원격 주소에서 해당 Windows inbound UDP/123 규칙에 접근 가능
- `LocalSubnet`: Windows가 판단한 로컬 subnet으로 제한
- CIDR/주소 목록: 지정한 네트워크로 제한

완전히 격리된 폐쇄망에서는 `Any`를 사용할 수 있습니다. 여러 VLAN, guest Wi-Fi, route된 사내망, Public network 또는 인터넷에서 해당 호스트의 UDP/123에 도달할 수 있으면 노출 범위와 NTP reflection/abuse 위험이 커집니다. 운영 경계가 불명확하면 다음처럼 제한합니다.

```text
-firewall-remote-address LocalSubnet
-firewall-remote-address 192.168.0.0/24
```

## 독립 실행

독립 실행 기본 bind는 안전한 로컬 검증용 `127.0.0.1:123`입니다. StartApps Manager 기본 카드는 LAN 제공을 위해 `-listen :123`을 명시합니다.

```powershell
NtpServer.exe -listen :123 -firewall-remote-address Any
```

개발·진단 목적으로 Windows host 자동 준비를 생략하려면:

```powershell
NtpServer.exe -listen 127.0.0.1:40123 -skip-host-setup
```

## 실행 옵션

```text
-listen string                    UDP bind 주소 (기본 127.0.0.1:123)
-firewall-remote-address string   Windows 방화벽 원격 주소 (기본 Any)
-skip-host-setup                  Windows Time·방화벽 자동 준비 생략
-restore-host                     managed 방화벽과 저장된 W32Time 상태 원복 후 종료
-stratum uint                     NTP stratum 1~15 (기본 10)
-ref-id string                    정확히 4자의 printable ASCII reference ID (기본 LOCL)
-root-dispersion duration         추정 호스트 시각 오차 (기본 1s)
-log-interval duration            상태 로그 주기, 0이면 비활성 (기본 1m)
-log-file string                  순환 로그 경로, 빈 값이면 파일 로그 비활성
-no-file-log                      fallback을 포함한 파일 로그 비활성
-log-max-mib int                  로그 파일 하나의 최대 크기 MiB (기본 10)
-log-backups int                  보관할 순환 백업 수 (기본 3)
-version                          버전 출력 후 종료
```

알 수 없는 옵션·잘못된 값·파일 로그 초기화 실패 등 startup 오류는 OS 임시 폴더의 bounded `NtpServer-startup.log`에 기록됩니다. `-no-file-log`는 fallback까지 끕니다.

## 검증

서버가 실행 중인 Windows 호스트에서:

```powershell
./test-client.ps1 -Server 127.0.0.1 -Count 5
```

다른 폐쇄망 PC에서는 서버 LAN IP를 사용합니다.

```powershell
./test-client.ps1 -Server 192.168.0.2 -Count 5
w32tm /stripchart /computer:192.168.0.2 /samples:5 /dataonly
```

## 운영 계약

1. NTP 호스트는 고정 IP를 사용하고 절전·최대 절전 모드를 끕니다.
2. BIOS/UEFI RTC, Windows timezone과 host clock source를 점검합니다.
3. W32Time을 중지하므로 별도 상위 source가 없다면 관리자가 host 시각 편차를 감시·보정합니다.
4. 기본 `stratum 10 / LOCL`은 외부 기준 없이 local clock을 배포한다는 뜻입니다. 실제 품질보다 낮은 stratum이나 dispersion을 설정하지 않습니다.
5. StartApps는 활성 NTP 프로세스를 1분 주기로 확인하며 없으면 자동 재시작합니다. 별도 watchdog 프로세스는 필요하지 않습니다.
6. StartApps의 중지/비활성화는 `Kill(true)`이므로 Go signal handler와 final `stopped` log 실행은 보장되지 않습니다. Windows가 process 종료 시 UDP socket과 file handle을 회수합니다. 이것은 자동 재시작 계약과 별개이며 graceful shutdown이라고 표현하지 않습니다.
7. 요청별 로그는 생성하지 않으며 현재 log와 backup 개수/크기는 제한됩니다.
8. 실제 Android firmware가 사용자 지정 NTP server를 사용해 system clock을 보정하는지는 현장에서 별도 확인해야 합니다.

## 제한

- 상위 NTP/GNSS에 직접 동기화하는 client 기능은 없습니다.
- 인증 NTP/NTS는 지원하지 않습니다.
- 서버 시각 품질은 Windows host system clock 품질을 넘을 수 없습니다.
- 서버는 host의 실제 동기화 여부를 자동 판정하지 않습니다.
