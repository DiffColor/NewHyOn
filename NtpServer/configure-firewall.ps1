param(
    [string]$RuleName = 'NtpServer',
    [int]$Port = 123,
    [string]$RemoteAddress = 'Any',
    [switch]$IncludePublic
)

$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw '관리자 권한 PowerShell에서 실행해야 합니다.'
}
if ($Port -lt 1 -or $Port -gt 65535) {
    throw "유효하지 않은 UDP 포트입니다: $Port"
}

$profiles = if ($IncludePublic) { 'Domain,Private,Public' } else { 'Domain,Private' }
$existingRules = @(Get-NetFirewallRule -DisplayName $RuleName -ErrorAction SilentlyContinue)
$existingRuleStates = @($existingRules | ForEach-Object {
    @{
        Name = $_.Name
        Enabled = $_.Enabled.ToString()
    }
})
$candidateRule = New-NetFirewallRule `
    -DisplayName $RuleName `
    -Direction Inbound `
    -Action Allow `
    -Enabled False `
    -Protocol UDP `
    -LocalPort $Port `
    -RemoteAddress $RemoteAddress `
    -Profile $profiles

if (-not $candidateRule) {
    throw '새 방화벽 규칙을 생성했지만 결과 객체를 확인하지 못했습니다.'
}

try {
    if ($existingRules.Count -gt 0) {
        $existingRules | Disable-NetFirewallRule
    }
}
catch {
    $rollbackErrors = @()
    foreach ($state in $existingRuleStates) {
        try {
            Set-NetFirewallRule -Name $state.Name -Enabled $state.Enabled -ErrorAction Stop | Out-Null
        }
        catch {
            $rollbackErrors += "기존 규칙 $($state.Name) 상태 복구 실패: $($_.Exception.Message)"
        }
    }
    try {
        $candidateRule | Remove-NetFirewallRule -ErrorAction Stop
    }
    catch {
        $rollbackErrors += "비활성 후보 규칙 $($candidateRule.Name) 제거 실패: $($_.Exception.Message)"
    }
    $suffix = if ($rollbackErrors.Count -gt 0) { " rollback 오류: $($rollbackErrors -join '; ')" } else { '' }
    throw "기존 방화벽 규칙을 안전하게 비활성화하지 못해 새 규칙 적용을 취소했습니다.$suffix"
}

try {
    $candidateRule | Enable-NetFirewallRule -ErrorAction Stop
}
catch {
    $activationError = $_.Exception.Message
    $rollbackErrors = @()
    try {
        $candidateRule | Disable-NetFirewallRule -ErrorAction Stop
    }
    catch {
        $rollbackErrors += "후보 규칙 $($candidateRule.Name) 비활성화 실패: $($_.Exception.Message)"
    }
    foreach ($state in $existingRuleStates) {
        try {
            Set-NetFirewallRule -Name $state.Name -Enabled $state.Enabled -ErrorAction Stop | Out-Null
        }
        catch {
            $rollbackErrors += "기존 규칙 $($state.Name) 상태 복구 실패: $($_.Exception.Message)"
        }
    }
    try {
        $candidateRule | Remove-NetFirewallRule -ErrorAction Stop
    }
    catch {
        $rollbackErrors += "후보 규칙 $($candidateRule.Name) 제거 실패: $($_.Exception.Message)"
    }
    $suffix = if ($rollbackErrors.Count -gt 0) { " rollback 오류: $($rollbackErrors -join '; ')" } else { '' }
    throw "후보 방화벽 규칙 활성화 실패: $activationError.$suffix"
}

try {
    if ($existingRules.Count -gt 0) {
        $existingRules | Remove-NetFirewallRule
    }
}
catch {
    Write-Warning '새 제한 규칙은 활성 상태입니다. 제거하지 못한 기존 규칙은 비활성 상태로 남아 있으므로 수동 정리하십시오.'
}

Write-Host "방화벽 허용 완료: UDP $Port ($profiles, remote=$RemoteAddress)"
if (-not $IncludePublic) {
    Write-Host 'Public 프로필은 의도적으로 허용하지 않았습니다. 폐쇄망 NIC가 Public이면 -IncludePublic을 명시하십시오.'
}
