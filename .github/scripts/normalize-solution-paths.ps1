param(
    [Parameter(Mandatory = $true)]
    [string]$SolutionPath
)

$solutionFullPath = Join-Path (Get-Location) $SolutionPath
if (-not (Test-Path $solutionFullPath)) {
    throw "Solution file not found: $SolutionPath"
}

$solutionFullPath = (Resolve-Path $solutionFullPath).Path
$solutionDirectory = Split-Path -Parent $solutionFullPath
$content = Get-Content -Path $solutionFullPath -Raw

function Get-RelativeProjectPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AbsoluteProjectPath
    )

    $normalizedAbsolutePath = $AbsoluteProjectPath -replace '/', '\'
    $projectFileName = [System.IO.Path]::GetFileName($normalizedAbsolutePath)
    if ([string]::IsNullOrWhiteSpace($projectFileName)) {
        return $null
    }

    $candidates = Get-ChildItem -Path $solutionDirectory -Recurse -File -Filter $projectFileName
    if (-not $candidates) {
        return $null
    }

    $normalizedAbsoluteSegments = $normalizedAbsolutePath.Split('\', [System.StringSplitOptions]::RemoveEmptyEntries)
    $bestCandidate = $null
    $bestScore = -1

    foreach ($candidate in $candidates) {
        $relativePath = [System.IO.Path]::GetRelativePath($solutionDirectory, $candidate.FullName)
        $relativePath = $relativePath -replace '/', '\'
        $relativeSegments = $relativePath.Split('\', [System.StringSplitOptions]::RemoveEmptyEntries)

        $score = 0
        $absoluteIndex = $normalizedAbsoluteSegments.Length - 1
        $relativeIndex = $relativeSegments.Length - 1

        while ($absoluteIndex -ge 0 -and $relativeIndex -ge 0) {
            if ($normalizedAbsoluteSegments[$absoluteIndex] -ine $relativeSegments[$relativeIndex]) {
                break
            }

            $score++
            $absoluteIndex--
            $relativeIndex--
        }

        if ($score -gt $bestScore) {
            $bestScore = $score
            $bestCandidate = $relativePath
        }
    }

    return $bestCandidate
}

$absoluteProjectPaths = [System.Text.RegularExpressions.Regex]::Matches(
    $content,
    '(?<path>(?:[A-Za-z]:\\|\\\\)[^"]+?\.csproj)'
)

$resolvedReplacements = @{}
foreach ($match in $absoluteProjectPaths) {
    $absoluteProjectPath = $match.Groups['path'].Value
    if ($resolvedReplacements.ContainsKey($absoluteProjectPath)) {
        continue
    }

    $relativeProjectPath = Get-RelativeProjectPath -AbsoluteProjectPath $absoluteProjectPath
    if (-not [string]::IsNullOrWhiteSpace($relativeProjectPath)) {
        $resolvedReplacements[$absoluteProjectPath] = $relativeProjectPath
    }
}

foreach ($absoluteProjectPath in $resolvedReplacements.Keys) {
    $content = $content.Replace($absoluteProjectPath, $resolvedReplacements[$absoluteProjectPath])
}

[System.IO.File]::WriteAllText($solutionFullPath, $content, [System.Text.UTF8Encoding]::new($true))
