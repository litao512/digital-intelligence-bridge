# 外部插件宿主化改造实施计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 为当前 Avalonia 桌面应用建立第一版外部插件宿主能力，支持独立 DLL 插件发现、清单读取、菜单接入与页面承载，并以“医保药品导入”作为第一个外部插件样例。

**Architecture:** 宿主程序与插件之间通过独立的 `Plugin.Abstractions` 契约项目通信，主程序只负责发现、加载、菜单接入和页面承载，不直接把内部 Prism/DryIoc 容器暴露给插件。第一阶段只验证“外部 DLL 加载 + 菜单接入 + 页面打开”闭环，不迁移 Excel、PostgreSQL、SQL Server 业务逻辑。

**Tech Stack:** .NET 10、Avalonia UI、Prism.Avalonia、DryIoc、AssemblyLoadContext、反射加载、JSON 插件清单、xUnit

---

### Task 1: 建立插件契约项目

**Files:**
- Create: `DigitalIntelligenceBridge.Plugin.Abstractions/DigitalIntelligenceBridge.Plugin.Abstractions.csproj`
- Create: `DigitalIntelligenceBridge.Plugin.Abstractions/IPluginModule.cs`
- Create: `DigitalIntelligenceBridge.Plugin.Abstractions/PluginManifest.cs`
- Create: `DigitalIntelligenceBridge.Plugin.Abstractions/PluginMenuItem.cs`
- Create: `DigitalIntelligenceBridge.Plugin.Abstractions/IPluginHostContext.cs`
- Modify: `digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj`
- Test: `digital-intelligence-bridge.UnitTests/PluginAbstractionsContractTests.cs`

**Step 1: Write the failing test**

在 `PluginAbstractionsContractTests.cs` 中新增测试，约束第一版插件契约至少具备：

- `IPluginModule.Initialize(IPluginHostContext context)`
- `IPluginModule.GetManifest()`
- `IPluginModule.CreateMenuItems()`
- `IPluginModule.CreateContent(string menuId)`
- `PluginManifest` 包含 `Id/Name/Version/EntryAssembly/EntryType/MinHostVersion`
- `PluginMenuItem` 包含 `Id/Name/Icon/Order`

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginAbstractionsContractTests`
Expected: FAIL，提示契约项目或类型不存在

**Step 3: Write minimal implementation**

- 新建 `DigitalIntelligenceBridge.Plugin.Abstractions` 项目
- 仅放第一版最小宿主契约
- 不放数据库、容器、Prism 特定类型

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginAbstractionsContractTests`
Expected: PASS

**Step 5: Commit**

```bash
git add DigitalIntelligenceBridge.Plugin.Abstractions digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj digital-intelligence-bridge.UnitTests/PluginAbstractionsContractTests.cs
git commit -m "feat: add plugin abstractions project"
```

### Task 2: 建立插件宿主项目

**Files:**
- Create: `DigitalIntelligenceBridge.Plugin.Host/DigitalIntelligenceBridge.Plugin.Host.csproj`
- Create: `DigitalIntelligenceBridge.Plugin.Host/PluginCatalogService.cs`
- Create: `DigitalIntelligenceBridge.Plugin.Host/PluginLoaderService.cs`
- Create: `DigitalIntelligenceBridge.Plugin.Host/PluginLoadContext.cs`
- Create: `DigitalIntelligenceBridge.Plugin.Host/LoadedPlugin.cs`
- Modify: `digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj`
- Test: `digital-intelligence-bridge.UnitTests/PluginCatalogServiceTests.cs`

**Step 1: Write the failing test**

新增测试覆盖：

- 能扫描 `plugins/*/plugin.json`
- 缺失清单时跳过插件目录
- 清单缺关键字段时标记无效
- 可返回规范化的插件清单集合

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginCatalogServiceTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 新建宿主项目
- 先只实现目录扫描与清单解析
- `PluginLoaderService` 先保留最小入口，不急着做复杂错误边界

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginCatalogServiceTests`
Expected: PASS

**Step 5: Commit**

```bash
git add DigitalIntelligenceBridge.Plugin.Host digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj digital-intelligence-bridge.UnitTests/PluginCatalogServiceTests.cs
git commit -m "feat: add plugin host catalog services"
```

### Task 3: 实现 DLL 加载与入口实例化

**Files:**
- Modify: `DigitalIntelligenceBridge.Plugin.Host/PluginLoaderService.cs`
- Modify: `DigitalIntelligenceBridge.Plugin.Host/PluginLoadContext.cs`
- Test: `digital-intelligence-bridge.UnitTests/PluginLoaderServiceTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 按 `plugin.json` 中的 `entryAssembly` 和 `entryType` 定位入口
- 能创建 `IPluginModule` 实例
- 插件入口类型不实现契约时拒绝加载
- 加载失败时返回可记录的错误结果，而不是直接抛到 UI

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginLoaderServiceTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 用 `AssemblyLoadContext` 从插件目录加载主 DLL
- 创建入口实例
- 验证其实现了 `IPluginModule`
- 返回 `LoadedPlugin`，包含清单、实例、目录和错误信息

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginLoaderServiceTests`
Expected: PASS

**Step 5: Commit**

```bash
git add DigitalIntelligenceBridge.Plugin.Host/PluginLoaderService.cs DigitalIntelligenceBridge.Plugin.Host/PluginLoadContext.cs digital-intelligence-bridge.UnitTests/PluginLoaderServiceTests.cs
git commit -m "feat: implement plugin assembly loading"
```

### Task 4: 为宿主定义插件上下文

**Files:**
- Create: `DigitalIntelligenceBridge.Plugin.Host/PluginHostContext.cs`
- Modify: `DigitalIntelligenceBridge.Plugin.Abstractions/IPluginHostContext.cs`
- Test: `digital-intelligence-bridge.UnitTests/PluginHostContextTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 宿主上下文能提供插件目录
- 能提供宿主版本
- 能提供基础日志入口
- 不暴露主程序容器、主窗口或 ViewModel

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginHostContextTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 在 `IPluginHostContext` 中只保留最小接口
- 由宿主项目实现 `PluginHostContext`
- 宿主只向插件暴露必要信息，不暴露 DryIoc/Prism

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginHostContextTests`
Expected: PASS

**Step 5: Commit**

```bash
git add DigitalIntelligenceBridge.Plugin.Host/PluginHostContext.cs DigitalIntelligenceBridge.Plugin.Abstractions/IPluginHostContext.cs digital-intelligence-bridge.UnitTests/PluginHostContextTests.cs
git commit -m "feat: add plugin host context"
```

### Task 5: 将插件宿主接入主程序启动

**Files:**
- Modify: `digital-intelligence-bridge/digital-intelligence-bridge.csproj`
- Modify: `digital-intelligence-bridge/App.axaml.cs`
- Modify: `digital-intelligence-bridge/Services/ServiceCollectionExtensions.cs`
- Test: `digital-intelligence-bridge.UnitTests/AppPluginRegistrationTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 主程序项目引用 `Plugin.Abstractions` 与 `Plugin.Host`
- 容器中可解析 `PluginCatalogService`、`PluginLoaderService`
- 宿主启动时能够创建插件上下文服务

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter AppPluginRegistrationTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 引入两个新项目引用
- 在 `App.axaml.cs` 和 `ServiceCollectionExtensions.cs` 注册插件宿主服务
- 不在这一步耦合菜单或 UI

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter AppPluginRegistrationTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/digital-intelligence-bridge.csproj digital-intelligence-bridge/App.axaml.cs digital-intelligence-bridge/Services/ServiceCollectionExtensions.cs digital-intelligence-bridge.UnitTests/AppPluginRegistrationTests.cs
git commit -m "feat: register plugin host services"
```

### Task 6: 定义主窗口插件菜单接入点

**Files:**
- Modify: `digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs`
- Modify: `digital-intelligence-bridge/Configuration/AppSettings.cs`
- Test: `digital-intelligence-bridge.UnitTests/MainWindowPluginNavigationTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 主窗口可附加来自外部插件的菜单项
- 插件菜单不再复用“已安装外部业务插件”那套模糊语义
- 内置模块与外部插件菜单可同时存在

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MainWindowPluginNavigationTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 在 `MainWindowViewModel` 中区分：
  - 内置菜单
  - 外部插件菜单
- 新增插件菜单专用的 `MainViewType` 或 `TabType` 承载标识
- 保持原“医保药品导入”内置模块逻辑不再继续扩散

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MainWindowPluginNavigationTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs digital-intelligence-bridge/Configuration/AppSettings.cs digital-intelligence-bridge.UnitTests/MainWindowPluginNavigationTests.cs
git commit -m "feat: add plugin menu injection to main window"
```

### Task 7: 新增通用插件内容承载视图

**Files:**
- Create: `digital-intelligence-bridge/Views/PluginHostView.axaml`
- Create: `digital-intelligence-bridge/Views/PluginHostView.axaml.cs`
- Create: `digital-intelligence-bridge/ViewModels/PluginHostViewModel.cs`
- Modify: `digital-intelligence-bridge/Views/MainWindow.axaml`
- Modify: `digital-intelligence-bridge/ViewLocator.cs`
- Test: `digital-intelligence-bridge.UnitTests/PluginHostViewTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 点击插件菜单后能创建统一承载页面
- 宿主视图能显示插件返回的 `Control`
- 插件页面创建失败时显示错误占位，而不是主窗口崩溃

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginHostViewTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 新增 `PluginHostViewModel`
- 新增 `PluginHostView`
- 在 `MainWindow.axaml` 的 Tab 内容模板中加入插件承载分支
- 先只支持单页面插件内容，不做复杂页面路由

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginHostViewTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/Views/PluginHostView.axaml digital-intelligence-bridge/Views/PluginHostView.axaml.cs digital-intelligence-bridge/ViewModels/PluginHostViewModel.cs digital-intelligence-bridge/Views/MainWindow.axaml digital-intelligence-bridge/ViewLocator.cs digital-intelligence-bridge.UnitTests/PluginHostViewTests.cs
git commit -m "feat: add plugin host view"
```

### Task 8: 创建第一个外部插件样例项目

**Files:**
- Create: `plugins-src/MedicalDrugImport.Plugin/MedicalDrugImport.Plugin.csproj`
- Create: `plugins-src/MedicalDrugImport.Plugin/MedicalDrugImportPlugin.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/Views/MedicalDrugImportHomeView.axaml`
- Create: `plugins-src/MedicalDrugImport.Plugin/Views/MedicalDrugImportHomeView.axaml.cs`
- Create: `plugins-src/MedicalDrugImport.Plugin/plugin.json`
- Test: `digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginContractTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 插件项目实现 `IPluginModule`
- `plugin.json` 与入口程序集、入口类型一致
- 插件至少返回一个菜单项和一个简单页面

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginContractTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 建立示例插件项目
- 页面只显示“医保药品导入插件加载成功”
- 先不接 Excel、数据库、同步功能

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter MedicalDrugImportPluginContractTests`
Expected: PASS

**Step 5: Commit**

```bash
git add plugins-src/MedicalDrugImport.Plugin digital-intelligence-bridge.UnitTests/MedicalDrugImportPluginContractTests.cs
git commit -m "feat: add external medical drug import plugin skeleton"
```

### Task 9: 将示例插件发布到运行时插件目录

**Files:**
- Create: `plugins/MedicalDrugImport/plugin.json`
- Create: `plugins/MedicalDrugImport/` 发布产物
- Modify: `digital-intelligence-bridge/README.md`
- Test: `digital-intelligence-bridge.UnitTests/PluginRuntimeDiscoveryTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 运行时 `plugins/MedicalDrugImport/plugin.json` 能被宿主发现
- 宿主能用运行时目录加载示例插件
- 插件目录缺文件时给出明确错误

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginRuntimeDiscoveryTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 约定 `plugins/MedicalDrugImport/` 为运行时目录
- 增加最小发布步骤或复制步骤
- README 补充插件目录规范和手工发布方式

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginRuntimeDiscoveryTests`
Expected: PASS

**Step 5: Commit**

```bash
git add plugins/MedicalDrugImport digital-intelligence-bridge/README.md digital-intelligence-bridge.UnitTests/PluginRuntimeDiscoveryTests.cs
git commit -m "feat: publish sample plugin to runtime directory"
```

### Task 10: 实现端到端菜单与页面联通

**Files:**
- Modify: `digital-intelligence-bridge/App.axaml.cs`
- Modify: `digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs`
- Modify: `digital-intelligence-bridge/Views/MainWindow.axaml`
- Test: `digital-intelligence-bridge.UnitTests/PluginEndToEndNavigationTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- 启动时能发现示例插件
- 左侧导航显示插件菜单
- 点击菜单能打开插件页面
- 插件页面失败时不影响 Home/Todo/Settings

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginEndToEndNavigationTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 在应用启动阶段加载插件清单
- 把插件菜单注入主窗口
- 点击插件菜单时创建 `PluginHostViewModel`
- 插件页面成功打开

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginEndToEndNavigationTests`
Expected: PASS

**Step 5: Commit**

```bash
git add digital-intelligence-bridge/App.axaml.cs digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs digital-intelligence-bridge/Views/MainWindow.axaml digital-intelligence-bridge.UnitTests/PluginEndToEndNavigationTests.cs
git commit -m "feat: wire external plugin into app navigation"
```

### Task 11: 增加错误边界与兼容性校验

**Files:**
- Modify: `DigitalIntelligenceBridge.Plugin.Host/PluginLoaderService.cs`
- Modify: `DigitalIntelligenceBridge.Plugin.Abstractions/PluginManifest.cs`
- Modify: `digital-intelligence-bridge/ViewModels/PluginHostViewModel.cs`
- Test: `digital-intelligence-bridge.UnitTests/PluginFailureHandlingTests.cs`

**Step 1: Write the failing test**

测试覆盖：

- `minHostVersion` 不兼容时拒绝加载
- 插件入口类型错误时记录失败
- 插件页面创建异常时显示错误视图
- 单个插件失败不影响其他插件显示

**Step 2: Run test to verify it fails**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginFailureHandlingTests`
Expected: FAIL

**Step 3: Write minimal implementation**

- 增加宿主版本比对
- 增加加载失败状态与错误信息
- 插件页面错误时回退到错误展示控件

**Step 4: Run test to verify it passes**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug --filter PluginFailureHandlingTests`
Expected: PASS

**Step 5: Commit**

```bash
git add DigitalIntelligenceBridge.Plugin.Host/PluginLoaderService.cs DigitalIntelligenceBridge.Plugin.Abstractions/PluginManifest.cs digital-intelligence-bridge/ViewModels/PluginHostViewModel.cs digital-intelligence-bridge.UnitTests/PluginFailureHandlingTests.cs
git commit -m "feat: add plugin failure boundaries"
```

### Task 12: 文档与人工验收

**Files:**
- Modify: `docs/plans/2026-03-12-medical-drug-import-plugin-design.md`
- Modify: `digital-intelligence-bridge/README.md`
- Modify: `docs/plans/2026-03-17-external-plugin-host-implementation.md`

**Step 1: Write the failing test**

无自动化测试；改为整理人工验收清单。

**Step 2: Run test to verify it fails**

无

**Step 3: Write minimal implementation**

文档补充：

- 第一阶段只验证外部插件宿主闭环
- `plugins/` 与 `plugins-src/` 目录约定
- `plugin.json` 字段说明
- 插件失败时的表现
- 下一阶段再迁移医保导入业务链路

**Step 4: Run test to verify it passes**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: PASS

**Step 5: Commit**

```bash
git add docs/plans/2026-03-12-medical-drug-import-plugin-design.md docs/plans/2026-03-17-external-plugin-host-implementation.md digital-intelligence-bridge/README.md
git commit -m "docs: add external plugin host plan"
```

### Task 13: 整体验证

**Files:**
- Verify: `DigitalIntelligenceBridge.Plugin.Abstractions/`
- Verify: `DigitalIntelligenceBridge.Plugin.Host/`
- Verify: `plugins-src/MedicalDrugImport.Plugin/`
- Verify: `digital-intelligence-bridge/`
- Verify: `digital-intelligence-bridge.UnitTests/`

**Step 1: Write the failing test**

无新增测试代码，执行完整回归。

**Step 2: Run test to verify it fails**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Debug`
Expected: 若有项目引用、加载、UI 绑定问题则失败

**Step 3: Write minimal implementation**

修复：

- 项目引用遗漏
- 插件目录扫描错误
- 页面承载绑定错误
- 插件菜单注入异常

**Step 4: Run test to verify it passes**

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
git commit -m "feat: add first external plugin host pipeline"
```

## 当前执行结果（2026-03-18）

本计划截至 2026 年 3 月 18 日已经完成 Task 1 到 Task 11，当前状态如下：

- 已新增 `DigitalIntelligenceBridge.Plugin.Abstractions`
- 已新增 `DigitalIntelligenceBridge.Plugin.Host`
- 已接入主程序启动链和导航菜单
- 已新增统一插件承载视图 `PluginHostView`
- 已新增样例插件 `plugins-src/MedicalDrugImport.Plugin`
- 已将样例插件发布到 `plugins/MedicalDrugImport/`
- 已实现运行时发现、加载、菜单打开和页面显示
- 已补充插件错误边界与 `minHostVersion` 兼容性校验

当前人工验收建议：

1. 启动主程序，确认左侧出现“医保药品导入”外部插件菜单
2. 点击菜单，确认页面显示“医保药品导入插件加载成功”
3. 暂时移走 `plugins/MedicalDrugImport/MedicalDrugImport.Plugin.dll`，确认宿主不崩溃且插件给出错误提示
4. 将 `plugin.json` 的 `minHostVersion` 临时提高，确认宿主拒绝加载该插件但不影响首页、待办、设置页面

下一阶段不在本计划内，建议单独立项：

- 把 Excel 导入、PostgreSQL 入库、SQL Server 同步逐步迁入 `MedicalDrugImport.Plugin`
- 为插件补充数据库连接工厂、配置读取和日志封装
