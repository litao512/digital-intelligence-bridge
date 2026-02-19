# 2026-02-19 回归清单（v1.0.2 UI 同步版）

## 1. 自动化验证（已执行）

| 项目 | 命令/方式 | 结果 |
|---|---|---|
| Debug 构建 | `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Debug` | 通过 |
| Release 构建 | `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Release` | 通过 |
| 启动冒烟 | `dotnet run --project digital-intelligence-bridge/digital-intelligence-bridge.csproj`（超时退出） | 可启动 |
| 托盘初始化日志 | 检查 `bin/Debug/net10.0/logs/app-20260219.log` 最新段 | 通过（无“加载托盘图标失败”） |
| ViewModel 自动化测试 | `dotnet digital-intelligence-bridge.Tests/bin/Debug/net10.0/digital-intelligence-bridge.Tests.dll` | 通过（5/5） |

## 2. 手动回归（需在桌面环境确认）

| 项目 | 预期 | 状态 |
|---|---|---|
| 托盘图标显示 | 右下角任务栏或 `^` 区域可见图标 | 待手动 |
| 托盘点击切换窗口 | 点击托盘图标可显示/隐藏窗口 | 待手动 |
| 导航高亮 | 左侧当前模块高亮（含设置） | 待手动 |
| Tab 行为 | 可打开/切换/关闭标签页，首页保底存在 | 待手动 |
| 首页快捷入口 | “打开待办/打开设置”可正确跳转 | 待手动 |
| Todo 空状态 | 空列表显示空状态；筛选为空可“清空筛选” | 待手动 |
| Todo 完成态 | 已完成任务标题划线、内容弱化 | 待手动 |
| 设置保存 | 保存后重启应用，设置可读取 | 待手动 |

## 3. 备注

- 本次已完成自动化冒烟与日志校验。
- 手动项建议在 Windows 桌面实际会话中逐条勾验，尤其是托盘显示行为（受系统“任务栏角落图标”设置影响）。
