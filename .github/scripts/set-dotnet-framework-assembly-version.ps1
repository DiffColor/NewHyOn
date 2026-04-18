param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [Parameter(Mandatory = $true)]
    [string[]]$AssemblyInfoPaths,

    [string]$ProjectTagPrefix
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-VersionFromTag {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputTag,
        [string]$ExpectedPrefix
    )

    $patterns = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($ExpectedPrefix)) {
        $escapedPrefix = [regex]::Escape($ExpectedPrefix.Trim())
        $patterns.Add("^${escapedPrefix}-v(?<version>\d+\.\d+\.\d+(?:\.\d+)?)$")
    }

    $patterns.Add("^v(?<version>\d+\.\d+\.\d+(?:\.\d+)?)$")

    foreach ($pattern in $patterns) {
        $match = [regex]::Match($InputTag, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($match.Success) {
            return $match.Groups["version"].Value
        }
    }

    if ([string]::IsNullOrWhiteSpace($ExpectedPrefix)) {
        throw "Unsupported tag format '$InputTag'. Expected v<major>.<minor>.<build>[.<revision>]."
    }

    throw "Unsupported tag format '$InputTag'. Expected ${ExpectedPrefix}-v<major>.<minor>.<build>[.<revision>] or v<major>.<minor>.<build>[.<revision>]."
}

$normalizedTag = $Tag.Trim()
if ([string]::IsNullOrWhiteSpace($normalizedTag)) {
    throw "Tag is required."
}

$versionString = Resolve-VersionFromTag -InputTag $normalizedTag -ExpectedPrefix $ProjectTagPrefix
[void][version]$versionString

$assemblyVersionLine = '[assembly: AssemblyVersion("{0}")]' -f $versionString
$fileVersionLine = '[assembly: AssemblyFileVersion("{0}")]' -f $versionString
$informationalLine = '[assembly: AssemblyInformationalVersion("{0}")]' -f $normalizedTag

$utf8Bom = New-Object System.Text.UTF8Encoding($true)

foreach ($assemblyInfoPath in $AssemblyInfoPaths) {
    $resolvedPath = Resolve-Path -Path $assemblyInfoPath
    $content = [System.IO.File]::ReadAllText($resolvedPath, [System.Text.Encoding]::UTF8)

    $assemblyVersionPattern = '(?m)^\[assembly:\s*AssemblyVersion\(".*?"\)\]\s*$'
    $fileVersionPattern = '(?m)^\[assembly:\s*AssemblyFileVersion\(".*?"\)\]\s*$'
    $informationalVersionPattern = '(?m)^\[assembly:\s*AssemblyInformationalVersion\(".*?"\)\]\s*$'

    if (-not [regex]::IsMatch($content, $assemblyVersionPattern)) {
        throw "AssemblyVersion attribute not found in '$assemblyInfoPath'."
    }

    if (-not [regex]::IsMatch($content, $fileVersionPattern)) {
        throw "AssemblyFileVersion attribute not found in '$assemblyInfoPath'."
    }

    $updated = [regex]::Replace($content, $assemblyVersionPattern, $assemblyVersionLine)
    $updated = [regex]::Replace($updated, $fileVersionPattern, $fileVersionLine)

    if ([regex]::IsMatch($updated, $informationalVersionPattern)) {
        $updated = [regex]::Replace($updated, $informationalVersionPattern, $informationalLine)
    }
    else {
        $replacement = "$fileVersionLine`r`n$informationalLine"
        $updated = $updated.Replace($fileVersionLine, $replacement)
    }

    [System.IO.File]::WriteAllText($resolvedPath, $updated, $utf8Bom)
    Write-Host "Applied version $versionString ($normalizedTag) to $assemblyInfoPath"
}
