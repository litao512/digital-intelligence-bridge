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

## 运行时配置（Supabase）

- 模板文件：`digital-intelligence-bridge/appsettings.runtime.template.json`
- 本机运行时配置路径：`%LOCALAPPDATA%/UniversalTrayTool/appsettings.runtime.json`

生成运行时配置（避免把密钥写进仓库）：

```powershell
./scripts/new-runtime-config.ps1 `
  -SupabaseUrl "http://your-supabase-host:8000" `
  -SupabaseAnonKey "<anon-key>" `
  -Schema "dib"
```

验证运行时配置可访问 Supabase REST：

```powershell
./scripts/verify-supabase-runtime.ps1
```

同步 GitHub Actions Secrets（从本机用户配置读取）：

```powershell
./scripts/sync-github-secrets.ps1
```

## 测试

### 标准单元测试（推荐）

```bash
dotnet restore digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug
```

当前覆盖范围聚焦 `MainWindowViewModel` 的核心行为（导航、Tab、筛选、空状态）。

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
