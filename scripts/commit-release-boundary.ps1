param(
    [string]$Message = "chore: normalize plugin staging directory and release scripts",
    [switch]$SkipChecks,
    [switch]$NoCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Text)
    Write-Host ""
    Write-Host "==> $Text" -ForegroundColor Cyan
}

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

$repoRoot = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    throw "Current directory is not inside a Git repository."
}

Set-Location $repoRoot
Write-Host "Repository root: $repoRoot"

Write-Step "Checking .git ACL for DENY entries"
$aclOutput = & icacls .git
$aclText = $aclOutput -join [Environment]::NewLine
Write-Host $aclText
if ($aclText -match "\bDENY\b") {
    throw ".git still has DENY ACL entries. Clean ACL first, then run this script again."
}

if (-not $SkipChecks) {
    Write-Step "Running pre-commit lightweight checks"
    & powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check-doc-lang.ps1
    if ($LASTEXITCODE -ne 0) {
        throw "Document language check failed."
    }

    Invoke-Git diff --check
}

$paths = @(
    ".gitignore",
    "AGENTS.md",
    "Directory.Build.targets",
    "digital-intelligence-bridge.slnx",
    "scripts\publish-release.ps1",
    "scripts\reset-and-rebuild-runtime.ps1",
    "scripts\sync-github-secrets.ps1",
    "digital-intelligence-bridge\README.md",
    "docs\05-operations\CLIENT_RELEASE_PUBLISH_RUNBOOK.md",
    "docs\05-operations\PLUGIN_PACKAGING_GUIDE.md",
    "docs\plugin-development-conventions.md",
    "docs\PRD.md",
    "docs\plans\2026-04-23-release-publish-script-design.md",
    "docs\plans\2026-04-23-release-publish-script.md"
)

Write-Step "Staging release-boundary files"
Invoke-Git add -- $paths

Write-Step "Staged changes"
Invoke-Git diff --cached --name-status

$cachedNames = (& git diff --cached --name-only)
if ($LASTEXITCODE -ne 0) {
    throw "Failed to read staged changes."
}

if (-not $cachedNames) {
    throw "Nothing staged."
}

Write-Step "Checking ignored local staging directories"
& git check-ignore -v plugins artifacts plugins\PatientRegistration plugins\MedicalDrugImport 2>$null

if ($NoCommit) {
    Write-Host ""
    Write-Host "Files are staged. -NoCommit was set, so no commit was created." -ForegroundColor Yellow
    exit 0
}

Write-Step "Creating commit"
Invoke-Git commit -m $Message

Write-Step "Commit complete. Current status"
Invoke-Git status --short --untracked-files=all
