param(
    [string]$Root = ".",
    [string]$PackageRoot = ""
)

$ErrorActionPreference = "Stop"

$requiredFiles = @(
    "Player/Windows/Dependencies/LicenseHub.DeviceAuth/netstandard2.0/LicenseHub.DeviceAuth.Core.dll",
    "Player/Windows/Dependencies/LicenseHub.DeviceAuth/net472/BouncyCastle.Crypto.dll",
    "Player/Windows/Dependencies/LicenseHub.DeviceAuth/net10.0-windows/LicenseHub.DeviceAuth.Core.dll",
    "Player/Windows/Dependencies/LicenseHub.DeviceAuth/net10.0-windows/LicenseHub.DeviceAuth.Wpf.dll"
)

foreach ($relativePath in $requiredFiles) {
    $path = Join-Path $Root $relativePath
    if (!(Test-Path $path)) {
        throw "Required LicenseHub DeviceAuth asset is missing: $relativePath"
    }

    $item = Get-Item $path
    if ($item.Length -le 0) {
        throw "Required LicenseHub DeviceAuth asset is empty: $relativePath"
    }
}

if (![string]::IsNullOrWhiteSpace($PackageRoot)) {
    $requiredPackageFiles = @(
        "LicenseHub.DeviceAuth.Core.dll",
        "BouncyCastle.Crypto.dll"
    )

    foreach ($relativePath in $requiredPackageFiles) {
        $path = Join-Path $PackageRoot $relativePath
        if (!(Test-Path $path)) {
            throw "Required LicenseHub DeviceAuth package asset is missing: $relativePath"
        }

        $item = Get-Item $path
        if ($item.Length -le 0) {
            throw "Required LicenseHub DeviceAuth package asset is empty: $relativePath"
        }
    }
}

Write-Host "LicenseHub DeviceAuth assets verified."
