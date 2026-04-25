# 双配置分工说明（运行目录与用户目录）

## 目的
本文定义 `appsettings.json` 的双配置分工，避免运行目录与用户目录职责混淆，降低重复配置和运维风险。

## 配置文件位置
1. 运行目录：`<AppContext.BaseDirectory>/appsettings.json`
2. 用户目录：`%LOCALAPPDATA%/DibClient/appsettings.json`

## 生效优先级
1. 先加载运行目录配置（默认基线）。
2. 再加载用户目录配置（用户覆盖）。
3. 最终运行时配置 = 基线 + 用户覆盖。

## 运行目录（基线配置）职责
运行目录配置只承载发布基线与环境默认值，重点是“可发布、可审计、可统一下发”。

建议保留在运行目录的字段：
1. `Application.Name`
2. `Application.Version`
3. `Tray.IconPath`
4. `Plugin.AutoLoad`
5. `Plugin.AllowUnsigned`
6. `Supabase.*`
7. `ReleaseCenter.Enabled`
8. `ReleaseCenter.BaseUrl`
9. `ReleaseCenter.Channel`
10. `ReleaseCenter.AnonKey`
11. `Logging.LogLevel.*`

## 用户目录（白名单覆盖）职责
用户目录配置只承载用户可变状态与本机差异项，采用固定白名单结构。

当前白名单字段：
1. `Application.MinimizeToTray`
2. `Application.StartWithSystem`
3. `Tray.ShowNotifications`
4. `ReleaseCenter.SiteId`
5. `ReleaseCenter.SiteOrganization`
6. `ReleaseCenter.SiteName`
7. `ReleaseCenter.SiteRemark`
8. `ReleaseCenter.CacheDirectory`
9. `ReleaseCenter.ClientCacheDirectory`
10. `ReleaseCenter.StagingDirectory`
11. `ReleaseCenter.RuntimePluginRoot`
12. `ReleaseCenter.BackupDirectory`

## 生命周期与写入规则
1. 首次启动：如果用户目录配置不存在，系统创建白名单模板文件，不复制运行目录整份配置。
2. 运行中保存：仅回写用户白名单字段，不写基线字段。
3. 后续升级：运行目录基线变更可自动生效，前提是对应字段未进入用户白名单。

## 变更控制规范
1. 新增字段前先判定归属：
   - 发布环境统一策略 -> 放运行目录。
   - 用户偏好或机器状态 -> 放用户目录白名单。
2. 禁止把密钥写入用户目录（除非明确设计为用户输入型密钥）。
3. 禁止在用户保存链路中直接序列化 `AppSettings` 全量对象。

## 常见误区
1. 误区：用户目录字段越全越安全。
   - 纠正：全量复制会固化旧默认值，削弱后续发布配置的可控性。
2. 误区：只有一个配置文件最简单。
   - 纠正：桌面端需要区分“发布基线”和“用户状态”，分层更可维护。

## 验收检查清单
1. 删除 `%LOCALAPPDATA%/DibClient/appsettings.json` 后启动应用，能自动生成白名单结构。
2. 修改运行目录 `ReleaseCenter.Channel`，用户目录未覆盖该字段时，运行时应读取新值。
3. 在设置页面修改托盘或站点信息后，用户目录只变化白名单字段。
4. 用户目录中的中文字段应保持直写，不应出现 `\uXXXX`。
