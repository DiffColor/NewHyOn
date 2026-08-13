param(
    [int]$Port = 123,
    [string]$RemoteAddress = 'Any',
    [switch]$IncludePublic
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$statePath = Join-Path $projectDir 'host-state.json'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw '관리자 권한 PowerShell에서 실행해야 합니다.'
}
if ($Port -lt 1 -or $Port -gt 65535) {
    throw "유효하지 않은 UDP 포트입니다: $Port"
}

$windowsTime = Get-Service W32Time -ErrorAction SilentlyContinue
$createdState = -not (Test-Path $statePath)
$firewallConfigured = $false
if ($createdState) {
    @{
        W32TimeExists = $null -ne $windowsTime
        W32TimeStatus = if ($windowsTime) { $windowsTime.Status.ToString() } else { '' }
        W32TimeStartType = if ($windowsTime) { $windowsTime.StartType.ToString() } else { '' }
    } | ConvertTo-Json | Set-Content -Path $statePath -Encoding UTF8
}

try {
    if ($Port -eq 123 -and $windowsTime) {
        if ($windowsTime.Status -ne 'Stopped') {
            Stop-Service W32Time -Force
        }
        Set-Service W32Time -StartupType Disabled
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $owners = @(Get-NetUDPEndpoint -LocalPort $Port -ErrorAction SilentlyContinue)
        if ($owners.Count -eq 0) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    if ($owners.Count -gt 0) {
        $details = $owners | ForEach-Object {
            $process = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
            "PID=$($_.OwningProcess) Process=$($process.ProcessName) Address=$($_.LocalAddress)"
        }
        throw "UDP $Port 점유를 해제하지 못했습니다: $($details -join '; ')"
    }

    $firewallScript = Join-Path $projectDir 'configure-firewall.ps1'
    & $firewallScript -Port $Port -RemoteAddress $RemoteAddress -IncludePublic:$IncludePublic
    $firewallConfigured = $true
    Write-Host "호스트 준비 완료: UDP $Port 사용 가능"
    Write-Host "원래 Windows Time 설정 백업: $statePath"
    Write-Host '이 호스트의 시각은 RTC/GNSS 또는 관리 절차로 정확하게 유지해야 합니다.'
}
catch {
    if ($createdState -and (Test-Path $statePath)) {
        & (Join-Path $projectDir 'restore-host.ps1') -KeepFirewallRules:(-not $firewallConfigured) -KeepNtpProcesses
    }
    throw
}