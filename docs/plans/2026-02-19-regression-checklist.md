# 2026-02-19 回归清单（v1.0.3 Supabase/托盘自检版）

## 1. 自动化验证（已执行）

| 项目 | 命令/方式 | 结果 |
|---|---|---|
| Debug 构建 | `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Debug` | 通过 |
| xUnit 单元测试 | `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj -c Debug` | 通过（24/24） |
| 运行时配置生成 | `./scripts/new-runtime-config.ps1 -SupabaseUrl ... -SupabaseAnonKey ... -Schema dib` | 通过（写入 `%LOCALAPPDATA%/UniversalTrayTool/appsettings.runtime.json`） |
| Supabase 运行时连通性 | `./scripts/verify-supabase-runtime.ps1` | 通过（HTTP 200） |
| 配置模板有效性 | `Get-Content digital-intelligence-bridge/appsettings.runtime.template.json -Raw \| Test-Json` | 通过 |
| GitHub CI（主分支） | `CI` run `22184801875`（attempt 2/3/4） | 连续通过 |
| GitHub Supabase Runtime Check | run `22185147286` | 通过 |

## 2. 手动回归（需在桌面环境确认）

| 项目 | 预期 | 状态 |
|---|---|---|
| 托盘图标显示 | 右下角任务栏或 `^` 区域可见图标 | 待复验（已修复“文件存在性误报”逻辑） |
| 托盘点击切换窗口 | 点击托盘图标可显示/隐藏窗口 | 待手动 |
| 导航高亮 | 左侧当前模块高亮（含设置） | 待手动 |
| Tab 行为 | 可打开/切换/关闭标签页，首页保底存在 | 待手动 |
| 首页快捷入口 | “打开待办/打开设置”可正确跳转 | 待手动 |
| Todo 空状态 | 空列表显示空状态；筛选为空可“清空筛选” | 待手动 |
| Todo 完成态 | 已完成任务标题划线、内容弱化 | 待手动 |
| 设置保存 | 保存后重启应用，设置可读取 | 待手动 |

## 3. 备注

- 本次已完成 Supabase 真实环境 API 验收（`/rest/v1/todos?select=id&limit=1` 返回 `200`）。
- 主分支 `CI` 已修复 `--no-build` 误用问题，并在同一 run 的多次重跑中连续通过。
- 已新增运行时配置模板与脚本，避免将敏感信息提交到仓库。
- 手动项建议在 Windows 桌面实际会话中逐条勾验，尤其是托盘显示行为（受系统“任务栏角落图标”设置影响）。
