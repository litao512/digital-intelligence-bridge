# Universal Tray Tool (UTT) - 通用工具箱
## 产品需求文档 (Product Requirements Document)

**版本**: v1.0.0
**日期**: 2026-02-19
**状态**: 开发中

---

## 1. 产品概述

### 1.1 产品简介
Universal Tray Tool (UTT) 是一款基于 Avalonia UI 和 Prism 框架开发的跨平台桌面工具箱应用。它采用插件化架构设计，支持系统托盘常驻、多标签页管理，用户可以根据需要灵活扩展功能模块。

### 1.2 核心价值主张
- **模块化扩展**: 通过插件机制动态加载功能模块
- **系统级集成**: 支持系统托盘常驻，快速访问常用功能
- **统一管理平台**: 在一个应用中管理多种工具和功能
- **跨平台支持**: 基于 Avalonia UI，支持 Windows、macOS、Linux

### 1.3 目标用户
| 用户类型 | 描述 | 需求 |
|---------|------|------|
| 开发者 | 需要多种开发辅助工具 | 可扩展的插件系统、快捷命令执行 |
| 办公人员 | 日常任务管理和效率工具 | 待办事项、日程安排、快速启动 |
| 系统管理员 | 服务器监控和工具集合 | 系统监控、远程连接、日志查看 |
| 普通用户 | 简洁易用的工具集合 | 直观的界面、即用即走 |

---

## 2. 功能需求

### 2.1 核心功能

#### 2.1.1 系统托盘集成
- **功能描述**: 应用最小化到系统托盘，支持托盘图标点击唤醒
- **详细需求**:
  - 左键点击托盘图标: 切换主窗口显示/隐藏
  - 右键菜单: 显示功能快捷入口和退出选项
  - 支持自定义托盘图标
  - 支持托盘通知气泡

#### 2.1.2 多标签页管理
- **功能描述**: 类似浏览器的多标签页界面，支持并行操作多个功能模块
- **详细需求**:
  - 支持打开/关闭/切换标签页
  - 标签页支持拖拽排序
  - 记住上次打开的标签页（可选）
  - 支持固定常用标签页

#### 2.1.3 插件系统
- **功能描述**: 动态加载和管理功能插件
- **详细需求**:
  - 插件目录: `plugins/`
  - 支持热加载/卸载插件
  - 插件隔离运行，避免互相影响
  - 提供插件 API 接口供开发者使用
  - 插件市场（后续版本）

#### 2.1.4 导航菜单
- **功能描述**: 左侧边栏导航，组织和管理所有功能模块
- **详细需求**:
  - 分组显示: 核心功能、已安装插件、占位功能
  - 支持图标和文字标签
  - 插件未安装时显示占位状态
  - 点击导航项打开对应标签页

### 2.2 内置功能模块

#### 2.2.1 待办事项管理 (已内置)
- **功能描述**: 任务管理工具，支持优先级、分类、截止日期
- **功能特性**:
  - 创建/编辑/删除任务
  - 任务优先级: 高/中/低
  - 任务分类和标签
  - 截止日期和逾期提醒
  - 完成状态切换
  - 搜索和筛选功能

#### 2.2.2 设置中心 (已内置)
- **功能描述**: 应用全局设置管理
- **功能特性**:
  - 应用基本配置
  - 托盘行为设置
  - 日志级别配置
  - 插件管理

### 2.3 占位功能模块 (开发中)

#### 2.3.1 患者管理
- 患者信息录入和管理
- 就诊记录跟踪
- 预约管理

#### 2.3.2 日程安排
- 日历视图
- 事件提醒
- 重复任务设置

### 2.4 可选插件模块 (后续版本)

#### 2.4.1 WebView 容器插件
- **功能描述**: 嵌入 Web 应用的容器
- **技术方案**: 基于 Microsoft Edge WebView2
- **功能特性**:
  - 加载外部 Web 应用
  - JavaScript 双向通信
  - 本地 API 桥接
  - 支持多个 Web 应用实例

#### 2.4.2 其他插件
- 计算器
- 剪贴板历史
- 颜色拾取器
- 二维码生成器
- JSON 格式化工具
- 文件搜索工具

---

## 3. 技术架构

### 3.1 技术栈
| 层级 | 技术选型 | 版本 |
|-----|---------|------|
| UI 框架 | Avalonia UI | 11.3.12 |
| 主题 | Semi.Avalonia | 11.3.7 |
| MVVM 框架 | Prism.Avalonia | 9.0.537 |
| DI 容器 | DryIoc | 内置 |
| 配置管理 | Microsoft.Extensions.Configuration | 9.0.0 |
| 日志系统 | Serilog | 4.2.0 |
| 目标框架 | .NET | 10.0 |

### 3.2 项目结构
```
AvaloniaDemo/
├── App.axaml              # 应用入口和全局样式
├── App.axaml.cs           # 应用生命周期和依赖注入配置
├── Configuration/         # 配置相关
│   └── AppSettings.cs     # 应用配置类
├── Models/                # 数据模型
├── Services/              # 服务层
│   ├── IApplicationService.cs
│   ├── ITrayService.cs
│   └── ...
├── ViewModels/            # 视图模型 (MVVM)
├── Views/                 # 视图层 (XAML)
│   ├── MainWindow.axaml   # 主窗口
│   ├── HomeView.axaml     # 首页
│   ├── TodoView.axaml     # 待办事项
│   └── SettingsView.axaml # 设置
├── plugins/               # 插件目录 (运行时创建)
└── appsettings.json       # 配置文件
```

### 3.3 架构设计

#### 3.3.1 MVVM 模式
- **Model**: 数据实体，实现 `ObservableObject` 和 `[ObservableProperty]`
- **ViewModel**: 业务逻辑，继承 `ViewModelBase`，使用 `DelegateCommand`
- **View**: XAML UI 定义，使用编译绑定 (`x:DataType`)

#### 3.3.2 插件架构
```
┌─────────────────────────────────────┐
│           Universal Tray Tool       │
│  ┌─────────────┐  ┌─────────────┐  │
│  │  Core App   │  │ Plugin Host │  │
│  └──────┬──────┘  └──────┬──────┘  │
└─────────┼────────────────┼──────────┘
          │                │
          ▼                ▼
┌─────────────────┐  ┌─────────────────┐
│ Built-in Modules│  │  Plugin Modules │
│ - Todo          │  │ - WebView       │
│ - Settings      │  │ - Calculator    │
│                 │  │ - ...           │
└─────────────────┘  └─────────────────┘
```

#### 3.3.3 服务接口
```csharp
// 应用程序服务
public interface IApplicationService
{
    Task InitializeAsync();
    Task OnStartedAsync();
    Task OnShutdownAsync();
    string GetVersion();
}

// 托盘服务
public interface ITrayService
{
    void Initialize(Window window);
    void ShowWindow();
    void HideWindow();
    void ToggleWindow();
    void ExitApplication();
}

// 插件接口 (预留)
public interface IPluginModule
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    void Initialize(IServiceProvider serviceProvider);
    void Shutdown();
}
```

---

## 4. 用户界面设计

### 4.1 主窗口布局
```
┌─────────────────────────────────────────────────────┐
│  [标题栏]  Universal Tray Tool                [_][□][X]│
├────────────────┬────────────────────────────────────┤
│                │                                    │
│  ┌──────────┐  │  ┌──────────────────────────────┐  │
│  │ 首页     │  │  │ [Tab1] [Tab2] [Tab3+]     [+]│  │
│  │ 待办事项 │  │  ├──────────────────────────────┤  │
│  │ 设置     │  │  │                              │  │
│  ├──────────┤  │  │     标签页内容区域            │  │
│  │ 插件分组 │  │  │                              │  │
│  │ - 插件A  │  │  │                              │  │
│  │ - 插件B  │  │  │                              │  │
│  └──────────┘  │  └──────────────────────────────┘  │
│                │                                    │
└────────────────┴────────────────────────────────────┘
       250px                    自适应
```

### 4.2 设计规范
- **颜色主题**: 使用 Semi.Avalonia 设计系统
- **侧边栏**: 深色主题 (#2d2d2d)
- **内容区**: 浅色主题 (#fafafa)
- **字体**: Inter 字体族
- **圆角**: 8px 卡片圆角
- **动画**: 200ms 过渡动画

### 4.3 交互设计
- **悬停效果**: 按钮背景色变化
- **点击反馈**: 按钮缩放至 0.97
- **卡片效果**: 悬停时轻微上浮 + 阴影增强
- **标签页**: 支持关闭按钮、拖拽排序

---

## 5. 配置管理

### 5.1 配置文件
**appsettings.json**:
```json
{
  "Application": {
    "Name": "通用工具箱",
    "Version": "1.0.0",
    "MinimizeToTray": true,
    "StartWithSystem": false
  },
  "Tray": {
    "IconPath": "Assets/avalonia-logo.ico",
    "ShowNotifications": true
  },
  "Plugin": {
    "PluginDirectory": "plugins",
    "AutoLoad": true,
    "AllowUnsigned": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "LogPath": "logs"
  }
}
```

### 5.2 配置存储
- **默认配置**: 应用目录下的 `appsettings.json`
- **用户配置**: `%LocalAppData%/UniversalTrayTool/appsettings.json`
- **热重载**: 支持配置文件变更自动重载

---

## 6. 日志系统

### 6.1 日志配置
- **框架**: Serilog
- **输出**: 控制台 + 文件
- **文件滚动**: 按天滚动，保留 7 天
- **路径**: `logs/app-YYYYMMDD.log`

### 6.2 日志级别
- `Debug`: 开发调试信息
- `Information`: 常规操作日志
- `Warning`: 警告信息
- `Error`: 错误信息

---

## 7. 开发规范

### 7.1 命名规范
- **文件**: PascalCase (如 `MainWindow.axaml`)
- **类**: PascalCase (如 `MainWindowViewModel`)
- **方法**: PascalCase (如 `InitializeAsync`)
- **字段**: 下划线 + camelCase (如 `_logger`)
- **属性**: PascalCase (如 `CurrentView`)

### 7.2 代码组织
- 一个 View 对应一个 ViewModel
- 服务接口定义在 `Services/` 目录
- 视图模型继承 `ViewModelBase`
- 使用 `[ObservableProperty]` 自动生成属性

### 7.3 插件开发规范 (预留)
```csharp
public class MyPlugin : IPluginModule
{
    public string Id => "my.plugin";
    public string Name => "我的插件";
    public string Version => "1.0.0";

    public void Initialize(IServiceProvider serviceProvider)
    {
        // 插件初始化
    }

    public void Shutdown()
    {
        // 插件清理
    }
}
```

---

## 8. 测试策略

### 8.1 测试类型
- **单元测试**: 服务层和视图模型
- **集成测试**: 插件加载和生命周期
- **UI 测试**: 界面交互和布局

### 8.2 测试工具
- xUnit: 单元测试框架
- Avalonia.Headless: UI 测试支持

---

## 9. 发布计划

### 9.1 版本规划
| 版本 | 功能 | 时间 |
|-----|------|------|
| v1.0.0 | 基础框架 + 待办事项 + 设置 | 2026 Q1 |
| v1.1.0 | WebView 插件 + 患者管理 | 2026 Q2 |
| v1.2.0 | 日程安排 + 插件市场 | 2026 Q3 |
| v2.0.0 | 跨平台支持 + 更多插件 | 2026 Q4 |

### 9.2 发布目标
- **Windows**: 主要目标平台
- **macOS**: 后续支持
- **Linux**: 后续支持

---

## 10. 附录

### 10.1 参考资源
- [Avalonia UI 文档](https://docs.avaloniaui.net/)
- [Prism 框架文档](https://prismlibrary.com/docs/)
- [Semi.Avalonia 主题](https://github.com/irihitech/Semi.Avalonia)

### 10.2 术语表
| 术语 | 解释 |
|-----|------|
| MVVM | Model-View-ViewModel 设计模式 |
| DI | 依赖注入 (Dependency Injection) |
| Prism | 用于构建松耦合、可维护的 XAML 应用程序框架 |
| Avalonia | 跨平台 .NET UI 框架 |
| WebView2 | Microsoft Edge 基于 Chromium 的 WebView 控件 |

### 10.3 变更记录
| 日期 | 版本 | 变更内容 |
|-----|------|---------|
| 2026-02-19 | v1.0.0 | 初始 PRD 创建 |

---

**文档维护**: 开发团队
**审核状态**: 待审核
**下次评审**: 2026-03-01
