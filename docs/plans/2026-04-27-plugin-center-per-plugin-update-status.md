# 插件中心逐插件更新状态实施计划

> **执行要求：** 实施时使用 `superpowers:executing-plans`，按任务逐项执行并在每项后验证。

**目标：** 让插件中心逐项显示插件当前版本、最新版本和更新状态，支持多插件场景下清晰判断哪些插件可更新、未安装或待重启。

**架构：** 扩展 `PluginCenterViewModel` 的合并逻辑，把本地 runtime、staging 和发布中心授权插件明细合并为统一列表。UI 增加当前版本、最新版本和详情列，检查更新后复用 `IPluginUpdateOrchestrator` 并解析 `AuthorizedPluginDetail` 刷新状态。

**技术栈：** .NET、Avalonia XAML、Prism `DelegateCommand`、xUnit。

---

### 任务 1：状态合并模型

**文件：**
- 修改：`digital-intelligence-bridge/ViewModels/PluginCenterViewModel.cs`
- 修改：`digital-intelligence-bridge.UnitTests/PluginCenterViewModelTests.cs`

**步骤 1：编写失败测试**

在 `PluginCenterViewModelTests` 新增：

```csharp
[Fact]
public async Task RefreshAsync_ShouldMarkPluginUpdatable_WhenAuthorizedVersionIsNewer()
{
    using var sandbox = new TestConfigSandbox();
    CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.2");
    var vm = new PluginCenterViewModel(
        new NullLoggerService<PluginCenterViewModel>(),
        Options.Create(CreateSettings(sandbox)),
        null);

    vm.SetAvailablePluginsForTesting("就诊登记 / patient-registration / 1.0.3");
    await vm.RefreshAsync();

    var item = Assert.Single(vm.PluginItems);
    Assert.Equal("1.0.2", item.CurrentVersion);
    Assert.Equal("1.0.3", item.LatestVersion);
    Assert.Equal("可更新", item.Status);
}
```

再新增：

- 授权存在、本地不存在时显示 `未安装`。
- 授权版本等于本地版本时显示 `已最新`。
- staging 存在时优先显示 `待重启生效`。

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter FullyQualifiedName~PluginCenterViewModelTests --property:BaseOutputPath=.tmp\test-bin\
```

预期：失败，因为 `CurrentVersion`、`LatestVersion` 和授权合并逻辑尚不存在。

**步骤 3：实现模型**

修改 `PluginCenterItem`：

- 新增 `CurrentVersion`
- 新增 `LatestVersion`
- 新增 `Detail`
- `Version` 保留为兼容别名，返回 `CurrentVersion`

修改 `PluginCenterViewModel`：

- 新增内部记录 `PluginCenterAvailablePlugin`。
- 新增解析方法 `ParseAvailablePlugins(string detail)`。
- 新增测试辅助方法 `SetAvailablePluginsForTesting(string detail)`。
- `RefreshAsync` 合并 runtime、staging、available 三类数据。
- 状态判断优先级：
  1. staging 存在：`待重启生效`
  2. available 存在但 runtime 不存在：`未安装`
  3. available 版本高于 runtime：`可更新`
  4. available 版本等于 runtime：`已最新`
  5. 只有 runtime：`已生效`

**步骤 4：运行测试并确认通过**

运行同一条 `dotnet test`。

预期：`PluginCenterViewModelTests` 通过。

### 任务 2：检查更新后刷新逐插件状态

**文件：**
- 修改：`digital-intelligence-bridge/ViewModels/PluginCenterViewModel.cs`
- 修改：`digital-intelligence-bridge.UnitTests/PluginCenterViewModelTests.cs`

**步骤 1：编写失败测试**

新增测试：

```csharp
[Fact]
public async Task CheckUpdatesCommand_ShouldParseAuthorizedPluginsAndRefreshStatuses()
{
    using var sandbox = new TestConfigSandbox();
    CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.2");
    var orchestrator = new StubPluginUpdateOrchestrator(new PluginUpdateRunResult(
        true,
        "发现插件更新",
        string.Empty,
        false,
        DateTime.Now,
        new ReleaseCenterCheckResult(true, "ok", "client", "plugin", "detail", "site", "authorized", "就诊登记 / patient-registration / 1.0.3"),
        null,
        null));
    var vm = new PluginCenterViewModel(
        new NullLoggerService<PluginCenterViewModel>(),
        Options.Create(CreateSettings(sandbox)),
        orchestrator);

    vm.CheckUpdatesCommand.Execute();
    await WaitUntilAsync(() => !vm.IsBusy);

    var item = Assert.Single(vm.PluginItems);
    Assert.Equal("可更新", item.Status);
    Assert.Equal("1.0.3", item.LatestVersion);
}
```

**步骤 2：运行测试并确认失败**

运行同一条 `dotnet test`。

预期：失败，因为检查更新后尚未解析授权插件明细。

**步骤 3：实现检查更新刷新**

在 `CheckUpdatesAsync` 中：

- 如果 `result.CheckResult` 不为空，解析 `AuthorizedPluginDetail` 到 `_availablePlugins`。
- 调用 `RefreshAsync` 后，列表按最新授权状态显示。
- 如果 `result.RestartRequired`，保留 `LastPrepareStatus`。

**步骤 4：运行测试并确认通过**

运行同一条 `dotnet test`。

预期：新增测试通过。

### 任务 3：插件中心界面列调整

**文件：**
- 修改：`digital-intelligence-bridge/Views/PluginCenterView.axaml`
- 修改：`digital-intelligence-bridge.UnitTests/PluginCenterViewModelTests.cs`

**步骤 1：编写失败测试**

新增 XAML 结构测试：

```csharp
[Fact]
public void PluginCenterViewAxaml_ShouldShowCurrentAndLatestVersionColumns()
{
    var fullPath = FindRepositoryFile("digital-intelligence-bridge", "Views", "PluginCenterView.axaml");
    var xaml = File.ReadAllText(fullPath);

    Assert.Contains("当前版本", xaml);
    Assert.Contains("最新版本", xaml);
    Assert.Contains("Detail", xaml);
}
```

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter FullyQualifiedName~PluginCenterViewModelTests --property:BaseOutputPath=.tmp\test-bin\
```

预期：失败，因为当前页面仍只显示单一版本列。

**步骤 3：修改 XAML**

调整插件表格列：

- 插件名称
- 插件 ID
- 当前版本
- 最新版本
- 状态
- 详情

总览卡片改为：

- 已最新
- 可更新
- 待重启
- 异常

**步骤 4：运行测试并确认通过**

运行同一条 `dotnet test`。

预期：XAML 结构测试通过。

### 任务 4：验证和提交

**文件：**
- 验证：`digital-intelligence-bridge`
- 验证：`digital-intelligence-bridge.UnitTests`
- 验证：`docs/plans/2026-04-27-plugin-center-per-plugin-update-status-design.md`
- 验证：`docs/plans/2026-04-27-plugin-center-per-plugin-update-status.md`

**步骤 1：运行文档语言检查**

```powershell
scripts\check-doc-lang.ps1
```

预期：通过。

**步骤 2：运行目标测试**

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter FullyQualifiedName~PluginCenterViewModelTests --property:BaseOutputPath=.tmp\test-bin\
```

预期：通过。

**步骤 3：运行客户端构建**

```powershell
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal --property:BaseOutputPath=.tmp\client-build\
```

预期：构建成功。若出现 `NU1900`，按网络受限警告记录，不作为业务失败。

**步骤 4：暂存相关文件**

只暂存插件中心逐插件更新状态相关文件，避免混入既有无关工作区改动。

**步骤 5：提交**

```powershell
git commit -m "feat: 增强插件中心逐插件更新状态"
```
