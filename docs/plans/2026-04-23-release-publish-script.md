# 发布脚本实现计划

> 执行说明：按任务顺序逐项实现，并在每个关键步骤后执行可验证检查。

**目标:** 新增一个统一的 Release 发布脚本，自动构建并同步插件、发布主程序、生成发布目录和 zip 包。

**架构:** 使用单个 PowerShell 脚本串行执行发布流程，先处理插件，再处理主程序发布，最后整理产物和压缩包。脚本通过约定式目录映射 `plugins-src/<PluginId>.Plugin -> plugins/<PluginId>` 本地中转目录，避免额外配置文件；仓库根 `plugins/` 已被忽略，不作为源码提交内容。

**技术栈:** PowerShell、dotnet CLI、Compress-Archive、仓库现有目录规范。

---

### 任务 1: 新增发布脚本文档

**文件:**
- 新增: `docs/plans/2026-04-23-release-publish-script-design.md`
- 新增: `docs/plans/2026-04-23-release-publish-script.md`

**Step 1: 记录发布脚本目标与边界**

- 说明为什么需要在发布主程序前自动刷新 `plugins/`
- 明确 `plugins/`、发布目录和 zip 都是本地生成产物

**Step 2: 保存设计与实现计划**

- 将设计结论写入 `docs/plans/`

### 任务 2: 实现发布脚本

**文件:**
- 新增: `scripts/publish-release.ps1`

**Step 1: 写出脚本骨架**

- 增加参数
- 定义仓库路径
- 定义输出目录

**Step 2: 实现插件构建与同步**

- 遍历 `plugins-src/*.Plugin`
- 逐个执行 `dotnet publish -c Release`
- 同步输出到 `plugins/<plugin-id>` 本地中转目录
- 保留 `plugin.settings.json`

**Step 3: 实现主程序发布与产物整理**

- 执行主程序 `dotnet restore`
- 执行主程序 `dotnet publish -c Release`
- 复制 `appsettings.json`
- 复制仓库根 `plugins/` 本地中转目录

**Step 4: 实现 zip 打包与校验**

- 生成 zip 文件
- 校验主 exe 和 zip 存在

### Task 3: 验证脚本

**Files:**
- Test: `scripts/publish-release.ps1`

**Step 1: 运行脚本生成发布产物**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Version 0.0.0-local`

Expected: 生成 `artifacts/releases/0.0.0-local/publish/` 和 zip 包

**Step 2: 检查关键产物**

- `digital-intelligence-bridge.exe`
- `appsettings.json`
- `plugins/`
- zip 包
