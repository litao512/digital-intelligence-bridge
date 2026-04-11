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
- `%LOCALAPPDATA%/UniversalTrayTool/appsettings.json`：本机用户状态配置
- 配置架构说明：`docs/05-operations/CONFIGURATION_ARCHITECTURE_SPEC.md`
- 运行时目录说明：`docs/05-operations/RUNTIME_DIRECTORY_GUIDE.md`
- 配置安全说明：`docs/05-operations/DIB_CONFIG_SAFETY_GUIDE.md`
- 插件打包规范：`docs/05-operations/PLUGIN_PACKAGING_GUIDE.md`
- 新电脑安装指南：`docs/05-operations/NEW_MACHINE_SETUP_GUIDE.md`

建议：

1. 仓库中的 `appsettings.json` 只保留默认模板值，不写入真实密钥。
2. 实际分发时，把目标环境的 `ReleaseCenter` 默认配置写入安装目录 `appsettings.json`。
3. 应用首次启动会把安装目录默认配置复制到 `%LOCALAPPDATA%/UniversalTrayTool/appsettings.json`。
4. 若用户配置已存在但 `ReleaseCenter` 关键字段缺失，程序会从安装目录默认配置补齐。

同步 GitHub Actions Secrets（从本机用户配置读取）：

```powershell
./scripts/sync-github-secrets.ps1
```

## 医保药品导入同步工具

当前项目已内置“医保药品导入同步工具”页面，用于执行固定模板 Excel 的预检、PostgreSQL 导入和 SQL Server 同步。

### 模板要求

当前版本按固定模板处理，要求 Excel 至少包含以下工作表：

- `总表（270419）`
- `新增（559）`
- `变更（449）`
- `关联关系表`

导入器会校验表头是否匹配固定列名，并按流式方式读取，避免在 4 GB 内存机器上整表加载。

### SQL Server 配置

工具页依赖 `MedicalDrugImport.SqlServer` 配置，支持以下来源：

1. `appsettings.json`
2. 用户配置文件
3. 环境变量

当前同时兼容以下环境变量：

- `MSSQL_DB_SERVER`
- `MSSQL_DB_PORT`
- `MSSQL_DB_NAME`
- `MSSQL_DB_USER`
- `MSSQL_DB_PASSWORD`
- `MSSQL_DB_ENCRYPT`
- `MSSQL_DB_TRUST_SERVER_CERTIFICATE`

示例配置：

```json
"MedicalDrugImport": {
  "Enabled": true,
  "PostgresSchema": "etl",
  "SqlServer": {
    "Host": "101.42.19.26",
    "Port": 22433,
    "Database": "ChisDict",
    "Username": "pluginUser",
    "Password": "******",
    "Encrypt": true,
    "TrustServerCertificate": true
  }
}
```

### 使用流程

1. 在左侧导航打开“医保药品导入”页面。
2. 输入或粘贴 Excel 文件路径。
3. 点击“预检”，确认工作表和表头都通过。
4. 点击“导入入库”，生成批次并写入 PostgreSQL 中间表。
5. 点击“同步 SQL Server”，将本批次增量同步到 `dbo.yb_药品目录`。
6. 如果同步失败，可直接点击“重试同步”，不会重新解析 Excel。

### 当前实现范围

当前版本已经实现：

- 固定模板预检
- 导入批次状态汇总
- PostgreSQL raw / clean / error / merge 流水线骨架
- SQL Server upsert 与同步记录写入骨架
- 最近批次上下文与重试同步入口

当前版本尚未实现：

- 文件选择对话框
- Excel 全字段映射与完整清洗规则
- `biz.drug_change_log` / `biz.drug_code_relation` 的完整写入
- 批次历史列表
- 后台定时同步
- outbox 消费模式

## 外部插件样例

当前仓库已新增第一个外部业务插件：`plugins-src/MedicalDrugImport.Plugin`。

运行时目录约定：

- 插件根目录：`plugins/`
- 当前样例插件目录：`plugins/MedicalDrugImport/`
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
- 插件可独立读取 `plugin.settings.json` 和 `MEDICAL_DRUG_IMPORT__*` 环境变量
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
2. 将 `plugins-src/MedicalDrugImport.Plugin/bin/Debug/net10.0/` 下的文件复制到 `plugins/MedicalDrugImport/`
3. 保留 `plugin.json` 和 `plugin.settings.json`，主程序启动时会从 `plugins/` 目录发现该插件
4. 生产环境优先通过环境变量覆盖连接串，例如：
   - `MEDICAL_DRUG_IMPORT__POSTGRES__CONNECTIONSTRING`
   - `MEDICAL_DRUG_IMPORT__SQLSERVER__CONNECTIONSTRING`
   - `MEDICAL_DRUG_IMPORT__IMPORT__BATCHSIZE`
5. 若必须执行大于 `Import.MaxSyncRowsPerRun` 的真实同步，应仅在明确窗口期内临时开启 `MEDICAL_DRUG_IMPORT__IMPORT__ALLOWUNSAFEFULLSYNC=true`
6. 若必须执行真实 SQL Server 写入，还需显式开启 `MEDICAL_DRUG_IMPORT__SQLSERVER__ENABLEWRITES=true`

## 测试

### 标准单元测试（推荐）

```bash
dotnet restore digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug
```

当前覆盖范围聚焦 `MainWindowViewModel` 的核心行为（导航、Tab、筛选、空状态）。

医保药品导入工具当前新增覆盖：

- 配置绑定与环境变量兼容
- Excel 固定模板预检
- 导入模型与流水线编排
- PostgreSQL 导入仓储 SQL 生成
- SQL Server 同步 SQL 生成
- 工具页 ViewModel 与导航接入

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
