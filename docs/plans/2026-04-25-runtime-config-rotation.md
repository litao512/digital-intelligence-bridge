# 运行配置轮换实施计划

> 执行说明：按任务顺序逐项实现，并在每个关键步骤后执行可验证检查。

**目标:** 为当前双配置架构新增安全配置轮换脚本，避免人工覆盖 `appsettings.json` 时缺少备份与校验。

**架构:** 新增一个独立 PowerShell 脚本对指定 `appsettings.json` 执行校验、备份、替换和替换后校验。测试使用轻量 PowerShell 脚本直接构造沙箱配置，不引入新的测试框架。

**技术栈:** PowerShell、JSON、现有 `scripts/check-doc-lang.ps1`、Git 差异检查。

---

### Task 1: 新增脚本测试

**文件:**
- 新增: `scripts/test-rotate-runtime-config.ps1`

**Step 1: 写失败测试**

编写两个测试：

1. 有效新配置替换目标配置，并生成备份。
2. 无效新配置失败，目标配置不变。

**Step 2: 运行测试确认失败**

执行：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-rotate-runtime-config.ps1`

预期：因 `scripts/rotate-runtime-config.ps1` 不存在而失败。

### Task 2: 实现轮换脚本

**文件:**
- 新增: `scripts/rotate-runtime-config.ps1`

**Step 1: 增加参数**

参数包含：

- `SourcePath`
- `TargetPath`
- `BackupDirectory`

**Step 2: 增加 JSON 校验**

校验 `ReleaseCenter`、`Supabase.Url` 与 `Supabase.Schema`。

**Step 3: 增加备份与替换**

目标存在时先备份，再通过临时文件替换目标。

**Step 4: 增加失败保护**

替换后校验失败时恢复备份。

**Step 5: 运行脚本测试**

执行：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-rotate-runtime-config.ps1`

预期：测试通过。

### Task 3: 更新技术债清单

**文件:**
- 修改: `docs/plans/2026-02-19-technical-debt-backlog.md`

**Step 1: 更新当前状态**

说明 `TD-002` 已按当前双配置架构完成。

**Step 2: 更新表格状态**

将 `TD-002` 状态改为已完成。

### Task 4: 收尾验证

**文件:**
- 仅验证

**Step 1: 文档语言检查**

执行：`powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`

预期：通过。

**Step 2: 差异检查**

执行：`git diff --check`

预期：无新增格式错误。
