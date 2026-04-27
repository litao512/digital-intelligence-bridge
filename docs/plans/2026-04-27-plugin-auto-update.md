# 插件自动更新体验实施计划

> **执行要求：** 实施时使用 `superpowers:executing-plans`，按任务逐项执行并在每项后验证。

**目标：** 让客户端在手动检查、启动静默检查和站点初始化后自动完成插件检查、下载和预安装，并提示重启后生效。

**架构：** 新增轻量插件更新编排服务，复用现有 `IReleaseCenterService` 的检查、下载和预安装能力。首页 ViewModel 调用统一编排入口，主窗口初始化后延迟触发静默检查，失败只更新状态和日志，不阻塞主界面。

**技术栈：** .NET、Avalonia、Prism `DelegateCommand`、xUnit、现有发布中心服务接口。

---

### 任务 1：编排服务测试

**文件：**
- 新增：`digital-intelligence-bridge.UnitTests/PluginUpdateOrchestratorTests.cs`
- 新增：`digital-intelligence-bridge/Services/PluginUpdateOrchestrator.cs`

**步骤 1：编写失败测试**

新增测试覆盖成功路径：

```csharp
[Fact]
public async Task RunAsync_ShouldCheckDownloadAndPrepare_WhenPluginPackagesAvailable()
{
    var releaseCenter = new StubReleaseCenterService
    {
        CheckResult = new ReleaseCenterCheckResult(true, "发现 1 类可用更新", "客户端更新：暂无", "插件更新：1 个可用插件（就诊登记 1.0.3）", "detail", "站点正常", "授权插件：1 个", "就诊登记 / patient-registration / 1.0.3"),
        DownloadResult = new ReleaseCenterPluginDownloadResult(true, "插件包已缓存 1 项", "download-detail", 1, "cache"),
        PrepareResult = new ReleaseCenterPluginPrepareResult(true, "已生成 1 个预安装目录", "prepare-detail", 1, "staging")
    };

    var orchestrator = new PluginUpdateOrchestrator(releaseCenter, new NullLoggerService<PluginUpdateOrchestrator>());

    var result = await orchestrator.RunAsync(PluginUpdateTrigger.Manual);

    Assert.True(result.IsSuccess);
    Assert.True(result.RestartRequired);
    Assert.Equal(1, releaseCenter.CheckCallCount);
    Assert.Equal(1, releaseCenter.DownloadCallCount);
    Assert.Equal(1, releaseCenter.PrepareCallCount);
}
```

再新增失败路径：

- 发布中心服务为空或未配置时返回未配置结果。
- 检查失败时不调用下载。
- 下载失败时不调用预安装。
- 下载数量为 0 时不调用预安装，返回无需重启。

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter FullyQualifiedName~PluginUpdateOrchestratorTests --property:BaseOutputPath=.tmp\test-bin\
```

预期：失败，因为 `PluginUpdateOrchestrator` 尚不存在。

**步骤 3：实现最小编排服务**

在 `digital-intelligence-bridge/Services/PluginUpdateOrchestrator.cs` 新增：

- `PluginUpdateTrigger` 枚举：`Manual`、`Startup`、`SiteInitialized`。
- `PluginUpdateRunResult` 记录，包含 `IsSuccess`、`Summary`、`Detail`、`RestartRequired`、`CheckedAt`、`CheckResult`、`DownloadResult`、`PrepareResult`。
- `IPluginUpdateOrchestrator` 接口。
- `PluginUpdateOrchestrator` 实现。

核心规则：

```csharp
if (_releaseCenterService is null || !_releaseCenterService.IsConfigured)
{
    return PluginUpdateRunResult.NotConfigured(trigger, DateTime.Now);
}

var check = await _releaseCenterService.CheckForUpdatesAsync(cancellationToken);
if (!check.IsSuccess) return PluginUpdateRunResult.CheckFailed(trigger, check, DateTime.Now);

var download = await _releaseCenterService.DownloadAvailablePluginPackagesAsync(cancellationToken);
if (!download.IsSuccess) return PluginUpdateRunResult.DownloadFailed(trigger, check, download, DateTime.Now);

if (download.DownloadedCount <= 0)
{
    return PluginUpdateRunResult.NoPackages(trigger, check, download, DateTime.Now);
}

var prepare = await _releaseCenterService.PrepareCachedPluginPackagesAsync(cancellationToken);
return PluginUpdateRunResult.Prepared(trigger, check, download, prepare, DateTime.Now);
```

**步骤 4：运行测试并确认通过**

运行同一条 `dotnet test`。

预期：`PluginUpdateOrchestratorTests` 全部通过。

### 任务 2：首页手动入口接入

**文件：**
- 修改：`digital-intelligence-bridge/ViewModels/HomeDashboardViewModel.cs`
- 修改：`digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs`
- 修改：`digital-intelligence-bridge.UnitTests/HomeDashboardViewModelTests.cs`

**步骤 1：编写失败测试**

在 `HomeDashboardViewModelTests` 新增：

```csharp
[Fact]
public async Task CheckUpdatesCommand_ShouldRunPluginAutoUpdateAndShowRestart_WhenPrepared()
{
    using var sandbox = new TestConfigSandbox();
    var updateService = new StubPluginUpdateOrchestrator(
        new PluginUpdateRunResult(
            true,
            "已生成 1 个预安装目录",
            "prepare-detail",
            true,
            DateTime.Now,
            null,
            null,
            null));
    var vm = CreateVm(pluginUpdateOrchestrator: updateService);

    vm.CheckUpdatesCommand.Execute();
    await WaitUntilAsync(() => !vm.IsBusy);

    Assert.Equal(1, updateService.RunCallCount);
    Assert.Equal("需要重启", vm.PendingActionTitle);
    Assert.Contains("重启", vm.PendingActionDetail);
}
```

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter "FullyQualifiedName~HomeDashboardViewModelTests|FullyQualifiedName~PluginUpdateOrchestratorTests" --property:BaseOutputPath=.tmp\test-bin\
```

预期：失败，因为首页尚未接收编排服务。

**步骤 3：接入首页**

修改 `HomeDashboardViewModel`：

- 构造函数新增可选 `IPluginUpdateOrchestrator? pluginUpdateOrchestrator`。
- `CheckUpdatesCommand` 从 `RefreshAsync` 改为 `RunPluginUpdateAsync(PluginUpdateTrigger.Manual)`。
- 新增公开方法 `RunPluginUpdateAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default)`。
- 成功且 `RestartRequired=true` 时设置：
  - `PendingActionTitle = "需要重启"`
  - `PendingActionDetail = "插件更新已预安装，重启 DIB 后生效。"`
  - `LastPrepareStatus` 使用结果摘要和时间。
- 无更新时设置最近检查状态，不打扰。
- 失败时设置“需要排查”和详情。
- 调用完成后 `RefreshLocalState(...)`，刷新插件清单。

修改 `MainWindowViewModel`：

- 构造函数新增可选 `IPluginUpdateOrchestrator? pluginUpdateOrchestrator`。
- 创建 `HomeDashboardViewModel` 时传入该编排服务；如果未提供但有 `IReleaseCenterService`，创建 `PluginUpdateOrchestrator`。

**步骤 4：运行测试并确认通过**

运行同一条 `dotnet test`。

预期：首页相关测试和编排服务测试通过。

### 任务 3：启动后静默检查

**文件：**
- 修改：`digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs`
- 修改：`digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs`

**步骤 1：编写失败测试**

在 `MainWindowViewModelTests` 新增测试：

```csharp
[Fact]
public async Task RunStartupPluginUpdateAsync_ShouldDelegateToHomeDashboardWithStartupTrigger()
{
    var updateService = new StubPluginUpdateOrchestrator(PluginUpdateRunResult.NoPackages(PluginUpdateTrigger.Startup, null, null, DateTime.Now));
    var vm = new MainWindowViewModel(
        new NullLoggerService<MainWindowViewModel>(),
        Options.Create(new AppSettings()),
        pluginUpdateOrchestrator: updateService);

    await vm.RunStartupPluginUpdateAsync();

    Assert.Equal(1, updateService.RunCallCount);
    Assert.Equal(PluginUpdateTrigger.Startup, updateService.LastTrigger);
}
```

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter "FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~HomeDashboardViewModelTests|FullyQualifiedName~PluginUpdateOrchestratorTests" --property:BaseOutputPath=.tmp\test-bin\
```

预期：失败，因为启动更新方法尚不存在。

**步骤 3：实现启动入口**

修改 `MainWindowViewModel`：

- 新增 `public async Task RunStartupPluginUpdateAsync(CancellationToken cancellationToken = default)`。
- 方法内部延迟短时间后调用 `HomeDashboard.RunPluginUpdateAsync(PluginUpdateTrigger.Startup, cancellationToken)`。
- 捕获异常并写日志，避免阻塞主窗口。
- 构造函数内使用 `_ = RunStartupPluginUpdateAsync();` 触发后台静默检查。

**步骤 4：运行测试并确认通过**

运行同一条 `dotnet test`。

预期：全部目标测试通过。

### 任务 4：设置页复用策略

**文件：**
- 修改：`digital-intelligence-bridge/ViewModels/SettingsViewModel.cs`
- 测试：`digital-intelligence-bridge.UnitTests/SettingsViewModelReleaseCenterTests.cs`

**步骤 1：保留现有底层按钮**

设置页继续保留“检查更新”“下载插件包”“预安装插件包”，用于开发和排障。暂不强制接入自动编排，避免破坏已有测试和细粒度操作。

**步骤 2：站点初始化后保持自动预安装**

确认 `InitializeSitePluginsCommand` 已经执行检查、下载、预安装并设置重启提示。若测试失败，仅修正与新编排服务冲突的地方。

### 任务 5：验证和提交

**文件：**
- 验证：`digital-intelligence-bridge`
- 验证：`digital-intelligence-bridge.UnitTests`
- 验证：`docs/plans/2026-04-27-plugin-auto-update-design.md`
- 验证：`docs/plans/2026-04-27-plugin-auto-update.md`

**步骤 1：运行文档语言检查**

```powershell
scripts\check-doc-lang.ps1
```

预期：通过。

**步骤 2：运行目标测试**

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter "FullyQualifiedName~PluginUpdateOrchestratorTests|FullyQualifiedName~HomeDashboardViewModelTests|FullyQualifiedName~MainWindowViewModelTests" --property:BaseOutputPath=.tmp\test-bin\
```

预期：通过。

**步骤 3：运行客户端构建**

```powershell
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal --property:BaseOutputPath=.tmp\client-build\
```

预期：构建成功。若出现 `NU1900`，按网络受限警告记录，不作为业务失败。

**步骤 4：暂存相关文件**

只暂存本任务相关文件，避免混入既有无关工作区改动。

**步骤 5：提交**

```powershell
git commit -m "feat: 增加插件自动更新编排"
```
