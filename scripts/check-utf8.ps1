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

$extensions = @(
    ".md", ".txt", ".json", ".yml", ".yaml", ".xml",
    ".cs", ".axaml", ".csproj", ".props", ".targets", ".sln", ".ps1"
)

function Get-CandidateFiles {
    param([bool]$OnlyStaged)

    if ($OnlyStaged) {
        $files = git diff --cached --name-only --diff-filter=ACMR
        return $files | Where-Object { $_ -and (Test-Path $_) }
    }

    $files = git ls-files
    return $files | Where-Object { $_ -and (Test-Path $_) }
}

$utf8Strict = [System.Text.UTF8Encoding]::new($false, $true)
$badFiles = New-Object System.Collections.Generic.List[string]
$replacementChar = [char]0xFFFD

$filesToCheck = Get-CandidateFiles -OnlyStaged:$Staged

foreach ($path in $filesToCheck) {
    $ext = [System.IO.Path]::GetExtension($path).ToLowerInvariant()
    if ($extensions -notcontains $ext) {
        continue
    }

    try {
        $bytes = [System.IO.File]::ReadAllBytes((Join-Path $repoRoot $path))
        $text = $utf8Strict.GetString($bytes)
        if ($text.IndexOf($replacementChar) -ge 0) {
            $badFiles.Add("$path (contains U+FFFD replacement character)")
        }
    }
    catch {
        $badFiles.Add("$path (invalid UTF-8 byte sequence)")
    }
}

if ($badFiles.Count -gt 0) {
    Write-Host "UTF-8 check failed for the following files:" -ForegroundColor Red
    $badFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Fix the file encoding/content before commit." -ForegroundColor Yellow
    exit 1
}

Write-Host "UTF-8 check passed." -ForegroundColor Green
exit 0

