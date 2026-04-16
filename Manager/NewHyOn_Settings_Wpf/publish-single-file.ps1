param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = Join-Path $ScriptDir "NewHyOn.Settings.Wpf.csproj"
$OutputDir = Join-Path $ScriptDir "bin/publish"

Write-Host "[publish] project: $ProjectPath"
Write-Host "[publish] configuration: $Configuration"
Write-Host "[publish] publish dir: $OutputDir"

dotnet publish $ProjectPath -c $Configuration -p:PublishDir="$OutputDir"

Write-Host ""
Write-Host "[publish] output:"
Write-Host $OutputDir
Write-Host ""

Get-ChildItem $OutputDir | Format-Table Name, Length, LastWriteTime -AutoSize
