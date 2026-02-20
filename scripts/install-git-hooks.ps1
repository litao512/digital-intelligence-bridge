Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (git rev-parse --show-toplevel).Trim()
if (-not $repoRoot) {
    throw "Not inside a git repository."
}

$hookPath = Join-Path $repoRoot ".git/hooks/pre-commit"
$hookContent = @'
#!/usr/bin/env sh
if command -v pwsh >/dev/null 2>&1; then
  pwsh -NoProfile -ExecutionPolicy Bypass -File "./scripts/check-utf8.ps1" -Staged
else
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\check-utf8.ps1" -Staged
fi
if [ $? -ne 0 ]; then
  exit 1
fi

if command -v pwsh >/dev/null 2>&1; then
  pwsh -NoProfile -ExecutionPolicy Bypass -File "./scripts/check-doc-lang.ps1" -Staged
else
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\check-doc-lang.ps1" -Staged
fi
if [ $? -ne 0 ]; then
  exit 1
fi
'@

[System.IO.File]::WriteAllText($hookPath, $hookContent, [System.Text.UTF8Encoding]::new($false))

try {
    git update-index --chmod=+x .git/hooks/pre-commit 2>$null | Out-Null
}
catch {
}

Write-Host "Installed pre-commit hook: $hookPath" -ForegroundColor Green
