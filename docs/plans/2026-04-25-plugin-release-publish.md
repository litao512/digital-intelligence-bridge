# 插件发布闭环实施计划

> **执行要求：** 使用 `superpowers:executing-plans`，按任务逐项执行并在批次之间回报。

**目标：** 建立以 `PatientRegistration` 为样例的 DIB 插件独立发布闭环。

**架构：** 新增单插件发布脚本，从 `plugins-src/<PluginId>.Plugin` 构建 Release 输出并生成独立插件 zip。发布流程文档与个人 skill 复用客户端发布的四段式校验思想，但插件侧产物写入 `plugin_versions` 并发布 `plugin-manifest.json`。

**技术栈：** PowerShell 7、.NET 10、Avalonia 插件项目、Supabase Release Center、Markdown 运行手册、Codex skill。

---

### Task 1: 增加插件发布脚本测试

**Files:**
- Create: `scripts/test-publish-plugin-release.ps1`
- Reference: `scripts/test-rotate-runtime-config.ps1`
- Reference: `scripts/publish-release.ps1`
- Reference: `plugins-src/PatientRegistration.Plugin/plugin.json`

**Step 1: 编写脚本测试**

创建 `scripts/test-publish-plugin-release.ps1`，测试应执行以下检查：

```powershell
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scriptPath = Join-Path $projectRoot 'scripts\publish-plugin-release.ps1'
$version = '0.1.0-test'
$pluginCode = 'patient-registration'
$releaseRoot = Join-Path $projectRoot "artifacts\plugin-releases\$pluginCode\$version"
$zipPath = Join-Path $releaseRoot "$pluginCode-$version.zip"
$manifestPath = Join-Path $releaseRoot 'plugin-release-manifest.json'
$publishRoot = Join-Path $releaseRoot 'publish'

if (Test-Path -LiteralPath $releaseRoot) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

& 'C:\Program Files\PowerShell\7\pwsh.exe' -File $scriptPath `
    -PluginId PatientRegistration `
    -Version $version `
    -Channel stable `
    -SkipRestore

if ($LASTEXITCODE -ne 0) {
    throw "publish-plugin-release.ps1 failed with exit code $LASTEXITCODE"
}

$requiredFiles = @(
    'plugin.json',
    'plugin.settings.json',
    'PatientRegistration.Plugin.dll',
    'PatientRegistration.Plugin.deps.json',
    'QRCoder.dll',
    'Npgsql.dll'
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $publishRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing expected plugin package file: $path"
    }
}

if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "Missing plugin zip: $zipPath"
}

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Missing plugin release manifest: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.pluginCode -ne $pluginCode) {
    throw "Unexpected pluginCode: $($manifest.pluginCode)"
}

if ($manifest.version -ne $version) {
    throw "Unexpected version: $($manifest.version)"
}

if ([string]::IsNullOrWhiteSpace($manifest.sha256)) {
    throw 'Manifest sha256 is empty.'
}

Write-Host 'publish-plugin-release.ps1 test passed.' -ForegroundColor Green
```

**Step 2: 运行测试确认失败**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-publish-plugin-release.ps1
```

Expected: FAIL，因为 `scripts/publish-plugin-release.ps1` 尚不存在。

**Step 3: 提交测试**

暂不提交，等 Task 2 实现脚本后与实现一起提交。

### Task 2: 实现插件发布脚本

**Files:**
- Create: `scripts/publish-plugin-release.ps1`
- Modify: `scripts/test-publish-plugin-release.ps1`
- Reference: `scripts/publish-release.ps1`
- Reference: `docs/05-operations/PLUGIN_PACKAGING_GUIDE.md`

**Step 1: 创建脚本骨架**

实现参数：

```powershell
[CmdletBinding()]
param(
    [ValidateSet('Release')]
    [string]$Configuration = 'Release',
    [string]$PluginId,
    [string]$Version,
    [string]$Channel = 'stable',
    [switch]$SkipRestore,
    [switch]$SkipZip
)
```

规则：

- `PluginId` 使用源码目录前缀，例如 `PatientRegistration`。
- 插件编码从 `plugin.json.id` 读取，例如 `patient-registration`。
- 默认版本从 `plugin.json.version` 读取；命令行 `-Version` 优先。
- 输出目录为 `artifacts/plugin-releases/<plugin-code>/<version>/`。
- zip 文件名为 `<plugin-code>-<version>.zip`。
- Storage 路径写入 manifest：`plugins/<plugin-code>/<channel>/<version>/<plugin-code>-<version>.zip`。

**Step 2: 实现构建与同步**

脚本应：

1. 定位仓库根目录。
2. 定位 `plugins-src/<PluginId>.Plugin/<PluginId>.Plugin.csproj`。
3. 执行 `dotnet restore <project> -m:1`，除非传入 `-SkipRestore`。
4. 清空插件输出目录 `bin/<Configuration>/net10.0`。
5. 执行 `dotnet build <project> -c <Configuration> --no-restore -m:1 -v minimal`。
6. 复制构建输出到 `artifacts/plugin-releases/<plugin-code>/<version>/publish/`。
7. 同步同一输出到仓库根 `plugins/<PluginId>/` 中转目录。

**Step 3: 实现发布前校验**

至少检查：

```powershell
plugin.json
plugin.settings.json
<PluginId>.Plugin.dll
<PluginId>.Plugin.deps.json
QRCoder.dll
Npgsql.dll
```

若缺失任一文件，脚本应直接 `throw`。

**Step 4: 实现 zip 与 manifest**

使用 `Compress-Archive -Path (Join-Path $publishRoot '*')` 创建 zip。

写入 `plugin-release-manifest.json`：

```json
{
  "pluginId": "PatientRegistration",
  "pluginCode": "patient-registration",
  "version": "0.1.0-dev.1",
  "channel": "stable",
  "configuration": "Release",
  "generatedAt": "2026-04-25T00:00:00",
  "zipFileName": "patient-registration-0.1.0-dev.1.zip",
  "storagePath": "plugins/patient-registration/stable/0.1.0-dev.1/patient-registration-0.1.0-dev.1.zip",
  "sha256": "<hex>",
  "sizeBytes": 123
}
```

**Step 5: 运行脚本测试**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-publish-plugin-release.ps1
```

Expected: PASS。

**Step 6: 提交脚本与测试**

Run:

```powershell
git add scripts\publish-plugin-release.ps1 scripts\test-publish-plugin-release.ps1
git commit -m "feat: 增加插件发布打包脚本"
```

### Task 3: 增加插件发布运行手册

**Files:**
- Create: `docs/05-operations/PLUGIN_RELEASE_PUBLISH_RUNBOOK.md`
- Modify: `docs/05-operations/PLUGIN_PACKAGING_GUIDE.md`
- Reference: `docs/05-operations/CLIENT_RELEASE_PUBLISH_RUNBOOK.md`
- Reference: `docs/plans/2026-04-25-plugin-release-publish-design.md`

**Step 1: 写运行手册**

运行手册应覆盖：

- 本地打包命令。
- `PatientRegistration` 示例版本命名。
- 本地包完整性检查。
- 发布中心资产上传字段。
- 插件定义字段。
- 插件版本字段。
- `plugin-manifest.json` 发布与验证。
- 客户端下载、预安装、激活验证。
- 同版本修复流程。
- 常见错误。

**Step 2: 更新既有插件打包指南**

在 `docs/05-operations/PLUGIN_PACKAGING_GUIDE.md` 增加“独立插件发布入口”小节，指向新手册与 `scripts/publish-plugin-release.ps1`。

**Step 3: 运行文档语言检查**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-doc-lang.ps1
```

Expected: PASS。

**Step 4: 提交文档**

Run:

```powershell
git add docs\05-operations\PLUGIN_RELEASE_PUBLISH_RUNBOOK.md docs\05-operations\PLUGIN_PACKAGING_GUIDE.md
git commit -m "docs: 增加插件发布运行手册"
```

### Task 4: 增加插件发布 skill

**Files:**
- Create: `C:\Users\Administrator\.codex\skills\plugin-release-publish\SKILL.md`
- Reference: `C:\Users\Administrator\.codex\skills\client-release-publish\SKILL.md`
- Reference: `docs/05-operations/PLUGIN_RELEASE_PUBLISH_RUNBOOK.md`

**Step 1: 创建 skill 目录和文件**

Skill 描述：

```markdown
---
name: plugin-release-publish
description: Use when packaging, uploading, registering, publishing, or verifying a DIB plugin release, especially when working with scripts/publish-plugin-release.ps1, dib-release-center, Supabase Storage plugin_package assets, plugin version records, or plugin-manifest.json publication for stable/beta/internal channels.
---
```

**Step 2: 编写 workflow**

Workflow 应包含：

1. 本地插件打包。
2. 本地包验证。
3. 打开发布中心。
4. 登记 `plugin_package` 资产。
5. 创建或复用插件定义。
6. 创建插件版本记录。
7. 发布 `plugin-manifest.json`。
8. 客户端下载、预安装、激活验证。

**Step 3: 写决策规则**

规则应说明：

- 开发期可以直接发布 manifest。
- 上线后 rehearsal 应保持草稿并不发布 manifest。
- 同版本重发按修复流程处理。
- zip 上传成功不等于发布完成。
- manifest 成功不等于客户端激活成功。

**Step 4: 人工校验 skill**

Run:

```powershell
Get-Content -LiteralPath 'C:\Users\Administrator\.codex\skills\plugin-release-publish\SKILL.md' -TotalCount 260
```

Expected: 元数据头存在，工作流完整，没有整段英文说明进入中文主体。元数据头的 `description` 可以保留英文以匹配 skill 元数据风格。

### Task 5: 完整验证插件发布本地产物

**Files:**
- Generated: `artifacts/plugin-releases/patient-registration/0.1.0-dev.1/`
- Generated: `plugins/PatientRegistration/`

**Step 1: 生成示例插件包**

Run:

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\publish-plugin-release.ps1 -PluginId PatientRegistration -Version 0.1.0-dev.1 -Channel stable
```

Expected:

- `artifacts/plugin-releases/patient-registration/0.1.0-dev.1/publish/` 存在。
- `artifacts/plugin-releases/patient-registration/0.1.0-dev.1/patient-registration-0.1.0-dev.1.zip` 存在。
- `artifacts/plugin-releases/patient-registration/0.1.0-dev.1/plugin-release-manifest.json` 存在。

**Step 2: 核对 manifest**

Run:

```powershell
Get-Content -LiteralPath .\artifacts\plugin-releases\patient-registration\0.1.0-dev.1\plugin-release-manifest.json
```

Expected:

- `pluginCode` 是 `patient-registration`。
- `storagePath` 是 `plugins/patient-registration/stable/0.1.0-dev.1/patient-registration-0.1.0-dev.1.zip`。
- `sha256` 非空。
- `sizeBytes` 大于 `0`。

**Step 3: 运行文档和构建验证**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-doc-lang.ps1
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal
```

Expected:

- 文档语言检查通过。
- 构建通过。
- 单元测试通过。

### Task 6: 发布中心手工闭环

**Files:**
- Reference: `docs/05-operations/PLUGIN_RELEASE_PUBLISH_RUNBOOK.md`
- Reference: `artifacts/plugin-releases/patient-registration/0.1.0-dev.1/plugin-release-manifest.json`

**Step 1: 打开发布中心**

Run:

```powershell
cd .\dib-release-center
npm run dev -- --host 127.0.0.1 --port 4173
```

Expected: 可以访问 `http://127.0.0.1:4173/release-center/`。

**Step 2: 上传插件资产**

在 `发布资产` 页面上传：

```text
Bucket: dib-releases
Asset Kind: plugin_package
Storage Path: plugins/patient-registration/stable/0.1.0-dev.1/patient-registration-0.1.0-dev.1.zip
File: artifacts/plugin-releases/patient-registration/0.1.0-dev.1/patient-registration-0.1.0-dev.1.zip
```

Expected: 新资产出现在资产列表，记录 `release_assets.id`。

**Step 3: 创建插件版本并发布**

在 `插件发布` 页面确认插件定义存在后，创建版本：

```text
插件定义：patient-registration
发布渠道：stable
资产 ID：上一步 release_assets.id
版本号：0.1.0-dev.1
DIB 最低版本：1.0.0
立即标记为已发布：是
```

Expected: 版本列表显示该版本已发布。

**Step 4: 发布 manifest 并验证**

在 manifest 发布入口发布 `stable` 渠道 manifest。

Expected:

- 公开 `manifests/stable/plugin-manifest.json` 可访问。
- manifest 包含 `patient-registration` 和 `0.1.0-dev.1`。
- manifest 中 `sha256` 等于本地 manifest 中的 `sha256`。

**Step 5: 客户端验证**

通过客户端设置中的发布中心操作或等价命令验证：

- 下载插件包成功。
- 预安装成功。
- 激活成功。
- 运行时插件目录包含 `patient-registration`。
- 宿主可发现并加载“就诊登记”。

**Step 6: 记录结果**

将发布中心闭环结果追加到新建记录：

```text
docs/plans/2026-04-25-plugin-release-rehearsal.md
```

记录至少包含：

- 本地 zip 路径。
- `sha256`。
- `sizeBytes`。
- `release_assets.id`。
- 插件版本记录状态。
- manifest 验证结果。
- 客户端下载、预安装、激活结果。
