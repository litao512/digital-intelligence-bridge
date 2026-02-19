# Universal Tray Tool (UTT) - 通用工具箱
## 产品需求文档 (Product Requirements Document)

**文档版本**: v1.0.3  
**发布日期**: 2026-02-19  
**文档状态**: 草案（UI 同步版）

---

## 1. 产品概述

### 1.1 产品定位
Universal Tray Tool (UTT) 是一款基于 Avalonia UI + Prism 的跨平台桌面托盘应用框架。  
产品目标是在一个统一入口中整合多个工具模块，支持常驻托盘、快速唤起、标签页管理，并逐步演进为插件化工具平台。

### 1.2 核心价值
- **统一入口**: 将常用工具聚合到一个轻量桌面入口中。
- **托盘常驻**: 通过系统托盘实现快速显示/隐藏，减少窗口切换成本。
- **插件化扩展**: 支持内置模块与外部插件共存，逐步支持动态加载。
- **跨平台演进**: 当前以 Windows 为主，后续扩展至 macOS / Linux。

### 1.3 目标用户
| 用户类型 | 痛点 | 产品价值 |
|---|---|---|
| 个人效率用户 | 工具分散、切换频繁 | 提供统一工具入口与快速访问 |
| 团队成员 | 日常任务与轻量流程缺少统一载体 | 提供待办、配置、扩展工具容器 |
| 系统管理员/实施人员 | 需要可控的桌面入口应用 | 可通过配置和插件控制功能范围 |

---

## 2. 功能需求

### 2.1 核心能力

#### 2.1.1 系统托盘
- 托盘图标常驻。
- 单击托盘图标可切换主窗口显示/隐藏。
- 托盘菜单支持快速操作（显示主窗口、打开设置、退出应用）。
- 支持托盘通知开关配置。

#### 2.1.2 导航与标签页
- 左侧导航栏显示可用模块入口。
- 当前导航项支持激活态高亮（左侧激活条 + 文本/图标联动）。
- 右侧采用 Tab 形式承载模块内容。
- 支持打开、切换、关闭标签页。
- 首页标签页默认存在，不允许关闭最后一个首页标签页。
- Tab 切换带轻量淡入过渡动画。

#### 2.1.3 内置模块（当前）
- **首页**: 结构化欢迎页（欢迎区 + 信息卡 + 今日概览），并提供“打开待办/打开设置”快捷入口（后续可替换为插件仓库页）。
- **待办事项**: 任务 CRUD、搜索、筛选、统计、完成状态切换；包含空状态引导（无数据/筛选为空）。
- **设置中心**: 基础应用设置、托盘设置、日志级别等配置项。

#### 2.1.4 占位模块（规划）
- 患者管理（占位）
- 日程安排（占位）

### 2.2 插件系统（分阶段）

#### 2.2.1 MVP 目标
- 约定插件目录：`plugins/`
- 应用启动时扫描插件目录（只做发现与状态展示）
- 导航栏可展示“已安装/未安装”状态

#### 2.2.2 后续演进
- 动态加载/卸载插件
- 插件隔离与异常保护
- 插件 API（生命周期、服务访问、菜单注入）
- WebView 容器插件（可选）

---

## 3. 技术架构

### 3.1 技术栈
| 层级 | 技术 | 版本（当前） |
|---|---|---|
| UI 框架 | Avalonia UI | 11.3.x |
| 主题 | Semi.Avalonia | 11.3.x |
| MVVM | Prism.Avalonia | 9.0.x |
| DI | DryIoc | Prism 集成 |
| 配置 | Microsoft.Extensions.Configuration | 9.0.x |
| 日志 | Serilog | 4.x |
| 后端服务 | Supabase（本地服务器部署） | self-hosted |
| 数据库 | PostgreSQL（由 Supabase 管理） | Supabase 默认版本 |
| 运行时 | .NET | 10.0 |

> 说明：后端与数据库统一采用本地服务器部署的 Supabase（self-hosted）方案，应用通过 Supabase API 访问认证、存储与数据库能力。

### 3.2 目录结构
```text
digital-intelligence-bridge/
├── App.axaml
├── App.axaml.cs
├── Configuration/
├── Models/
├── Services/
├── ViewModels/
├── Views/
├── Assets/
├── plugins/               # 运行时插件目录（规划）
└── appsettings.json
```

### 3.3 架构原则
- 严格 MVVM 分层：业务逻辑放在 ViewModel/Service。
- 视图层优先使用编译绑定（`x:DataType`）。
- 服务通过接口抽象，便于替换和测试。
- 插件能力通过独立接口定义生命周期。

---

## 4. 界面与交互设计

### 4.1 总体布局
- 左侧：导航栏（固定宽度）
- 右侧：Tab 内容区域（主工作区）
- 顶层：系统窗口 + 托盘交互
- 首页采用“信息面板式”布局，待办与设置采用卡片分区布局。

### 4.2 视觉系统（统一规范）
- 主色：`#0077FA`
- 成功色：`#3BB346`
- 警告色：`#FCA321`
- 错误色：`#F93920`
- 导航背景：`#1F2329`
- 页面背景：`#F0F2F5`
- 卡片背景：`#FFFFFF`

### 4.3 动效与交互
- 按钮悬停：150~200ms 过渡
- 按钮点击：`scale(0.97~0.98)`
- 卡片悬停：轻微上浮 + 阴影增强
- 页面切换：Tab 内容淡入（约 180ms）
- 待办完成态：标题划线 + 内容弱化，强调信息状态变化
- 空状态：提供说明文案与操作入口（如“清空筛选”）

### 4.4 响应式约束
- 推荐最小窗口：`980 x 620`
- 中屏默认布局：导航固定 + 内容自适应
- 小屏（<1000）支持折叠导航（规划）

---

## 5. 配置与日志

### 5.1 配置文件
主配置文件：`appsettings.json`

核心配置项：
- `Application.Name / Version / MinimizeToTray / StartWithSystem`
- `Tray.IconPath / ShowNotifications`
- `Plugin.PluginDirectory / AutoLoad / AllowUnsigned`
- `Logging.LogLevel / LogPath`
- `Supabase.Url / AnonKey / ServiceRoleKey / Schema`

Supabase 部署约束：
- 开发、测试、生产环境均使用本地服务器部署的 Supabase 实例。
- 严禁将 `AnonKey`、`ServiceRoleKey` 明文提交到仓库，需改用环境变量或安全配置注入。
- 若涉及数据库结构变更，需同步提供迁移脚本与回滚方案。

### 5.2 日志策略
- 使用 Serilog 输出到控制台 + 文件
- 日志路径：`logs/app-YYYYMMDD.log`
- 级别：`Debug / Information / Warning / Error`

---

## 6. 非功能需求

### 6.1 性能
- 常驻后台内存占用可控
- 主窗口显示/隐藏响应快速
- 常见操作（切页、筛选）无明显卡顿

### 6.2 稳定性
- 插件异常不影响主程序核心功能
- 启动流程具备错误日志与降级策略

### 6.3 安全与合规
- 不在仓库提交敏感密钥
- 插件加载需要基础签名/可信策略（规划）

---

## 7. 测试与验收

### 7.1 最低验收标准（v1.0.x）
- Debug/Release 构建通过
- 托盘显示/隐藏逻辑正确
- 导航与 Tab 操作正确
- 导航激活态高亮与设置入口高亮生效
- Todo 模块核心流程可用
- Todo 空状态与筛选空状态展示正确
- 首页快捷入口可跳转到待办与设置
- 设置项可读可写并持久化

### 7.2 建议测试策略
- 新增测试工程：`digital-intelligence-bridge.Tests`
- 优先覆盖：
  - `MainWindowViewModel` 导航与标签逻辑
  - Todo 筛选与统计逻辑
  - 配置加载与保存流程

---

## 8. 版本规划

### 8.1 路线图
| 版本 | 目标 | 时间 |
|---|---|---|
| v1.0.0 | 托盘 + 待办 + 设置基础能力 | 2026 Q1 |
| v1.1.0 | 插件仓库首页 + 插件发现机制 | 2026 Q2 |
| v1.2.0 | 插件加载生命周期 + 远程菜单配置 | 2026 Q3 |
| v2.0.0 | 跨平台增强 + 插件生态完善 | 2026 Q4 |

---

## 9. 风险与依赖

### 9.1 主要风险
- WebView 能力在跨平台兼容上存在差异。
- 插件隔离与安全策略实现复杂度高。
- 需求扩展速度可能超过架构演进速度。

### 9.2 关键依赖
- Avalonia 生态能力与版本稳定性
- Prism + DryIoc 的导航和依赖注入实践
- Windows 平台托盘行为兼容性

---

## 10. 文档管理

**维护人**: 项目组  
**评审状态**: 待评审  
**下次评审日期**: 2026-03-01

### 变更记录
| 日期 | 版本 | 变更说明 |
|---|---|---|
| 2026-02-19 | v1.0.3 | 增加技术栈说明：后端与数据库采用本地部署 Supabase |
| 2026-02-19 | v1.0.2 | 同步 UI 改造：导航激活态、空状态、快捷入口、Tab 过渡 |
| 2026-02-19 | v1.0.1 | 重建 PRD 内容并修复 UTF-8 乱码 |
| 2026-02-19 | v1.0.0 | 初版创建 |

---

## 11. Runtime Sensitive Config Delivery Plan (Draft)

### 11.1 Temporary phase (current)
- For delivery progress, clients can read sensitive runtime config from `%LOCALAPPDATA%/UniversalTrayTool/appsettings.runtime.json`.
- This file is local-machine only and must never be committed to repository.
- Effective config loading order: `appsettings.json` -> user `appsettings.json` -> `appsettings.runtime.json` -> environment variables.

### 11.2 Target phase (to implement with plugin repository)
- Introduce remote config distribution service for desktop clients.
- Client fetches signed runtime config (Url/AnonKey/Schema/configVersion/expiresAt).
- Client verifies signature with embedded public key before applying config.
- Client persists last valid config as local cache and supports offline fallback.
- ServiceRoleKey must stay server-side only; desktop client never stores it.

### 11.3 Rotation strategy
- Support overlap window for old/new runtime config during key or endpoint rotation.
- Add `minAppVersion` in remote config to coordinate forced upgrade when required.
