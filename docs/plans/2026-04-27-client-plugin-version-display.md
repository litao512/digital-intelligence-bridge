# 客户端和插件版本显示实施计划

> **执行要求：** 实施时使用 `superpowers:executing-plans`，按任务逐项执行并在每项后验证。

**目标：** 主界面显示真实客户端版本，插件宿主页显示当前加载插件版本。

**架构：** 客户端版本通过 `IApplicationService.GetVersion()` 注入到 `MainWindowViewModel`，XAML 只绑定格式化文本。插件版本通过 `LoadedPlugin.Manifest` 传入 `PluginHostViewModel`，`PluginHostView` 负责展示插件名称、编码和版本。

**技术栈：** .NET 10、Avalonia、Prism、xUnit。

---

### 任务 1：主界面版本测试

**文件：**
- 修改：`digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs`

**步骤 1：编写失败测试**

新增测试，构造 `MainWindowViewModel` 时注入返回 `1.0.3` 的 `IApplicationService`，断言：

```csharp
Assert.Equal("v1.0.3", viewModel.AppVersionText);
```

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter MainWindowViewModel
```

预期：失败，因为 `AppVersionText` 尚不存在。

### 任务 2：插件宿主版本测试

**文件：**
- 修改：`digital-intelligence-bridge.UnitTests/PluginHostViewModelTests.cs`

**步骤 1：编写失败测试**

新增或扩展测试，构造：

```csharp
new PluginHostViewModel(content, pluginName: "就诊登记", pluginId: "patient-registration", pluginVersion: "1.0.3-dev.2")
```

断言：

```csharp
Assert.Equal("就诊登记", vm.PluginName);
Assert.Equal("patient-registration", vm.PluginId);
Assert.Equal("v1.0.3-dev.2", vm.PluginVersionText);
```

**步骤 2：运行测试并确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter PluginHostViewModel
```

预期：失败，因为构造函数和属性尚不存在。

### 任务 3：实现 ViewModel

**文件：**
- 修改：`digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs`
- 修改：`digital-intelligence-bridge/ViewModels/PluginHostViewModel.cs`

**步骤 1：实现最小属性**

`MainWindowViewModel` 增加：

```csharp
public string AppVersionText => FormatVersion(_applicationService.GetVersion());
```

`PluginHostViewModel` 增加插件元数据属性和版本格式化。

**步骤 2：通过定向测试**

重新运行定向测试，预期通过。

### 任务 4：修改 XAML

**文件：**
- 修改：`digital-intelligence-bridge/Views/MainWindow.axaml`
- 修改：`digital-intelligence-bridge/Views/PluginHostView.axaml`
- 修改：`digital-intelligence-bridge/appsettings.json`

**步骤 1：绑定主界面版本**

把 `Text="v1.0.0"` 改为：

```xml
Text="{Binding AppVersionText}"
```

**步骤 2：增加插件标题栏**

`PluginHostView.axaml` 在 `ContentControl` 上方增加插件名称、版本和编码。

**步骤 3：更新默认应用版本**

把 `Application.Version` 从 `1.0.0` 改为 `1.0.3`。

### 任务 5：验证

**文件：**
- 验证：客户端项目和单元测试。

**步骤 1：运行单元测试**

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -m:1 -v minimal
```

**步骤 2：运行客户端构建**

```powershell
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
```

**步骤 3：运行文档语言检查**

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\check-doc-lang.ps1
```

### 任务 6：提交

**文件：**
- 只提交任务相关文件。

**步骤 1：暂存相关文件**

```powershell
git add digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs digital-intelligence-bridge/ViewModels/PluginHostViewModel.cs digital-intelligence-bridge/Views/MainWindow.axaml digital-intelligence-bridge/Views/PluginHostView.axaml digital-intelligence-bridge/appsettings.json digital-intelligence-bridge.UnitTests/MainWindowViewModelTests.cs digital-intelligence-bridge.UnitTests/PluginHostViewModelTests.cs docs/plans/2026-04-27-client-plugin-version-display-design.md docs/plans/2026-04-27-client-plugin-version-display.md
```

**步骤 2：提交**

```powershell
git commit -m "feat: 显示客户端和插件版本"
```
