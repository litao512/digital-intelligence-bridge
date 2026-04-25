# 主程序配置收紧 Implementation Plan

> 执行说明：按任务顺序逐项实现，并在每个关键步骤后执行可验证检查。

**Goal:** 将主程序配置模型收敛为“宿主部署控制 + 本机状态”，删除插件业务资源的主程序兼容入口和旧兼容环境变量映射。

**Architecture:** 先统一文档口径，再删除 `ConfigurationExtensions` 中的旧兼容映射，随后移除主程序里的插件业务配置入口和相关测试。整个过程保持插件资源中心模型不变，不回退到主程序配置。验证采用串行 `dotnet build` / `dotnet test`。

**Tech Stack:** .NET 10、Avalonia、Microsoft.Extensions.Configuration、xUnit

---

### Task 1: 收口主程序侧配置文档

**Files:**
- Modify: `digital-intelligence-bridge/README.md`
- Modify: `docs/PRD.md`
- Modify: `docs/05-operations/CONFIGURATION_ARCHITECTURE_SPEC.md`
- Modify: `docs/standards/configuration-layering-principles.md`
- Test: `scripts/check-doc-lang.ps1`

**Step 1: 写文档检查点**

确认每份文档都不再描述以下内容：

- `MSSQL_DB_*` 作为正式入口
- `MedicalDrugImport.SqlServer` 作为插件业务连接来源
- 插件业务资源通过主程序环境变量进入系统

**Step 2: 修改文档**

将文档统一改为：

- 主程序只负责部署控制和本机状态
- 插件正式业务资源只来自资源中心
- `DIB_CONFIG_ROOT` 等宿主环境变量保留
- `MSSQL_DB_*` 属于删除目标，不再作为推荐路径

**Step 3: 运行文档检查**

Run: `powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: PASS

**Step 4: 提交**

```bash
git add digital-intelligence-bridge/README.md docs/PRD.md docs/05-operations/CONFIGURATION_ARCHITECTURE_SPEC.md docs/standards/configuration-layering-principles.md
git commit -m "docs: 收紧主程序配置边界"
```

### Task 2: 删除旧兼容环境变量映射

**Files:**
- Modify: `digital-intelligence-bridge/Configuration/ConfigurationExtensions.cs`
- Test: `digital-intelligence-bridge.UnitTests/ConfigurationExtensionsTests.cs`

**Step 1: 写失败测试**

新增或修改测试，描述：

- `MSSQL_DB_*` 不再映射到 `MedicalDrugImport:SqlServer:*`
- 主配置仍保留 `DIB_CONFIG_ROOT` 与普通宿主环境变量行为

**Step 2: 运行测试确认失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "ConfigurationExtensionsTests" --no-restore -m:1 -v minimal`
Expected: FAIL，原因是当前仍存在 `GetLegacySqlServerEnvironmentOverrides()` 映射

**Step 3: 删除最小实现**

修改 `ConfigurationExtensions.cs`：

- 移除 `.AddInMemoryCollection(GetLegacySqlServerEnvironmentOverrides())`
- 删除 `GetLegacySqlServerEnvironmentOverrides()`
- 删除 `MapLegacyEnv(...)`

**Step 4: 回跑测试**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "ConfigurationExtensionsTests" --no-restore -m:1 -v minimal`
Expected: PASS

**Step 5: 提交**

```bash
git add digital-intelligence-bridge/Configuration/ConfigurationExtensions.cs digital-intelligence-bridge.UnitTests/ConfigurationExtensionsTests.cs
git commit -m "refactor: remove legacy sqlserver env compatibility"
```

### Task 3: 删除主程序中的插件业务配置入口

**Files:**
- Modify: `digital-intelligence-bridge/Configuration/AppSettings.cs`
- Modify: `digital-intelligence-bridge/Services/SqlServerDrugSyncService.cs`
- Modify: `digital-intelligence-bridge/Services/DrugImportRepository.cs`
- Modify: `digital-intelligence-bridge/Services/ApplicationService.cs`
- Test: `digital-intelligence-bridge.UnitTests/SqlServerDrugSyncServiceTests.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugImportRepositoryTests.cs`
- Test: 其他依赖 `MedicalDrugImport.SqlServer` 或 `MedicalDrugImport.PostgresSchema` 的测试

**Step 1: 写失败测试**

补测试描述：

- 主程序配置对象不再包含插件业务连接配置段
- 主程序服务不再从 `AppSettings` 读取插件业务连接

**Step 2: 运行测试确认失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "SqlServerDrugSyncServiceTests|DrugImportRepositoryTests" --no-restore -m:1 -v minimal`
Expected: FAIL，原因是当前服务仍依赖主程序插件业务配置

**Step 3: 删除最小实现**

修改配置模型和服务：

- 从 `AppSettings` 中移除插件业务连接配置模型
- 让主程序相关服务不再承载插件业务连接逻辑
- 若这些服务已被插件版本替代，则同步删除不再需要的主程序服务和绑定

**Step 4: 回跑测试**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "SqlServerDrugSyncServiceTests|DrugImportRepositoryTests" --no-restore -m:1 -v minimal`
Expected: PASS

**Step 5: 提交**

```bash
git add digital-intelligence-bridge/Configuration/AppSettings.cs digital-intelligence-bridge/Services digital-intelligence-bridge.UnitTests/SqlServerDrugSyncServiceTests.cs digital-intelligence-bridge.UnitTests/DrugImportRepositoryTests.cs
git commit -m "refactor: remove host plugin resource settings"
```

### Task 4: 清理 README 与运维说明中的旧插件配置指引

**Files:**
- Modify: `digital-intelligence-bridge/README.md`
- Modify: `docs/05-operations/PLUGIN_PACKAGING_GUIDE.md`
- Modify: `docs/05-operations/CONFIGURATION_ARCHITECTURE_SPEC.md`
- Test: `scripts/check-doc-lang.ps1`

**Step 1: 定位旧表述**

搜索并删除：

- “插件可独立读取 `plugin.settings.json` 和环境变量”
- “生产环境优先通过环境变量覆盖连接串”
- “工具页依赖 `MedicalDrugImport.SqlServer` 配置”

**Step 2: 修改文档**

统一改为：

- 插件正式资源来自资源中心
- 开发模式本地敏感资源来自 `plugin.development.json`
- 主程序环境变量只用于宿主部署控制

**Step 3: 运行文档检查**

Run: `powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: PASS

**Step 4: 提交**

```bash
git add digital-intelligence-bridge/README.md docs/05-operations/PLUGIN_PACKAGING_GUIDE.md docs/05-operations/CONFIGURATION_ARCHITECTURE_SPEC.md
git commit -m "docs: remove host plugin config guidance"
```

### Task 5: 串行回归构建与测试

**Files:**
- Verify: `digital-intelligence-bridge/`
- Verify: `digital-intelligence-bridge.UnitTests/`

**Step 1: 构建主项目**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal`
Expected: PASS

**Step 2: 构建测试项目**

Run: `dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal`
Expected: PASS

**Step 3: 跑受影响测试**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build --filter "ConfigurationExtensionsTests|SqlServerDrugSyncServiceTests|DrugImportRepositoryTests|MedicalDrugImportPluginConfigurationTests|PatientRegistrationPluginConfigurationTests|PluginHostContextResourceTests|ReleaseCenterServiceTests" -v minimal`
Expected: PASS

**Step 4: 检查 diff 格式**

Run: `git diff --check`
Expected: PASS（允许仅有已有 LF/CRLF warning）

**Step 5: 提交**

```bash
git add -A
git commit -m "refactor: tighten host configuration model"
```
