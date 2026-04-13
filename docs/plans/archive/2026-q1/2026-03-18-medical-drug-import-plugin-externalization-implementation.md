# 医保药品导入插件外部化实施计划

> **说明：** 需要按 `superpowers:executing-plans` 技能逐任务执行。

**Goal:** 将 `MedicalDrugImport.Plugin` 从样例插件扩展为具备独立配置、Excel 预检、PostgreSQL 导入和 SQL Server 同步能力的真实外部插件。

**Architecture:** 宿主继续只负责发现、加载、菜单和页面承载；插件自身读取 `plugin.settings.json` 和环境变量，独立创建数据库连接并执行完整医保业务链路。迁移按“配置 -> UI -> Excel -> PostgreSQL -> SQL Server”顺序推进，降低耦合和联调风险。

**Tech Stack:** .NET 10、Avalonia UI、xUnit、Microsoft.Extensions.Configuration、Npgsql、Microsoft.Data.SqlClient、Open XML / ZipArchive 流式读取

---

### Task 1: 为插件建立独立配置模型

**Files:**
- Create: `plugins-src/MedicalDrugImport.Plugin/Configuration/PluginSettings.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Configuration/PluginConfigurationLoader.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/plugin.settings.json`
- Modify: `plugins-src/MedicalDrugImport.Plugin/MedicalDrugImport.Plugin.csproj`
- Modify: `digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj`
- Test: `digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginConfigurationTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 插件能读取 `plugin.settings.json`
- 环境变量能覆盖插件配置
- 缺少配置文件时仍能返回默认配置对象

**步骤 2：运行测试并确认先失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginConfigurationTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 新增 `PluginSettings`
- 新增 `PluginConfigurationLoader`
- 约定环境变量前缀 `MEDICAL_DRUG_IMPORT__`
- 将 `plugin.settings.json` 复制到输出目录

**步骤 4：运行测试并确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginConfigurationTests`
Expected: PASS

**Step 5: Commit**

```bash
git add plugins-src/MedicalDrugImport.Plugin/Configuration plugins-src/MedicalDrugImport.Plugin/plugin.settings.json plugins-src/MedicalDrugImport.Plugin/MedicalDrugImport.Plugin.csproj digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginConfigurationTests.cs digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj
git commit -m "feat: add plugin-local configuration system"
```

### Task 2: 将样例页替换为真实工具页骨架

**Files:**
- Create: `plugins-src/MedicalDrugImport.Plugin/ViewModels/DrugImportPluginViewModel.cs`
- Modify: `plugins-src/MedicalDrugImport.Plugin/Views/MedicalDrugImportHomeView.axaml`
- Modify: `plugins-src/MedicalDrugImport.Plugin/Views/MedicalDrugImportHomeView.axaml.cs`
- Modify: `plugins-src/MedicalDrugImport.Plugin/MedicalDrugImportPlugin.cs`
- Test: `digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginViewTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 插件页面包含文件路径、预检、导入、同步操作区
- 插件入口返回的页面不再是纯样例文本页
- 页面加载不依赖宿主 ViewModel

**步骤 2：运行测试并确认先失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginViewTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 新增 `DrugImportPluginViewModel`
- 将样例页面改为真实工具页骨架
- 插件返回该工具页

**步骤 4：运行测试并确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginViewTests`
Expected: PASS

**Step 5: Commit**

```bash
git add plugins-src/MedicalDrugImport.Plugin/ViewModels/DrugImportPluginViewModel.cs plugins-src/MedicalDrugImport.Plugin/Views/MedicalDrugImportHomeView.axaml plugins-src/MedicalDrugImport.Plugin/Views/MedicalDrugImportHomeView.axaml.cs plugins-src/MedicalDrugImport.Plugin/MedicalDrugImportPlugin.cs digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginViewTests.cs
git commit -m "feat: replace sample plugin page with tool shell"
```

### Task 3: 迁移 Excel 预检到插件内

**Files:**
- Create: `plugins-src/MedicalDrugImport.Plugin/Models/DrugImportPreview.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Models/DrugImportRow.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Services/IDrugExcelImportService.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Services/DrugExcelImportService.cs`
- Modify: `plugins-src/MedicalDrugImport.Plugin/ViewModels/DrugImportPluginViewModel.cs`
- Test: `digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginExcelTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 插件能按固定模板检查 4 个工作表
- 插件能完成流式预检与行数统计
- 插件 ViewModel 能展示预检摘要

**步骤 2：运行测试并确认先失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginExcelTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 将 Excel 预检服务迁入插件项目
- 保持低内存流式实现
- 接入插件 ViewModel 的预检命令

**步骤 4：运行测试并确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginExcelTests`
Expected: PASS

**Step 5: Commit**

```bash
git add plugins-src/MedicalDrugImport.Plugin/Models/DrugImportPreview.cs plugins-src/MedicalDrugImport.Plugin/Models/DrugImportRow.cs plugins-src/MedicalDrugImport.Plugin/Services/IDrugExcelImportService.cs plugins-src/MedicalDrugImport.Plugin/Services/DrugExcelImportService.cs plugins-src/MedicalDrugImport.Plugin/ViewModels/DrugImportPluginViewModel.cs digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginExcelTests.cs
git commit -m "feat: move excel validation into plugin"
```

### Task 4: 迁移 PostgreSQL 导入链路到插件内

**Files:**
- Create: `plugins-src/MedicalDrugImport.Plugin/Models/DrugImportBatch.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Services/IDrugImportRepository.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Services/DrugImportRepository.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Services/IDrugImportPipelineService.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Services/DrugImportPipelineService.cs`
- Modify: `plugins-src/MedicalDrugImport.Plugin/ViewModels/DrugImportPluginViewModel.cs`
- Test: `digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginPostgresTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 插件可使用自身配置连接 PostgreSQL
- 可写入 `raw / clean / error`
- 可执行批次合并并返回摘要

**步骤 2：运行测试并确认先失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginPostgresTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 迁移 PostgreSQL 仓储与导入流水线
- 插件从 `plugin.settings.json` / 环境变量读取 PostgreSQL 连接串
- 接入插件 ViewModel 的“导入入库”

**步骤 4：运行测试并确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginPostgresTests`
Expected: PASS

**Step 5: Commit**

```bash
git add plugins-src/MedicalDrugImport.Plugin/Models/DrugImportBatch.cs plugins-src/MedicalDrugImport.Plugin/Services/IDrugImportRepository.cs plugins-src/MedicalDrugImport.Plugin/Services/DrugImportRepository.cs plugins-src/MedicalDrugImport.Plugin/Services/IDrugImportPipelineService.cs plugins-src/MedicalDrugImport.Plugin/Services/DrugImportPipelineService.cs plugins-src/MedicalDrugImport.Plugin/ViewModels/DrugImportPluginViewModel.cs digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginPostgresTests.cs
git commit -m "feat: move postgres import pipeline into plugin"
```

### Task 5: 迁移 SQL Server 同步链路到插件内

**Files:**
- Create: `plugins-src/MedicalDrugImport.Plugin/Services/IDrugCatalogSyncRepository.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Services/ISqlServerDrugSyncService.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Services/SqlServerDrugSyncService.cs`
- Modify: `plugins-src/MedicalDrugImport.Plugin/ViewModels/DrugImportPluginViewModel.cs`
- Test: `digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginSqlServerTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 插件可使用自身配置连接 SQL Server
- 可按批次执行 upsert
- 可记录同步摘要
- 同步失败时插件页展示错误而宿主不崩

**步骤 2：运行测试并确认先失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginSqlServerTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 迁移 SQL Server 同步服务
- 插件从自身配置读取 SQL Server 连接串
- 接入插件 ViewModel 的同步与重试同步命令

**步骤 4：运行测试并确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginSqlServerTests`
Expected: PASS

**Step 5: Commit**

```bash
git add plugins-src/MedicalDrugImport.Plugin/Services/IDrugCatalogSyncRepository.cs plugins-src/MedicalDrugImport.Plugin/Services/ISqlServerDrugSyncService.cs plugins-src/MedicalDrugImport.Plugin/Services/SqlServerDrugSyncService.cs plugins-src/MedicalDrugImport.Plugin/ViewModels/DrugImportPluginViewModel.cs digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginSqlServerTests.cs
git commit -m "feat: move sql server sync into plugin"
```

### Task 6: 更新运行时发布与文档

**Files:**
- Modify: `plugins/MedicalDrugImport/plugin.settings.json`
- Modify: `plugins/MedicalDrugImport/` 发布产物
- Modify: `digital-intelligence-bridge/README.md`
- Modify: `docs/plans/2026-03-18-medical-drug-import-plugin-externalization-design.md`
- Test: `digital-intelligence-bridge.UnitTests/PluginRuntimeDiscoveryTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 运行时插件目录包含 `plugin.settings.json`
- 发布后的插件仍可被宿主发现
- 插件目录缺少配置文件时仍能加载默认配置

**步骤 2：运行测试并确认先失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginRuntimeDiscoveryTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 补充运行时插件目录中的配置文件
- 更新 README 中的发布与配置说明
- 同步设计文档中的已实现状态

**步骤 4：运行测试并确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginRuntimeDiscoveryTests`
Expected: PASS

**Step 5: Commit**

```bash
git add plugins/MedicalDrugImport digital-intelligence-bridge/README.md docs/plans/2026-03-18-medical-drug-import-plugin-externalization-design.md digital-intelligence-bridge.UnitTests/PluginRuntimeDiscoveryTests.cs
git commit -m "docs: update plugin runtime packaging guidance"
```

### Task 7: 整体验证

**Files:**
- Verify: `plugins-src/MedicalDrugImport.Plugin/`
- Verify: `plugins/MedicalDrugImport/`
- Verify: `digital-intelligence-bridge/`
- Verify: `digital-intelligence-bridge.UnitTests/`

**Step 1: Write the failing test**

无新增测试代码，执行完整回归。

**步骤 2：运行测试并确认先失败**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Debug -p:UseSharedCompilation=false`
Expected: 若插件依赖、配置、页面绑定存在问题则失败

**Step 3: Write minimal implementation**

修复：

- 插件配置加载错误
- 插件页面绑定错误
- PostgreSQL / SQL Server 配置读取错误
- 运行时插件发布遗漏

**步骤 4：运行测试并确认通过**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Debug -p:UseSharedCompilation=false`
Expected: PASS

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Release -p:UseSharedCompilation=false`
Expected: PASS

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug -p:UseSharedCompilation=false`
Expected: PASS

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: PASS

**Step 5: Commit**

```bash
git add .
git commit -m "feat: externalize medical drug import business plugin"
```


