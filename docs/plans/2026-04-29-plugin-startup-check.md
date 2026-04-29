# 插件启动检查实施计划

> **执行提示：** 实施本计划时按任务逐项执行，并在每个任务后运行对应验证。

**目标：** 将客户端启动插件流程改为只检查更新，并通过插件中心和托盘提示告知用户。

**架构：** `MainWindowViewModel.RunStartupPluginUpdateAsync` 从完整更新编排改为检查编排。启动检查结果同步给 `PluginCenterViewModel`，由插件中心现有合并逻辑显示安装/更新状态；托盘仅作为非打扰提示。

**技术栈：** .NET 10、Avalonia、Prism、xUnit。

---

### 任务 1：启动检查行为测试

**文件：**
- 修改：`digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs`

**步骤 1：编写失败测试**

新增测试：`RunStartupPluginUpdateAsync_ShouldOnlyCheckPluginsAndExposeAvailablePlugins`

验证：
- 调用 `RunStartupPluginUpdateAsync(TimeSpan.Zero)` 后，`StubPluginUpdateOrchestrator.CheckCallCount == 1`。
- `RunCallCount == 0`。
- 插件中心刷新后能显示授权插件为可安装。
- 托盘收到一次更新提示。

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~MainWindowViewModelTests.RunStartupPluginUpdateAsync_ShouldOnlyCheckPluginsAndExposeAvailablePlugins" -v minimal
```

预期：失败，当前实现仍调用 `RunAsync(Startup)`。

### 任务 2：启动检查实现

**文件：**
- 修改：`digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs`
- 修改：`digital-intelligence-bridge/ViewModels/PluginCenterViewModel.cs`

**步骤 1：增加插件中心同步接口**

在 `PluginCenterViewModel` 增加：

```csharp
public async Task ApplyAvailablePluginsAsync(string detail, CancellationToken cancellationToken = default)
{
    _availablePlugins = ParseAvailablePlugins(detail);
    await RefreshAsync(cancellationToken);
}
```

**步骤 2：调整启动流程**

将 `RunStartupPluginUpdateAsync` 改为：

- 等待首页空闲。
- 获取编排器。
- 调用 `CheckAsync(PluginUpdateTrigger.Startup)`。
- 若 `CheckResult` 存在，则同步授权插件详情到插件中心。
- 若存在插件条目，更新托盘提示并发出托盘通知。
- 不调用 `RunAsync`。

**步骤 3：运行测试并确认通过**

运行同一条筛选测试。预期：通过。

### 任务 3：回归验证

**文件：**
- 仅测试。

**步骤 1：运行聚焦测试**

```powershell
dotnet test digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~PluginCenterViewModelTests" -v minimal
```

预期：通过。

**步骤 2：运行标准验证**

```powershell
dotnet build digital-intelligence-bridge\digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
dotnet build digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal
dotnet test digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal
```

预期：构建成功且所有测试通过。
