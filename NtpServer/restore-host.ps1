param(
    [string]$RuleName = 'NtpServer',
    [switch]$KeepFirewallRules,
    [switch]$KeepNtpProcesses
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$statePath = Join-Path $projectDir 'host-state.json'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw '관리자 권한 PowerShell에서 실행해야 합니다.'
}

if (-not $KeepNtpProcesses) {
    Get-Process NtpServer -ErrorAction SilentlyContinue | Stop-Process -Force
}
if (-not $KeepFirewallRules) {
    Get-NetFirewallRule -DisplayName $RuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
}

if (Test-Path $statePath) {
    $state = Get-Content $statePath -Raw | ConvertFrom-Json
    if ($state.W32TimeExists) {
        $startupType = switch ($state.W32TimeStartType) {
            'Automatic' { 'Automatic' }
            'Manual' { 'Manual' }
            'Disabled' { 'Disabled' }
            default { 'Manual' }
        }
        Set-Service W32Time -StartupType $startupType
        if ($state.W32TimeStatus -eq 'Running') {
            Start-Service W32Time
        }
    }
    Remove-Item $statePath -Force
}

Write-Host 'NtpServer 호스트 설정을 원복했습니다.'