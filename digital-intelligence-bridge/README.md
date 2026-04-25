# Avalonia 入门示例 - 给 WinForm 开发者的指南

这是一个专为 WinForm 开发者设计的 Avalonia UI 入门项目。

## 项目概述

本项目实现了一个**待办事项管理器**，展示了 Avalonia 的核心概念：
- **MVVM 模式** - 替代 WinForm 的代码后置模式
- **XAML 布局** - 替代 WinForm 的拖拽设计器
- **数据绑定** - 替代手动控件赋值
- **命令 (Commands)** - 替代事件处理程序

## 项目结构

```
DigitalIntelligenceBridge/
├── Models/
│   └── TodoItem.cs              # 数据模型（类似 WinForm 的实体类）
├── ViewModels/
│   ├── ViewModelBase.cs         # 视图模型基类
│   └── MainWindowViewModel.cs   # 主窗口视图模型（业务逻辑）
├── Views/
│   ├── MainWindow.axaml         # 主窗口 XAML（UI 定义）
│   └── MainWindow.axaml.cs      # 主窗口代码后置（极少代码）
├── Converters/
│   └── PriorityToBrushConverter.cs  # 值转换器
├── App.axaml                    # 应用级资源和样式
└── Program.cs                   # 程序入口
```

## WinForm vs Avalonia 对比

| 概念 | WinForm | Avalonia |
|------|---------|----------|
| **UI 设计** | 拖拽式设计器 | XAML 声明式布局 |
| **业务逻辑位置** | 代码后置文件 (*.cs) | ViewModel 类 |
| **数据绑定** | 手动赋值/BindingSource | 声明式绑定 `{Binding 属性名}` |
| **事件处理** | 事件处理程序 (button1_Click) | 命令 (ICommand) |
| **属性变更通知** | INotifyPropertyChanged 手动实现 | Source Generator 自动生成 |
| **集合绑定** | BindingList/BindingSource | ObservableCollection |
| **控件命名** | 通过 Name 属性 | 通过 x:Name 属性 |
| **样式/主题** | 控件属性设置 | Style 系统和资源字典 |

## 核心概念详解

### 1. MVVM 模式

**WinForm 方式（代码后置）：**
```csharp
public partial class MainForm : Form
{
    private List<TodoItem> items = new();

    private void btnAdd_Click(object sender, EventArgs e)
    {
        items.Add(new TodoItem { Title = txtTitle.Text });
        listBox.DataSource = null;
        listBox.DataSource = items;
    }
}
```

**Avalonia 方式（MVVM）：**
```csharp
// ViewModel 处理业务逻辑
public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<TodoItem> _todoItems = new();

    [RelayCommand]
    private void AddTodo()
    {
        TodoItems.Add(new TodoItem { Title = NewTodoTitle });
    }
}

// XAML 绑定到 ViewModel
// <Button Content="添加" Command="{Binding AddTodoCommand}" />
```

### 2. 数据绑定

**WinForm：**
```csharp
textBox1.Text = item.Title;
item.Title = textBox1.Text;  // 需要手动同步
```

**Avalonia：**
```xml
<TextBox Text="{Binding NewTodoTitle}" />
```
- 双向绑定，自动同步
- 输入框的值自动更新到 ViewModel 属性
- ViewModel 属性变化自动更新 UI

### 3. 属性变更通知

**WinForm（手动实现）：**
```csharp
public class TodoItem : INotifyPropertyChanged
{
    private string _title;
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }
}
```

**Avalonia（自动生成）：**
```csharp
public partial class TodoItem : ObservableObject
{
    [ObservableProperty]  // Source Generator 自动生成属性
    private string _title;
}
```

### 4. 命令 (Commands)

**WinForm（事件）：**
```csharp
private void deleteButton_Click(object sender, EventArgs e)
{
    var item = (TodoItem)listBox.SelectedItem;
    items.Remove(item);
}
```

**Avalonia：**
```csharp
[RelayCommand]
private void DeleteTodo(TodoItem item)
{
    TodoItems.Remove(item);
}
```
```xml
<Button Content="删除" Command="{Binding DeleteTodoCommand}"
        CommandParameter="{Binding}" />
```

### 5. 布局系统

**WinForm：**
- 使用 Anchor/Dock 属性
- 通过设计器拖拽定位
- 流式布局/表格布局面板

**Avalonia：**
```xml
<Grid RowDefinitions="Auto,*,Auto" ColumnDefinitions="200,*">
    <!-- 第一行，第一列 -->
    <StackPanel Grid.Row="0" Grid.Column="0">
        <TextBlock Text="标题" />
        <TextBox Text="{Binding Title}" />
    </StackPanel>

    <!-- 第二行，跨两列 -->
    <ListBox Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="2" />
</Grid>
```

## 常用控件对照表

| WinForm | Avalonia | 说明 |
|---------|----------|------|
| Form | Window | 窗口 |
| Button | Button | 按钮 |
| TextBox | TextBox | 文本框 |
| CheckBox | CheckBox | 复选框 |
| ComboBox | ComboBox | 下拉框 |
| ListBox | ListBox | 列表框 |
| DataGridView | DataGrid | 数据表格 |
| Label | TextBlock | 文本显示（只读） |
| Panel | Panel/StackPanel/Grid | 容器面板 |
| GroupBox | Border + Header | 带边框的组 |

## 运行项目

```bash
cd DigitalIntelligenceBridge
dotnet run
```

## 配置分层

- 安装目录 `appsettings.json`：部署默认配置
- `%LOCALAPPDATA%/DibClient/appsettings.json`：本机用户状态配置
- 配置架构说明：`docs/05-operations/CONFIGURATION_ARCHITECTURE_SPEC.md`
- 双配置分工说明：`docs/05-operations/CONFIG_SPLIT_GUIDE.md`
- 运行时目录说明：`docs/05-operations/RUNTIME_DIRECTORY_GUIDE.md`
- 配置安全说明：`docs/05-operations/DIB_CONFIG_SAFETY_GUIDE.md`
- 插件打包规范：`docs/05-operations/PLUGIN_PACKAGING_GUIDE.md`
- 新电脑安装指南：`docs/05-operations/NEW_MACHINE_SETUP_GUIDE.md`

建议：

1. 仓库中的 `appsettings.json` 只保留默认模板值，不写入真实密钥。
2. 实际分发时，把目标环境的 `ReleaseCenter` 默认配置写入安装目录 `appsettings.json`。
3. 应用首次启动会在 `%LOCALAPPDATA%/DibClient/appsettings.json` 创建“用户白名单配置”，不再复制安装目录整份配置。
4. 用户配置仅保存用户可变项，部署默认值应通过安装目录 `appsettings.json` 统一下发。
5. 主程序配置只负责宿主部署控制和本机状态，不再承载插件业务资源连接。

同步 GitHub Actions Secrets（从本机用户配置读取）：

```powershell
./scripts/sync-github-secrets.ps1
```

## 医保药品导入能力

“医保药品导入”能力已从主程序内置功能收敛为外部插件，不再通过主程序页面、主程序配置或宿主环境变量提供正式运行入口。

当前正式路径为：

1. 运行时插件目录发现 `MedicalDrugImport` 插件
2. 插件声明资源需求
3. 资源中心授权后，由宿主运行时下发资源
4. 插件独立执行固定模板 Excel 预检、PostgreSQL 导入和 SQL Server 同步

主程序当前只负责：

- 插件发现与加载
- 资源中心与授权资源缓存
- 插件页面承载
- 本机站点状态与宿主部署控制

## 外部插件样例

当前仓库已新增第一个外部业务插件：`plugins-src/MedicalDrugImport.Plugin`。

运行时目录约定：

- 插件源码目录：`plugins-src/<PluginName>.Plugin/`
- 本地产包中转目录：仓库根 `plugins/<PluginName>/`，该目录由发布脚本生成并被 `.gitignore` 忽略
- 正式运行目录：`%LOCALAPPDATA%/DibClient/plugins/<PluginName>/`
- 目录内至少包含：
  - `plugin.json`
  - `plugin.settings.json`
  - `MedicalDrugImport.Plugin.dll`
  - `MedicalDrugImport.Plugin.deps.json`
  - 插件运行所需依赖文件

当前插件第一阶段已具备这些能力：

- 外部 DLL 能被宿主发现
- 菜单项能从清单和插件入口生成
- 页面能通过统一插件宿主承载显示
- 插件正式资源由宿主按授权下发，开发模式下可显式读取 `plugin.development.json`
- 插件内已包含 Excel 预检、PostgreSQL 导入和 SQL Server 同步骨架
- 插件侧 `Excel -> PostgreSQL` 已完成真实联调，最近一次验证批次为 `3a9f877e-45b8-4426-b352-4f5507554887`
- 插件导入链路已改为“结构预检 + 单遍导入”，避免在 50 多 MB Excel 上先全量统计再重复扫描
- 插件侧 SQL Server 同步默认启用安全护栏：
  - `SqlServer.EnableWrites = false`
  - `Import.BatchSize = 50`
  - `Import.MaxSyncRowsPerRun = 50`
  - `Import.AllowUnsafeFullSync = false`
- 当待同步记录超过 `MaxSyncRowsPerRun` 时，插件会直接拒绝真实同步，避免默认对线上库发起大批量写入
- 插件页已支持“同步预检”，当前实现会直接执行数据库 `count(*)` 统计待同步条数，而不是把整批记录先拉回插件进程
- 即使在阈值内，只要 `SqlServer.EnableWrites = false`，插件也只允许做只读预检，不允许真实写入
- 插件页会直接显示当前写入模式、每批大小和单次安全阈值，便于联调前人工确认
- 当处于只读模式时，插件页的“同步 SQL Server”按钮会直接禁用，避免误点
- “重试同步”按钮也会遵循同一条只读保护，避免通过重试入口绕过写入开关

插件失败时的当前行为：

- 入口程序集缺失：插件被记录为加载失败，不进入可用菜单
- `minHostVersion` 不兼容：宿主拒绝加载该插件
- 页面创建异常：当前标签页显示错误占位，但不影响首页、待办或其他插件

手工发布步骤：

1. 构建插件项目：
   `dotnet build plugins-src/MedicalDrugImport.Plugin/MedicalDrugImport.Plugin.csproj -c Debug`
2. 将 `plugins-src/MedicalDrugImport.Plugin/bin/Debug/net10.0/` 下的文件复制到 `%LOCALAPPDATA%/DibClient/plugins/MedicalDrugImport/`
3. 保留 `plugin.json` 和 `plugin.settings.json`，主程序启动时会从运行时插件目录发现该插件
4. 若需本地开发联调，显式开启 `DevelopmentMode.Enabled = true`，并在插件目录下创建 `plugin.development.json`
5. 正式生产资源应由资源中心授权下发，不再通过主程序配置或插件环境变量覆盖连接串

## 测试

### 标准单元测试（推荐）

本地构建与验证建议：

1. 先 `build`，再 `test`，不要并行执行
2. Avalonia 项目建议使用 `-m:1`
3. 详细排障说明见：`docs/05-operations/AVALONIA_BUILD_TROUBLESHOOTING.md`

```bash
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal
```

当前覆盖范围聚焦 `MainWindowViewModel` 的核心行为（导航、Tab、筛选、空状态）。

当前与插件、资源中心相关的新增覆盖：

- 配置分层与开发模式
- 宿主授权资源缓存
- 资源中心发现、申请与授权同步
- 插件运行时资源读取
- Excel 固定模板预检
- PostgreSQL 导入仓储 SQL 生成
- SQL Server 同步 SQL 生成
- 插件页面承载与导航接入

### 轻量回归程序（过渡方案）

```bash
dotnet build digital-intelligence-bridge.Tests/digital-intelligence-bridge.Tests.csproj -c Debug
dotnet digital-intelligence-bridge.Tests/bin/Debug/net10.0/digital-intelligence-bridge.Tests.dll
```

说明：`digital-intelligence-bridge.Tests` 为历史过渡方案，建议逐步迁移到 `digital-intelligence-bridge.UnitTests`。

## 学习建议

1. **从简单的绑定开始** - 先理解 `{Binding 属性名}` 的工作原理
2. **熟悉 ObservableCollection** - 这是 MVVM 中集合绑定的核心
3. **掌握常用布局** - Grid（表格）、StackPanel（堆叠）、DockPanel（停靠）
4. **理解命令模式** - 用 Command 替代传统的事件处理
5. **使用设计时数据** - `d:DataContext` 可以在 IDE 中预览 UI

## 进阶学习资源

- [Avalonia 官方文档](https://docs.avaloniaui.net/)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- [Avalonia UI Gallery](https://github.com/AvaloniaUI/ControlCatalogAvalonia)

## 本示例演示的功能

- ✅ 添加/删除待办事项
- ✅ 标记完成状态
- ✅ 优先级设置（不同颜色显示）
- ✅ 实时统计面板
- ✅ 数据绑定和属性变更通知
- ✅ 命令模式处理用户交互
- ✅ 自定义样式

