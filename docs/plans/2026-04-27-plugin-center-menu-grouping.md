# 插件中心与菜单分组实施计划

> **执行要求：** 实施时使用 `superpowers:executing-plans`，按任务逐项执行并在每项后验证。

**目标：** 新增插件中心，并把左侧导航分为工作台、插件和系统，避免插件菜单与内置菜单混排。

**架构：** 在 `MainWindowViewModel` 中扩展菜单模型，新增菜单分组视图模型供 XAML 渲染。新增 `PluginCenterViewModel` 和 `PluginCenterView`，复用现有插件状态读取与插件自动更新编排能力，第一版只提供总览、列表和操作入口。

**技术栈：** .NET、Avalonia XAML、Prism `DelegateCommand`、xUnit。

---

### 任务 1：菜单分组模型

**文件：**
- 修改：`digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs`
- 修改：`digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs`

**步骤 1：编写失败测试**

在 `MainWindowViewModelTests` 新增：

```csharp
[Fact]
public void MenuSections_ShouldSeparateWorkspaceAndPluginMenus_WhenExternalPluginMenusExist()
{
    var pluginMenu = new PluginMenuItem("patient-registration.entry", "就诊登记", "🧾", 10);
    var vm = new MainWindowViewModel(
        new NullLoggerService<MainWindowViewModel>(),
        Microsoft.Extensions.Options.Options.Create(new Configuration.AppSettings()),
        externalPluginMenus: [pluginMenu]);

    var workspace = Assert.Single(vm.MenuSections, section => section.Title == "工作台");
    Assert.Contains(workspace.Items, item => item.Title == "首页");
    Assert.Contains(workspace.Items, item => item.Title == "资源中心");
    Assert.Contains(workspace.Items, item => item.Title == "插件中心");

    var plugins = Assert.Single(vm.MenuSections, section => section.Title == "插件");
    var item = Assert.Single(plugins.Items);
    Assert.Equal("就诊登记", item.Title);
    Assert.True(item.IsExternalPlugin);
}
```

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter FullyQualifiedName~MainWindowViewModelTests --property:BaseOutputPath=.tmp\test-bin\
```

预期：失败，因为 `MenuSections` 和“插件中心”尚不存在。

**步骤 3：实现菜单分组**

在 `MainWindowViewModel.cs` 中：

- 新增 `MenuSectionKind`：`Workspace`、`Plugin`、`System`。
- `MenuItem` 增加 `SectionKind`。
- 新增 `MenuSection`，包含 `Title` 和 `ObservableCollection<MenuItem> Items`。
- `MainWindowViewModel` 新增 `ObservableCollection<MenuSection> MenuSections`。
- `InitializeMenuItems` 继续维护 `MenuItems`，同时按分组填充 `MenuSections`。
- 新增 `MainViewType.PluginCenter` 和 `TabItemType.PluginCenter`。
- 首页、资源中心、插件中心归入 `Workspace`；插件菜单归入 `Plugin`。

**步骤 4：运行测试并确认通过**

运行同一条 `dotnet test`。

预期：`MainWindowViewModelTests` 通过。

### 任务 2：插件中心 ViewModel

**文件：**
- 新增：`digital-intelligence-bridge/ViewModels/PluginCenterViewModel.cs`
- 新增：`digital-intelligence-bridge.UnitTests/PluginCenterViewModelTests.cs`

**步骤 1：编写失败测试**

新增测试：

```csharp
[Fact]
public async Task RefreshAsync_ShouldListRuntimeAndStagingPlugins()
{
    using var sandbox = new TestConfigSandbox();
    CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.2");
    CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "staging", "patient-registration-1.0.3"), "patient-registration", "就诊登记", "1.0.3");

    var vm = new PluginCenterViewModel(
        new NullLoggerService<PluginCenterViewModel>(),
        Options.Create(CreateSettings(sandbox)),
        null);

    await vm.RefreshAsync();

    var item = Assert.Single(vm.PluginItems);
    Assert.Equal("就诊登记", item.Name);
    Assert.Equal("1.0.3", item.Version);
    Assert.Equal("待重启生效", item.Status);
}
```

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter FullyQualifiedName~PluginCenterViewModelTests --property:BaseOutputPath=.tmp\test-bin\
```

预期：失败，因为插件中心 ViewModel 尚不存在。

**步骤 3：实现 ViewModel**

新增 `PluginCenterViewModel`：

- 属性：`InstalledCountText`、`PendingRestartCountText`、`FailedCountText`、`LastUpdateStatus`、`LastPrepareStatus`。
- 集合：`ObservableCollection<PluginCenterItem> PluginItems`。
- 命令：`RefreshCommand`、`CheckUpdatesCommand`、`RestartApplicationCommand`。
- 读取运行时插件目录和预安装目录中的 `plugin.json`。
- 合并同一插件 ID，预安装版本优先显示为“待重启生效”。
- `CheckUpdatesCommand` 调用 `IPluginUpdateOrchestrator.RunAsync(PluginUpdateTrigger.Manual)`，然后刷新列表。

**步骤 4：运行测试并确认通过**

运行同一条 `dotnet test`。

预期：`PluginCenterViewModelTests` 通过。

### 任务 3：插件中心页面和导航

**文件：**
- 新增：`digital-intelligence-bridge/Views/PluginCenterView.axaml`
- 新增：`digital-intelligence-bridge/Views/PluginCenterView.axaml.cs`
- 修改：`digital-intelligence-bridge/Views/MainWindow.axaml`
- 修改：`digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs`
- 修改：`digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs`

**步骤 1：编写失败测试**

在 `MainWindowViewModelTests` 新增：

```csharp
[Fact]
public void NavigateCommand_ShouldOpenPluginCenterTab_WhenNavigateToPluginCenter()
{
    var vm = CreateVm();

    vm.NavigateCommand.Execute(MainViewType.PluginCenter);

    Assert.Equal(MainViewType.PluginCenter, vm.CurrentView);
    Assert.NotNull(vm.SelectedTab);
    Assert.Equal(TabItemType.PluginCenter, vm.SelectedTab!.TabType);
}
```

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter FullyQualifiedName~MainWindowViewModelTests --property:BaseOutputPath=.tmp\test-bin\
```

预期：失败，因为插件中心导航未实现。

**步骤 3：实现导航和页面**

- `MainWindowViewModel` 新增 `PluginCenter` 属性。
- `NavigateTo` 支持 `MainViewType.PluginCenter`。
- `GetDefaultIcon` 为插件中心返回图标。
- `MainWindow.axaml` 增加 `PluginCenterView` 的标签内容区域。
- 新增 `PluginCenterView.axaml`，布局保持资源中心风格：顶部摘要、操作按钮、插件表格。

**步骤 4：运行测试并确认通过**

运行同一条 `dotnet test`。

预期：`MainWindowViewModelTests` 通过。

### 任务 4：左侧菜单分组 UI

**文件：**
- 修改：`digital-intelligence-bridge/Views/MainWindow.axaml`
- 修改：`digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs`

**步骤 1：编写失败测试**

新增 XAML 结构测试：

```csharp
[Fact]
public void MainWindowAxaml_ShouldBindSidebarToMenuSections()
{
    var fullPath = FindRepositoryFile("digital-intelligence-bridge", "Views", "MainWindow.axaml");
    var xaml = File.ReadAllText(fullPath);

    Assert.Contains("ItemsSource=\"{Binding MenuSections}\"", xaml);
    Assert.Contains("ItemsSource=\"{Binding Items}\"", xaml);
}
```

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter FullyQualifiedName~MainWindowViewModelTests --property:BaseOutputPath=.tmp\test-bin\
```

预期：失败，因为侧边栏仍绑定 `MenuItems`。

**步骤 3：修改 XAML**

将侧边栏菜单从单层 `ItemsControl ItemsSource="{Binding MenuItems}"` 改为分组结构：

- 外层绑定 `MenuSections`。
- 分组标题绑定 `Title`。
- 内层绑定 `Items`。
- 菜单项按钮继续复用现有 `NavigateCommand` 和样式。

**步骤 4：运行测试并确认通过**

运行同一条 `dotnet test`。

预期：XAML 结构测试通过。

### 任务 5：验证和提交

**文件：**
- 验证：`digital-intelligence-bridge`
- 验证：`digital-intelligence-bridge.UnitTests`
- 验证：`docs/plans/2026-04-27-plugin-center-menu-grouping-design.md`
- 验证：`docs/plans/2026-04-27-plugin-center-menu-grouping.md`

**步骤 1：运行文档语言检查**

```powershell
scripts\check-doc-lang.ps1
```

预期：通过。

**步骤 2：运行目标测试**

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal --filter "FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~PluginCenterViewModelTests" --property:BaseOutputPath=.tmp\test-bin\
```

预期：通过。

**步骤 3：运行客户端构建**

```powershell
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal --property:BaseOutputPath=.tmp\client-build\
```

预期：构建成功。若出现 `NU1900`，按网络受限警告记录，不作为业务失败。

**步骤 4：暂存相关文件**

只暂存插件中心和菜单分组相关文件，避免混入既有无关工作区改动。

**步骤 5：提交**

```powershell
git commit -m "feat: 增加插件中心和菜单分组"
```
