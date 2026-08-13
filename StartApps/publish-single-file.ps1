param(
    [ValidateSet("default", "manager", "player")]
    [string]$StartAppsProfile = "default",

    [string]$Configuration = "Release",

    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

function Resolve-DotNet10Sdk {
    $candidates = New-Object System.Collections.Generic.List[string]
    $pathCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $pathCommand) {
        $candidates.Add($pathCommand.Source)
    }

    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
        $programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
        foreach ($path in @(
            (Join-Path $programFiles "dotnet\dotnet.exe"),
            (Join-Path $programFiles "dotnet\x64\dotnet.exe"),
            (Join-Path $programFilesX86 "dotnet\dotnet.exe")
        )) {
            if (-not [string]::IsNullOrWhiteSpace($path)) {
                $candidates.Add($path)
            }
        }
    }

    $seen = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::OrdinalIgnoreCase)
    foreach ($candidate in $candidates) {
        if (-not $seen.Add($candidate) -or -not (Test-Path $candidate)) {
            continue
        }

        $sdkVersions = @(& $candidate --list-sdks 2>$null | ForEach-Object {
            if ($_ -match '^(10\.\d+\.\d+)\s+\[') {
                [Version]$Matches[1]
            }
        })
        if ($LASTEXITCODE -eq 0 -and $sdkVersions.Count -gt 0) {
            return [PSCustomObject]@{
                Command = $candidate
                Version = ($sdkVersions | Sort-Object -Descending | Select-Object -First 1)
            }
        }
    }

    throw ".NET 10 SDK not found. Install the .NET 10 SDK (the runtime alone is not sufficient)."
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = Join-Path $ScriptDir "StartApps.csproj"
$PublishRoot = Join-Path $ScriptDir "bin/publish"
$OutputDir = Join-Path $PublishRoot $StartAppsProfile

if (-not (Test-Path $ProjectPath)) {
    throw "Project file not found: $ProjectPath"
}

$dotnetSdk = Resolve-DotNet10Sdk
$sdkWorkingDir = Join-Path ([IO.Path]::GetTempPath()) ("StartApps-publish-{0}-{1}" -f $PID, [Guid]::NewGuid().ToString("N"))

$targetFileName = switch ($StartAppsProfile) {
    "manager" { "StartApps.Manager.exe" }
    "player" { "StartApps.Player.exe" }
    default { "StartApps.exe" }
}

Write-Host "[publish] project: $ProjectPath"
Write-Host "[publish] configuration: $Configuration"
Write-Host "[publish] runtime: $RuntimeIdentifier"
Write-Host "[publish] profile: $StartAppsProfile"
Write-Host "[publish] publish dir: $OutputDir"
Write-Host "[publish] dotnet: $($dotnetSdk.Command)"
Write-Host "[publish] SDK: $($dotnetSdk.Version)"

Remove-Item -Recurse -Force $OutputDir -ErrorAction SilentlyContinue
if (Test-Path $OutputDir) {
    throw "Cannot clean publish directory. Close the running '$targetFileName' process and try again: $OutputDir"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

New-Item -ItemType Directory -Force -Path $sdkWorkingDir | Out-Null
@{
    sdk = @{
        version = $dotnetSdk.Version.ToString(3)
        rollForward = "disable"
        allowPrerelease = $false
    }
} | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $sdkWorkingDir "global.json") -Encoding UTF8

try {
    Push-Location $sdkWorkingDir
    try {
        & $dotnetSdk.Command publish $ProjectPath `
            -c $Configuration `
            -r $RuntimeIdentifier `
            --self-contained true `
            -o $OutputDir `
            "/p:StartAppsProfile=$StartAppsProfile" `
            "/p:PublishSingleFile=true" `
            "/p:PublishReadyToRun=true" `
            "/p:IncludeNativeLibrariesForSelfExtract=true" `
            "/p:EnableCompressionInSingleFile=true" `
            "/p:DebugType=None" `
            "/p:DebugSymbols=false"
        $publishExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
finally {
    Remove-Item -Recurse -Force $sdkWorkingDir -ErrorAction SilentlyContinue
}

if ($publishExitCode -ne 0) {
    throw "dotnet publish failed for profile '$StartAppsProfile'."
}

$publishedExecutablePath = Join-Path $OutputDir "StartApps.exe"
if (-not (Test-Path $publishedExecutablePath)) {
    throw "Published executable not found: $publishedExecutablePath"
}

$targetExecutablePath = Join-Path $OutputDir $targetFileName
if (-not [string]::Equals($publishedExecutablePath, $targetExecutablePath, [System.StringComparison]::OrdinalIgnoreCase)) {
    Move-Item -Path $publishedExecutablePath -Destination $targetExecutablePath
}

Write-Host ""
Write-Host "[publish] output executable:"
Write-Host $targetExecutablePath
Write-Host ""

Get-ChildItem $OutputDir | Format-Table Name, Length, LastWriteTime -AutoSize
