param(
    [switch]$Staged
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (git rev-parse --show-toplevel).Trim()
if (-not $repoRoot) {
    throw "Not inside a git repository."
}

Set-Location $repoRoot

$allowList = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
@(
    "PRD", "API", "CI", "CD", "MVVM", "UI", "UX", "DX",
    "Avalonia", "Prism", "DryIoc", "Serilog", "Supabase",
    ".NET", "NET", "JSON", "YAML", "UTF", "UTF-8",
    "Debug", "Release", "Tab", "Todo", "WebView"
) | ForEach-Object { [void]$allowList.Add($_) }

function Get-TargetFiles {
    param([bool]$OnlyStaged)

    if ($OnlyStaged) {
        $files = git diff --cached --name-only --diff-filter=ACMR
    }
    else {
        $files = git ls-files
    }

    return $files |
        Where-Object { $_ } |
        ForEach-Object { $_.Replace("\", "/") } |
        Where-Object { $_ -like "docs/*" -and $_.ToLowerInvariant().EndsWith(".md") } |
        Where-Object { Test-Path $_ }
}

function Get-NormalizedToken {
    param([string]$Token)

    $normalized = $Token -replace "^[^A-Za-z.]+", "" -replace "[^A-Za-z0-9.+/-]+$", ""
    return $normalized
}

function Strip-IgnoredSegments {
    param([string]$Line)

    $result = $Line
    $result = [System.Text.RegularExpressions.Regex]::Replace($result, '`[^`]*`', " ")
    $result = [System.Text.RegularExpressions.Regex]::Replace($result, "https?://\S+", " ")
    $result = [System.Text.RegularExpressions.Regex]::Replace($result, "\[[^\]]+\]\([^)]+\)", " ")
    return $result
}

$issues = New-Object System.Collections.Generic.List[string]
$files = Get-TargetFiles -OnlyStaged:$Staged

foreach ($path in $files) {
    $fullPath = Join-Path $repoRoot $path
    $lines = Get-Content -LiteralPath $fullPath -Encoding UTF8
    $inCodeBlock = $false

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $lineNo = $i + 1
        $line = $lines[$i]

        if ($line -match '^\s*```') {
            $inCodeBlock = -not $inCodeBlock
            continue
        }

        if ($inCodeBlock) {
            continue
        }

        $sanitized = Strip-IgnoredSegments -Line $line
        if ([string]::IsNullOrWhiteSpace($sanitized)) {
            continue
        }

        $isHeading = $sanitized -match '^\s*#{1,6}\s+'
        $hasChinese = $sanitized -match '[\p{IsCJKUnifiedIdeographs}]'

        if ($isHeading -and -not $hasChinese -and $sanitized -match '[A-Za-z]{4,}') {
            $issues.Add("${path}:${lineNo}: Suspicious English heading: $($line.Trim())")
            continue
        }

        $tokens = $sanitized -split '\s+'
        $consecutiveEnglish = 0
        $maxConsecutiveEnglish = 0

        foreach ($token in $tokens) {
            $normalized = Get-NormalizedToken -Token $token
            if (-not $normalized) {
                $consecutiveEnglish = 0
                continue
            }

            if ($normalized -match '^[A-Za-z][A-Za-z0-9.+/-]*$') {
                if ($allowList.Contains($normalized)) {
                    $consecutiveEnglish = 0
                    continue
                }

                $consecutiveEnglish++
                if ($consecutiveEnglish -gt $maxConsecutiveEnglish) {
                    $maxConsecutiveEnglish = $consecutiveEnglish
                }
            }
            else {
                $consecutiveEnglish = 0
            }
        }

        if ($maxConsecutiveEnglish -ge 5) {
            $issues.Add("${path}:${lineNo}: Suspicious English sentence fragment (>= 5 consecutive words): $($line.Trim())")
        }
    }
}

if ($issues.Count -gt 0) {
    Write-Host "Document language check failed. Possible mixed-language content found:" -ForegroundColor Red
    $issues | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Keep markdown prose in docs/ primarily Chinese. Code/paths/keys can stay in English." -ForegroundColor Yellow
    exit 1
}

Write-Host "Document language check passed." -ForegroundColor Green
exit 0
