param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFiles = @(Get-ChildItem -Path $ScriptDir -Filter '*.csproj' -File | Sort-Object Name)
if ($ProjectFiles.Count -eq 0) {
    throw "Project file not found in script directory: $ScriptDir"
}

if ($ProjectFiles.Count -gt 1) {
    throw "Multiple project files found in script directory: $ScriptDir"
}

$ProjectPath = $ProjectFiles[0].FullName
$OutputDir = Join-Path $ScriptDir "bin/publish"

Write-Host "[publish] project: $ProjectPath"
Write-Host "[publish] configuration: $Configuration"
Write-Host "[publish] publish dir: $OutputDir"

& dotnet publish $ProjectPath -c $Configuration "-p:PublishDir=$OutputDir"

Write-Host ""
Write-Host "[publish] output:"
Write-Host $OutputDir
Write-Host ""

Get-ChildItem $OutputDir | Format-Table Name, Length, LastWriteTime -AutoSize
