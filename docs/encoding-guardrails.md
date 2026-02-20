# 编码守护规范（UTF-8）

本仓库统一使用 UTF-8 作为文档、源码与配置文件的唯一编码标准。

## 当前约束
- `.editorconfig` 设置 `charset = utf-8`。
- `.gitattributes` 为关键文本后缀声明 `working-tree-encoding=UTF-8`。
- `scripts/check-utf8.ps1` 校验 UTF-8，并拒绝包含替换字符（`U+FFFD`）的文件。
- `scripts/check-doc-lang.ps1` 校验 `docs/**/*.md` 主体是否以中文为主（代码/路径/配置键除外）。
- `scripts/install-git-hooks.ps1` 安装 `pre-commit` 钩子，对暂存文件自动执行检查。

## 一次性初始化
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1
```

## 手动检查
检查暂存文件（与 pre-commit 一致）：
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-utf8.ps1 -Staged
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1 -Staged
```

检查所有已跟踪文件：
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-utf8.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1
```

## 如果文件中出现替换字符
- 将其视为数据损坏，而非显示问题。
- 继续编辑前应先从 Git 历史或备份恢复。
- 不要把终端中乱码内容再次复制回文件。
