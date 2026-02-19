# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 提供在此代码库中工作的指导。

## 项目概述

这是一个面向 WinForms 开发者的 Avalonia UI 入门教程项目 - 一个待办事项管理应用，演示了 MVVM 模式、数据绑定和 XAML 布局。使用 Semi.Avalonia 主题和 CommunityToolkit.Mvvm 提供 MVVM 支持。

## 构建和运行命令

```bash
# 进入项目目录
cd AvaloniaDemo

# 构建项目
dotnet build

# 运行应用
dotnet run

# Release 模式运行
dotnet run --configuration Release

# 开发热重载
dotnet watch run --hot-reload
```

## 架构概述

### MVVM 模式
项目遵循严格的 MVVM 架构：
- **Models**：数据实体（如 `TodoItem.cs`）- 实现 `ObservableObject` 并使用 `[ObservableProperty]` 特性自动生成属性变更通知
- **ViewModels**：业务逻辑和状态（如 `MainWindowViewModel.cs`）- 继承自 `ViewModelBase`，而 `ViewModelBase` 继承自 `ObservableObject`
- **Views**：XAML UI 定义（如 `MainWindow.axaml`）- 代码后置文件包含最少逻辑

### ViewLocator 模式
`ViewLocator.cs` 实现 `IDataTemplate` 接口，通过命名约定自动映射 ViewModel 到 View：
- ViewModel: `MainWindowViewModel` → View: `MainWindow`
- 使用反射定位对应的视图类型
- 在 `App.axaml` 中注册为应用级数据模板

### 核心框架
- **Avalonia UI 11.3.12**：跨平台 UI 框架
- **CommunityToolkit.Mvvm 8.2.1**：MVVM 样板代码源生成器（`[ObservableProperty]`、`[RelayCommand]`）
- **Semi.Avalonia 11.3.7**：现代 UI 主题（在 `App.axaml` 中配置）

### 数据绑定约定
- 标记 `[ObservableProperty]` 的特性会自动生成可观察属性
- 命令使用 `[RelayCommand]` 特性 - 方法名如 `AddTodo` 会生成 `AddTodoCommand`
- 集合使用 `ObservableCollection<T>` 实现自动 UI 更新
- 在视图上设置 `x:DataType` 用于编译绑定（在 `.csproj` 中默认启用）

### 系统托盘实现
应用支持最小化到托盘：
- 在 `App.axaml` 中通过 `<TrayIcon.Icons>` 配置
- 在 `App.axaml.cs` 中通过 `ShutdownMode.OnExplicitShutdown` 实现逻辑
- 拦截窗口关闭事件使其隐藏而非退出
- 退出方式：托盘图标"退出"菜单或显式调用 `ExitApplication()` 方法

## 重要实现细节

- **验证**：在 `App.axaml.cs` 中禁用 Avalonia 的 DataAnnotations 验证，以避免与 CommunityToolkit 验证冲突
- **主题**：使用 `SemiTheme`（默认浅色模式），在 `App.axaml` 样式中注册
- **字体**：在 `Program.cs` 中通过 `.WithInterFont()` 配置 Inter 字体
- **编译绑定**：在项目文件中默认启用；需要在视图上设置 `x:DataType`
