# 客户端和插件版本显示设计

## 背景

客户端主界面左侧底部当前写死显示 `v1.0.0`，与实际发布版本不一致。插件页面打开后也没有显示插件版本，用户无法确认当前加载的是哪个插件包版本。

现有代码中已经存在两个可靠来源：

- 客户端版本：`IApplicationService.GetVersion()`。
- 插件版本：运行时插件目录 `plugin.json` 对应的 `LoadedPlugin.Manifest.Version`。

## 目标

让客户端界面显示的版本号来自运行时事实来源，不再依赖 XAML 硬编码。

## 设计

### 客户端版本

`MainWindowViewModel` 增加 `AppVersionText` 属性。属性通过 `IApplicationService.GetVersion()` 读取版本，并统一格式化为 `v<version>`。如果版本为空，则回退为 `v1.0.0`。

`MainWindow.axaml` 左侧底部文本改为绑定 `AppVersionText`。

开发默认配置 `digital-intelligence-bridge/appsettings.json` 的 `Application.Version` 调整为当前测试客户端版本 `1.0.3`，避免本地运行继续显示旧版本。

### 插件版本

`PluginHostViewModel` 增加插件元数据属性：

- `PluginName`
- `PluginId`
- `PluginVersion`
- `PluginVersionText`

打开插件页时，`MainWindowViewModel.CreatePluginHostContent` 使用 `LoadedPlugin.Manifest` 创建宿主视图模型。`PluginHostView` 在插件内容上方显示轻量标题栏：

```text
就诊登记    v1.0.3-dev.2
patient-registration
```

插件内容继续由插件自身控件承载，不改变插件业务界面。

## 错误处理

如果插件加载失败，错误页继续显示错误信息。错误页也可以显示插件名称和版本；如果缺少元数据，则使用空字符串隐藏版本文本。

## 测试策略

- `MainWindowViewModel` 单元测试验证 `AppVersionText` 来自注入的应用服务。
- `PluginHostViewModel` 单元测试验证插件版本格式化为 `v<version>`。
- 构建验证 XAML 绑定和编译通过。
