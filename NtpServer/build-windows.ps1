param(
    [ValidateSet('amd64', 'arm64')]
    [string]$Architecture = 'amd64',
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $projectDir 'bin'
$outputPath = Join-Path $outputDir 'NtpServer.exe'

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
Push-Location $projectDir
try {
    go test ./...
    if ($LASTEXITCODE -ne 0) { throw 'go test failed' }

    $env:CGO_ENABLED = '0'
    $env:GOOS = 'windows'
    $env:GOARCH = $Architecture
    go build -trimpath -ldflags "-s -w -X main.version=$Version" -o $outputPath .
    if ($LASTEXITCODE -ne 0) { throw 'go build failed' }

    Write-Host "Built: $outputPath"
    Get-FileHash -Algorithm SHA256 $outputPath
}
finally {
    Pop-Location
}
