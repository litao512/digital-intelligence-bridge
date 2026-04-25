# 药品导入业务插件化清理 Implementation Plan

> 执行说明：按任务顺序逐项实现，并在每个关键步骤后执行可验证检查。

**Goal:** 将药品导入业务代码彻底从主程序移除，并以 `MedicalDrugImport.Plugin` 作为唯一业务实现来源。

**Architecture:** 主程序只保留插件宿主职责，不再保留药品导入业务页面、业务接口与业务服务。主程序相关旧测试将删除或改写，药品导入业务覆盖统一由插件侧测试承担。整个过程坚持 TDD，先让旧测试表达“主程序不再承载该能力”，再删除实现并串行验证。

**Tech Stack:** .NET 10、Avalonia、xUnit、Prism

---

## 当前进度

截至 2026-04-20：

1. Task 1 已完成：主程序内置药品导入 UI 残留已删除。
2. Task 2 已完成：主程序药品导入模型、接口、服务副本已删除，相关测试已删改。
3. Task 3 正在进行：当前主要剩余工作是文档口径收口与手动回归说明。
4. Task 4 的构建与自动化验证已通过：
   - `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal`
   - `dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal`
   - `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -m:1 -v minimal`

以下任务明细保留为实施记录。

---

### Task 1: 删除主程序内置药品导入 UI 残留

**Files:**
- Delete: `digital-intelligence-bridge/Views/DrugImportView.axaml`
- Delete: `digital-intelligence-bridge/Views/DrugImportView.axaml.cs`
- Delete: `digital-intelligence-bridge/ViewModels/DrugImportViewModel.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugImportViewModelTests.cs`
- Test: `digital-intelligence-bridge.UnitTests/ViewLocatorTests.cs`
- Test: `digital-intelligence-bridge.UnitTests/MainWindowDrugImportViewTests.cs`

**Step 1: 写失败测试或改旧测试**

将旧测试改为表达：

- 主程序不再存在 `DrugImportViewModel`
- 主程序主窗口 XAML 不再包含 `DrugImportView`
- `ViewLocator` 不再为内置药品导入视图提供映射

**Step 2: 运行测试确认失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "DrugImportViewModelTests|ViewLocatorTests|MainWindowDrugImportViewTests" --no-restore -m:1 -v minimal`
Expected: FAIL，因为这些测试当前仍依赖内置药品导入 UI 类型

**Step 3: 删除最小实现**

- 删除 `DrugImportView.axaml`
- 删除 `DrugImportView.axaml.cs`
- 删除 `DrugImportViewModel.cs`
- 删除仅服务于这些类型的测试文件，或将其改写为“主程序不再暴露该内置页面”

**Step 4: 回跑测试**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "ViewLocatorTests|MainWindowDrugImportViewTests|MainWindowViewModelTests|MainWindowDrugImportNavigationTests" --no-restore -m:1 -v minimal`
Expected: PASS

### Task 2: 删除主程序药品导入业务接口与实现

**Files:**
- Delete: `digital-intelligence-bridge/Services/IDrugExcelImportService.cs`
- Delete: `digital-intelligence-bridge/Services/IDrugImportPipelineService.cs`
- Delete: `digital-intelligence-bridge/Services/IDrugImportRepository.cs`
- Delete: `digital-intelligence-bridge/Services/IDrugCatalogSyncRepository.cs`
- Delete: `digital-intelligence-bridge/Services/ISqlServerDrugSyncService.cs`
- Delete: `digital-intelligence-bridge/Services/DrugExcelImportService.cs`
- Delete: `digital-intelligence-bridge/Services/DrugImportPipelineService.cs`
- Delete: `digital-intelligence-bridge/Services/DrugImportRepository.cs`
- Delete: `digital-intelligence-bridge/Services/SqlServerDrugSyncService.cs`
- Delete: `digital-intelligence-bridge/Services/DrugExcelTemplate.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugExcelImportServiceTests.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugImportPipelineServiceTests.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugImportRepositoryTests.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugImportRepositoryContractTests.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugExcelImportContractTests.cs`
- Test: `digital-intelligence-bridge.UnitTests/SqlServerDrugSyncServiceTests.cs`

**Step 1: 写失败测试或改旧测试**

调整主程序测试口径为：

- 主程序不再声明这些接口与实现
- 插件业务覆盖统一留在插件侧测试

**Step 2: 运行测试确认失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "DrugExcelImportServiceTests|DrugImportPipelineServiceTests|DrugImportRepositoryTests|DrugImportRepositoryContractTests|DrugExcelImportContractTests|SqlServerDrugSyncServiceTests" --no-restore -m:1 -v minimal`
Expected: FAIL，因为测试当前仍引用主程序业务接口或实现

**Step 3: 删除最小实现**

- 删除主程序中上述接口与实现文件
- 删除或改写对应测试
- 确认插件侧已有等价实现，不回退到主程序共享库

**Step 4: 回跑测试**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "MedicalDrugImportPluginExcelTests|MedicalDrugImportPluginPipelineTests|MedicalDrugImportPluginPostgresTests|MedicalDrugImportPluginSqlServerTests|DrugExcelImportServiceTests|DrugImportRepositoryTests|SqlServerDrugSyncServiceTests" --no-restore -m:1 -v minimal`
Expected: PASS，其中主程序旧测试已删除或不再匹配，插件测试通过

### Task 3: 清理主程序与测试工程中的残留引用

**Files:**
- Modify: `digital-intelligence-bridge.UnitTests/*.cs` 中所有仍引用主程序药品导入类型的文件
- Modify: `digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj`
- Modify: `digital-intelligence-bridge/README.md`
- Modify: `docs/05-operations/PLUGIN_PACKAGING_GUIDE.md`
- Modify: `docs/plugin-development-conventions.md`

**Step 1: 全局搜索残留引用**

搜索：

- `DrugImportViewModel`
- `DrugImportView`
- `IDrugExcelImportService`
- `IDrugImportPipelineService`
- `IDrugImportRepository`
- `IDrugCatalogSyncRepository`
- `ISqlServerDrugSyncService`

**Step 2: 删除或改写引用**

- 主程序与主程序测试不再引用这些类型
- 插件测试统一使用插件命名空间与插件实现
- 文档明确药品导入为插件能力

**Step 3: 运行文档检查**

Run: `powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: PASS

### Task 4: 串行构建与回归验证

**Files:**
- Verify: `digital-intelligence-bridge/`
- Verify: `digital-intelligence-bridge.UnitTests/`
- Verify: `plugins-src/MedicalDrugImport.Plugin/`

**Step 1: 构建主项目**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal`
Expected: PASS

**Step 2: 构建测试项目**

Run: `dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal`
Expected: PASS

**Step 3: 跑受影响测试**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build --filter "MedicalDrugImportPluginConfigurationTests|MedicalDrugImportPluginViewTests|MedicalDrugImportPluginExcelTests|MedicalDrugImportPluginPipelineTests|MedicalDrugImportPluginPostgresTests|MedicalDrugImportPluginSqlServerTests|PluginHostViewTests|PluginEndToEndNavigationTests|PluginFailureHandlingTests|MainWindowPluginNavigationTests|MainWindowDrugImportNavigationTests|ReleaseCenterServiceTests|PluginHostContextResourceTests" -v minimal`
Expected: PASS

**Step 4: 检查 diff 格式**

Run: `git diff --check`
Expected: PASS（允许仅有已有 LF/CRLF warning）
