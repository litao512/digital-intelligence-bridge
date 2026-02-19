# Repository Guidelines

## 项目结构与模块组织
- 主应用位于 `AvaloniaDemo/`（单一 .NET 桌面项目）。
- UI 层在 `AvaloniaDemo/Views/*.axaml`，对应代码后置在 `*.axaml.cs`。
- 视图模型在 `AvaloniaDemo/ViewModels/`；数据模型在 `AvaloniaDemo/Models/`。
- 通用能力放在 `AvaloniaDemo/Services/`、`AvaloniaDemo/Configuration/`，值转换器在 `AvaloniaDemo/Converters/`。
- 静态资源位于 `AvaloniaDemo/Assets/`，全局样式与资源入口为 `App.axaml`。
- 方案和设计记录在 `docs/plans/`。

## 构建、测试与开发命令
默认在仓库根目录执行。
- `dotnet restore AvaloniaDemo/AvaloniaDemo.csproj`：还原 NuGet 依赖。
- `dotnet build AvaloniaDemo/AvaloniaDemo.csproj -c Debug`：本地调试构建。
- `dotnet run --project AvaloniaDemo/AvaloniaDemo.csproj`：启动桌面应用。
- `dotnet watch --project AvaloniaDemo/AvaloniaDemo.csproj run --hot-reload`：热重载开发循环。
- `dotnet build AvaloniaDemo/AvaloniaDemo.csproj -c Release`：发布配置构建校验。

## 代码风格与命名约定
- 使用 4 空格缩进，保持可空引用类型启用（`<Nullable>enable</Nullable>`）。
- C# 约定：文件作用域命名空间、类型/成员使用 `PascalCase`、私有字段使用 `_camelCase`。
- 严格遵循 MVVM 分层：业务逻辑放在 ViewModel/Service，代码后置只保留最少 UI 交互逻辑。
- XAML 视图应声明 `x:DataType`，以使用编译绑定并减少运行时绑定错误。
- 方法尽量短小、职责单一，仅对“非显而易见”的意图添加注释。

## 测试指南
- 当前仓库未包含独立测试项目。
- 新功能建议新增测试工程（推荐命名：`AvaloniaDemo.Tests`）。
- 测试文件命名：`<ClassName>Tests.cs`；测试方法命名：`MethodName_ShouldExpectedBehavior_WhenCondition`。
- 最低验证要求：`Debug/Release` 可构建、关键 ViewModel 行为正确、核心 UI 流程可手动回归。

## 提交与合并请求规范
- 当前快照无法读取 Git 历史，无法可靠提炼既有提交风格。
- 建议统一使用 Conventional Commits，例如：`feat: 增加托盘菜单动作`、`fix: 处理 SelectedTab 为空`。
- PR 至少包含：变更摘要、影响模块、验证步骤；UI 变更附截图或 GIF。
- 若涉及配置变更（如 `appsettings.json`），请在 PR 描述中明确说明兼容性和迁移方式。

## 安全与配置提示
- 不要在 `appsettings.json` 中提交密钥或凭据，优先使用环境变量或环境专属配置。
- 不要提交构建产物和日志（如 `bin/`、`obj/`、`webview_debug.log`、运行时 `logs/`）。
