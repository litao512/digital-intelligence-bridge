# Encoding Guardrails (UTF-8)

This repository uses UTF-8 as the single source-of-truth encoding for docs, source, and config files.

## What is enforced
- `.editorconfig` sets `charset = utf-8`.
- `.gitattributes` marks key text extensions with `working-tree-encoding=UTF-8`.
- `scripts/check-utf8.ps1` validates UTF-8 and rejects files containing the replacement char (`U+FFFD`).
- `scripts/install-git-hooks.ps1` installs a `pre-commit` hook to run staged checks automatically.

## One-time setup
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1
```

## Manual checks
Check staged files (same as pre-commit):
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-utf8.ps1 -Staged
```

Check all tracked files:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-utf8.ps1
```

## If you see replacement characters in a file
- Treat it as data loss, not a display issue.
- Restore from git history/backup before continuing edits.
- Do not copy text from a garbled terminal back into files.
