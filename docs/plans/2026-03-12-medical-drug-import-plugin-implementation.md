# Medical Drug Import Plugin Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在当前 Avalonia 桌面应用中实现“医保药品导入同步工具”模块，支持固定模板 Excel 预检、导入 PostgreSQL、手工同步 SQL Server，并展示批次结果。

**Architecture:** 采用“内置工具模块 + 分层服务”方式实现，不直接构建外部 DLL 热插拔插件。UI 层仅负责文件选择、状态展示和命令触发；Excel 解析、PostgreSQL 入库、SQL Server 同步分别落在独立服务中，所有导入与同步都围绕 `batch_id` 进行追踪和重试。

**Tech Stack:** .NET 10、Avalonia UI、Prism.Avalonia、DryIoc、Microsoft.Extensions.Configuration、Npgsql、Microsoft.Data.SqlClient、流式 OpenXML 解析

---

### Task 1: 补充依赖与配置模型

**Files:**
- Modify: `digital-intelligence-bridge/digital-intelligence-bridge.csproj`
- Modify: `digital-intelligence-bridge/Configuration/AppSettings.cs`
- Modify: `digital-intelligence-bridge/appsettings.json`
- Test: `digital-intelligence-bridge.UnitTests/ConfigurationExtensionsTests.cs`

**Step 1: Write the failing test**

在 `ConfigurationExtensionsTests.cs` 中新增断言，验证配置可绑定以下新增节点：

- PostgreSQL 导入目标 schema
- SQL Server 连接参数
- 插件页开关或工具模块配置

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter ConfigurationExtensionsTests`
Expected: FAIL，提示新增配置字段不存在或绑定为空

**Step 3: Write minimal implementation**

- 在 `AppSettings.cs` 中新增导入工具配置模型
- 在 `appsettings.json` 中补充默认配置
- 在 `.csproj` 中加入实现所需依赖：
  - `Npgsql`
  - `Microsoft.Data.SqlClient`

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter ConfigurationExtensionsTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/digital-intelligence-bridge.csproj digital-intelligence-bridge/Configuration/AppSettings.cs digital-intelligence-bridge/appsettings.json digital-intelligence-bridge.UnitTests/ConfigurationExtensionsTests.cs
git commit -m "feat: add medical drug import configuration"
```

### Task 2: 定义导入批次与行模型

**Files:**
- Create: `digital-intelligence-bridge/Models/DrugImportBatch.cs`
- Create: `digital-intelligence-bridge/Models/DrugImportRow.cs`
- Create: `digital-intelligence-bridge/Models/DrugImportPreview.cs`
- Test: `digital-intelligence-bridge.UnitTests/ApplicationServiceTests.cs`

**Step 1: Write the failing test**

新增单元测试，验证模型能表达：

- `batch_id`
- 工作表名
- 行号
- 原始字段字典
- 标准化字段字典
- 错误消息
- 统计摘要

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter ApplicationServiceTests`
Expected: FAIL，提示相关类型不存在

**Step 3: Write minimal implementation**

创建导入批次、导入行、预检结果等模型，字段命名与后续数据库落点保持一致。

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter ApplicationServiceTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/Models/DrugImportBatch.cs digital-intelligence-bridge/Models/DrugImportRow.cs digital-intelligence-bridge/Models/DrugImportPreview.cs digital-intelligence-bridge.UnitTests/ApplicationServiceTests.cs
git commit -m "feat: add drug import models"
```

### Task 3: 定义 Excel 预检与解析接口

**Files:**
- Create: `digital-intelligence-bridge/Services/IDrugExcelImportService.cs`
- Create: `digital-intelligence-bridge/Services/DrugExcelTemplate.cs`
- Test: `digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs`

**Step 1: Write the failing test**

新增测试，要求接口至少支持：

- `ValidateAsync(string filePath, CancellationToken)`
- `ReadRowsAsync(string filePath, CancellationToken)`

并能表达固定 sheet 与表头规则。

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MainWindowViewModelTests`
Expected: FAIL，提示接口或模板类型不存在

**Step 3: Write minimal implementation**

- 新增服务接口
- 新增固定模板定义
- 将 4 个工作表和预期列名固化到模板中

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MainWindowViewModelTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/Services/IDrugExcelImportService.cs digital-intelligence-bridge/Services/DrugExcelTemplate.cs digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs
git commit -m "feat: define drug excel import contract"
```

### Task 4: 实现低内存 Excel 预检

**Files:**
- Create: `digital-intelligence-bridge/Services/DrugExcelImportService.cs`
- Modify: `digital-intelligence-bridge/Services/ServiceCollectionExtensions.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugExcelImportServiceTests.cs`

**Step 1: Write the failing test**

编写测试覆盖：

- 缺失工作表时报错
- 表头不匹配时报错
- 正常文件可返回各 sheet 统计
- 空行不会计入有效行数

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter DrugExcelImportServiceTests`
Expected: FAIL，提示服务未实现

**Step 3: Write minimal implementation**

使用 `System.IO.Packaging` 或 `ZipArchive + XmlReader` 流式读取 `.xlsx`：

- 只读取 workbook、relationships、shared strings、worksheet xml
- 预检阶段只取表头和有效行计数
- 不引入整表内存加载

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter DrugExcelImportServiceTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/Services/DrugExcelImportService.cs digital-intelligence-bridge/Services/ServiceCollectionExtensions.cs digital-intelligence-bridge.UnitTests/DrugExcelImportServiceTests.cs
git commit -m "feat: implement streaming excel validation"
```

### Task 5: 定义 PostgreSQL 导入仓储接口

**Files:**
- Create: `digital-intelligence-bridge/Services/IDrugImportRepository.cs`
- Create: `digital-intelligence-bridge/Services/IDrugCatalogSyncRepository.cs`
- Test: `digital-intelligence-bridge.UnitTests/SupabaseServiceTests.cs`

**Step 1: Write the failing test**

新增测试，约束仓储接口要支持：

- 创建批次
- 插入 raw
- 插入 clean
- 插入 error
- 合并业务表
- 查询批次影响记录

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter SupabaseServiceTests`
Expected: FAIL，提示接口不存在

**Step 3: Write minimal implementation**

新增仓储接口，方法名直接围绕 `batch_id` 和现有表结构设计。

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter SupabaseServiceTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/Services/IDrugImportRepository.cs digital-intelligence-bridge/Services/IDrugCatalogSyncRepository.cs digital-intelligence-bridge.UnitTests/SupabaseServiceTests.cs
git commit -m "feat: define drug import repository contracts"
```

### Task 6: 实现 PostgreSQL 导入仓储

**Files:**
- Create: `digital-intelligence-bridge/Services/DrugImportRepository.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugImportRepositoryTests.cs`

**Step 1: Write the failing test**

编写测试验证 SQL 生成与参数绑定逻辑：

- raw 写入包含 `batch_id/source_sheet/row_no/row_data`
- clean 写入包含 `biz_key/normalized_data`
- error 写入包含错误码与消息
- 合并阶段按 `drug_code` 执行 upsert

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter DrugImportRepositoryTests`
Expected: FAIL

**Step 3: Write minimal implementation**

使用 `Npgsql` 实现仓储，并将 SQL 拼装与参数化执行封装到仓储层。

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter DrugImportRepositoryTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/Services/DrugImportRepository.cs digital-intelligence-bridge.UnitTests/DrugImportRepositoryTests.cs
git commit -m "feat: implement postgres drug import repository"
```

### Task 7: 实现导入流水线服务

**Files:**
- Create: `digital-intelligence-bridge/Services/IDrugImportPipelineService.cs`
- Create: `digital-intelligence-bridge/Services/DrugImportPipelineService.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugImportPipelineServiceTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 能为一次导入生成 `batch_id`
- 对有效行写 raw + clean
- 对错误行写 error
- 导入结束后调用合并逻辑
- 统计新增、更新、错误数量

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter DrugImportPipelineServiceTests`
Expected: FAIL

**Step 3: Write minimal implementation**

实现流水线编排：

- 调用 Excel 服务流式枚举数据
- 进行字段标准化
- 调用仓储落 raw/clean/error
- 最后执行业务表合并

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter DrugImportPipelineServiceTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/Services/IDrugImportPipelineService.cs digital-intelligence-bridge/Services/DrugImportPipelineService.cs digital-intelligence-bridge.UnitTests/DrugImportPipelineServiceTests.cs
git commit -m "feat: add drug import pipeline service"
```

### Task 8: 定义并实现 SQL Server 同步服务

**Files:**
- Create: `digital-intelligence-bridge/Services/ISqlServerDrugSyncService.cs`
- Create: `digital-intelligence-bridge/Services/SqlServerDrugSyncService.cs`
- Test: `digital-intelligence-bridge.UnitTests/SqlServerDrugSyncServiceTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 能按 `drug_code` 查询本批次影响记录
- 能执行目标表 upsert
- Excel 未提供值时不会无脑清空目标表已有值
- 同步完成后写 `dbo.yb_同步记录`

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter SqlServerDrugSyncServiceTests`
Expected: FAIL

**Step 3: Write minimal implementation**

使用 `Microsoft.Data.SqlClient` 实现：

- SQL Server 连接
- 参数化 upsert
- 同步摘要写入
- 失败信息透出

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter SqlServerDrugSyncServiceTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/Services/ISqlServerDrugSyncService.cs digital-intelligence-bridge/Services/SqlServerDrugSyncService.cs digital-intelligence-bridge.UnitTests/SqlServerDrugSyncServiceTests.cs
git commit -m "feat: add sql server drug sync service"
```

### Task 9: 新增工具页面 ViewModel

**Files:**
- Create: `digital-intelligence-bridge/ViewModels/DrugImportViewModel.cs`
- Modify: `digital-intelligence-bridge/Services/ServiceCollectionExtensions.cs`
- Test: `digital-intelligence-bridge.UnitTests/DrugImportViewModelTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 选择文件后可执行预检
- 预检通过后可执行导入
- 导入成功后可执行 SQL Server 同步
- 执行期间按钮禁用
- 错误信息和统计可正确更新

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter DrugImportViewModelTests`
Expected: FAIL

**Step 3: Write minimal implementation**

新增独立 ViewModel，不把导入细节塞进现有 `MainWindowViewModel`。

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter DrugImportViewModelTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/ViewModels/DrugImportViewModel.cs digital-intelligence-bridge/Services/ServiceCollectionExtensions.cs digital-intelligence-bridge.UnitTests/DrugImportViewModelTests.cs
git commit -m "feat: add drug import view model"
```

### Task 10: 新增工具页面视图

**Files:**
- Create: `digital-intelligence-bridge/Views/DrugImportView.axaml`
- Create: `digital-intelligence-bridge/Views/DrugImportView.axaml.cs`
- Modify: `digital-intelligence-bridge/ViewLocator.cs`
- Test: `digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs`

**Step 1: Write the failing test**

补充测试，验证新增视图类型后：

- 能被定位器解析
- 不影响现有页面切换

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MainWindowViewModelTests`
Expected: FAIL

**Step 3: Write minimal implementation**

新增工具页 XAML，只展示：

- 文件路径
- 预检结果
- 执行按钮
- 批次统计
- 错误样例

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MainWindowViewModelTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/Views/DrugImportView.axaml digital-intelligence-bridge/Views/DrugImportView.axaml.cs digital-intelligence-bridge/ViewLocator.cs digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs
git commit -m "feat: add drug import view"
```

### Task 11: 将工具页面接入主导航

**Files:**
- Modify: `digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs`
- Modify: `digital-intelligence-bridge/Configuration/AppSettings.cs`
- Modify: `digital-intelligence-bridge/appsettings.json`
- Test: `digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs`

**Step 1: Write the failing test**

补充导航测试，验证：

- 新菜单项可显示
- 新页面可打开并进入 Tab
- 不破坏现有 Home/Todo/Settings

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MainWindowViewModelTests`
Expected: FAIL

**Step 3: Write minimal implementation**

在主导航中新增“医保药品导入同步工具”入口，并为页面分配新的视图类型或工具页类型。

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MainWindowViewModelTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs digital-intelligence-bridge/Configuration/AppSettings.cs digital-intelligence-bridge/appsettings.json digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs
git commit -m "feat: wire drug import tool into navigation"
```

### Task 12: 增加批次历史与重试入口

**Files:**
- Modify: `digital-intelligence-bridge/ViewModels/DrugImportViewModel.cs`
- Modify: `digital-intelligence-bridge/Views/DrugImportView.axaml`
- Test: `digital-intelligence-bridge.UnitTests/DrugImportViewModelTests.cs`

**Step 1: Write the failing test**

新增测试验证：

- 能记录最近一次批次摘要
- SQL Server 同步失败后可对同一 `batch_id` 重试
- 重试不会重新解析 Excel

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter DrugImportViewModelTests`
Expected: FAIL

**Step 3: Write minimal implementation**

在 ViewModel 中增加最近批次上下文和“重试同步”命令。

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter DrugImportViewModelTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/ViewModels/DrugImportViewModel.cs digital-intelligence-bridge/Views/DrugImportView.axaml digital-intelligence-bridge.UnitTests/DrugImportViewModelTests.cs
git commit -m "feat: add drug import sync retry"
```

### Task 13: 文档与手工验证说明

**Files:**
- Modify: `digital-intelligence-bridge/README.md`
- Modify: `docs/plans/2026-03-12-medical-drug-import-plugin-design.md`

**Step 1: Write the failing test**

无自动化测试；改为定义手工验证清单。

**Step 2: Run test to verify it fails**

无

**Step 3: Write minimal implementation**

更新 README，补充：

- Excel 模板要求
- 导入步骤
- SQL Server 配置要求
- 重试同步说明

**Step 4: Run test to verify it passes**

手工检查文档与实际实现一致。

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/README.md docs/plans/2026-03-12-medical-drug-import-plugin-design.md
git commit -m "docs: add drug import usage notes"
```

### Task 14: 整体验证

**Files:**
- Verify: `digital-intelligence-bridge/`
- Verify: `digital-intelligence-bridge.UnitTests/`

**Step 1: Write the failing test**

无新增测试代码，执行完整回归。

**Step 2: Run test to verify it fails**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Debug`
Expected: 若有编译问题则失败

**Step 3: Write minimal implementation**

修复编译错误、绑定错误、注册遗漏和命令状态问题。

**Step 4: Run test to verify it passes**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Debug`
Expected: PASS

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Release`
Expected: PASS

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug`
Expected: PASS

**Step 5: Commit**

```bash
git add .
git commit -m "feat: complete medical drug import tool"
```
