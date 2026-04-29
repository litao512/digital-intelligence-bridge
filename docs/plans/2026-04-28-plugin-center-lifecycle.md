# 插件中心独立生命周期实施计划

> **执行提示：** 实施本计划时按任务逐项执行，并在每个任务后运行对应验证。

**目标：** 将插件中心改为每个插件独立检查、下载、预安装、卸载和重启生效的生命周期管理界面。

**架构：** 发布中心服务提供插件级下载、预安装和待卸载标记；插件中心 ViewModel 合并运行时目录、staging 目录、待卸载标记和发布中心清单，给每一行生成独立操作命令。重启激活流程统一处理预安装目录和待卸载标记，避免运行时 DLL 占用问题。

**技术栈：** .NET、Avalonia、Prism `DelegateCommand`、xUnit、`System.IO.Compression`、现有 `ReleaseCenterService`。

---

### 任务 1：服务接口测试

**文件：**
- 修改：`digital-intelligence-bridge.UnitTests/ReleaseCenterDownloadTests.cs`
- 修改：`digital-intelligence-bridge.UnitTests/ReleaseCenterPrepareInstallTests.cs`
- 修改：`digital-intelligence-bridge.UnitTests/ReleaseCenterActivatePreparedTests.cs`

**步骤 1：编写失败测试**

新增测试覆盖：

- `DownloadPluginPackageAsync` 只下载指定插件，不下载 manifest 中其他插件。
- `PreparePluginPackageAsync` 只解压指定插件缓存包。
- `MarkPluginForUninstallAsync` 创建待卸载标记。
- `ActivatePreparedPluginPackagesAsync` 处理待卸载标记，删除目标运行时目录并创建备份。

**步骤 2：运行测试并确认失败**

运行：`dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter "ReleaseCenterDownloadTests|ReleaseCenterPrepareInstallTests|ReleaseCenterActivatePreparedTests"`

预期：失败，原因是接口和方法尚不存在。

### 任务 2：插件级发布中心能力

**文件：**
- 修改：`digital-intelligence-bridge/Services/ReleaseCenterService.cs`

**步骤 1：扩展记录类型和接口**

新增结果类型：

```csharp
public sealed record ReleaseCenterPluginUninstallResult(bool IsSuccess, string Summary, string Detail, string PluginId, string StagingDirectory);
```

`IReleaseCenterService` 新增：

```csharp
Task<ReleaseCenterPluginDownloadResult> DownloadPluginPackageAsync(string pluginId, string? version = null, CancellationToken cancellationToken = default);
Task<ReleaseCenterPluginPrepareResult> PreparePluginPackageAsync(string pluginId, string? version = null, CancellationToken cancellationToken = default);
Task<ReleaseCenterPluginUninstallResult> MarkPluginForUninstallAsync(string pluginId, CancellationToken cancellationToken = default);
```

**步骤 2：实现插件级下载**

复用 manifest 获取、URL 归一化、SHA256 校验和文件命名逻辑，只筛选目标 `pluginId` 与可选 `version`。

**步骤 3：实现插件级预安装**

只查找目标插件缓存 zip，解压到 staging 下的单个目录。目录命名继续使用 zip 文件名，避免和现有激活流程冲突。

**步骤 4：实现卸载标记**

在 staging 下创建目录：`uninstall-<pluginId>`，内部写入 `uninstall.json`：

```json
{
  "pluginId": "patient-registration",
  "requestedAt": "2026-04-28T00:00:00Z"
}
```

**步骤 5：扩展激活流程**

`ActivatePreparedPluginPackagesAsync` 遍历 staging 时：

- 如果目录包含 `uninstall.json`，读取 `pluginId`，备份并删除运行时插件目录，然后删除该 staging 目录。
- 否则按现有预安装逻辑复制插件。

**步骤 6：运行测试**

运行：`dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter "ReleaseCenterDownloadTests|ReleaseCenterPrepareInstallTests|ReleaseCenterActivatePreparedTests"`

预期：通过。

### 任务 3：插件更新编排拆分

**文件：**
- 修改：`digital-intelligence-bridge/Services/PluginUpdateOrchestrator.cs`
- 修改：`digital-intelligence-bridge.UnitTests/PluginUpdateOrchestratorTests.cs`

**步骤 1：编写失败测试**

覆盖：

- `CheckAsync` 只检查，不触发下载和预安装。
- `InstallOrUpdateAsync(pluginId, version)` 调用插件级下载和预安装。
- `UninstallAsync(pluginId)` 调用插件级卸载标记。

**步骤 2：实现接口**

`IPluginUpdateOrchestrator` 新增：

```csharp
Task<PluginUpdateRunResult> CheckAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default);
Task<PluginUpdateRunResult> InstallOrUpdateAsync(string pluginId, string? version = null, CancellationToken cancellationToken = default);
Task<PluginUpdateRunResult> UninstallAsync(string pluginId, CancellationToken cancellationToken = default);
```

保留 `RunAsync`，内部调用 `CheckAsync` + 批量旧流程，避免现有启动事件暂时断裂。

**步骤 3：运行测试**

运行：`dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter PluginUpdateOrchestratorTests`

预期：通过。

### 任务 4：插件中心 ViewModel

**文件：**
- 修改：`digital-intelligence-bridge/ViewModels/PluginCenterViewModel.cs`
- 修改：`digital-intelligence-bridge.UnitTests/PluginCenterViewModelTests.cs`

**步骤 1：编写失败测试**

覆盖：

- `CheckUpdatesCommand` 只刷新清单，不执行安装。
- 未安装插件行显示 `安装`。
- 可更新插件行显示 `更新`。
- 待重启生效插件行显示 `重启生效`。
- 已安装插件行显示 `卸载`。
- 执行行操作后刷新列表和状态文案。

**步骤 2：实现行操作命令**

`PluginCenterItem` 增加：

```csharp
public string ActionText { get; init; } = string.Empty;
public bool CanExecuteAction { get; init; }
public DelegateCommand? ActionCommand { get; init; }
```

`PluginCenterViewModel` 根据状态创建行命令：

- 安装/更新：调用 `InstallOrUpdateAsync(pluginId, latestVersion)`。
- 卸载：调用 `UninstallAsync(pluginId)`。
- 重启生效：调用 `RestartApplicationCommand`。
- 重试：重复上一次失败动作。

**步骤 3：读取卸载标记**

`RefreshAsync` 读取 staging 中 `uninstall.json`，并合并为 `待卸载` 状态。

**步骤 4：运行测试**

运行：`dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter PluginCenterViewModelTests`

预期：通过。

### 任务 5：插件中心 UI

**文件：**
- 修改：`digital-intelligence-bridge/Views/PluginCenterView.axaml`

**步骤 1：更新表格列**

将列表列调整为：插件名称、插件 ID、当前版本、最新版本、状态、操作、详情。

**步骤 2：绑定操作按钮**

在操作列增加按钮：

```xml
<Button Content="{Binding ActionText}"
        Command="{Binding ActionCommand}"
        IsVisible="{Binding CanExecuteAction}"/>
```

**步骤 3：保持布局稳定**

给操作列固定宽度，避免按钮文字造成表格跳动。

### 任务 6：构建和验证

**文件：**
- 无直接编辑。

**步骤 1：构建应用**

运行：`dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal`

预期：构建成功。

**步骤 2：构建测试**

运行：`dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal`

预期：构建成功。

**步骤 3：运行目标测试**

运行：`dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter "PluginCenterViewModelTests|PluginUpdateOrchestratorTests|ReleaseCenterDownloadTests|ReleaseCenterPrepareInstallTests|ReleaseCenterActivatePreparedTests"`

预期：所有目标测试通过。

**步骤 4：运行插件端到端冒烟测试**

运行：`pwsh -File .\scripts\test-plugin-upgrade-e2e-script.ps1`

预期：脚本自检通过。

**步骤 5：提交**

```bash
git add docs/plans/2026-04-28-plugin-center-lifecycle-design.md docs/plans/2026-04-28-plugin-center-lifecycle.md digital-intelligence-bridge digital-intelligence-bridge.UnitTests
git commit -m "feat: 增加插件中心独立生命周期操作"
```
