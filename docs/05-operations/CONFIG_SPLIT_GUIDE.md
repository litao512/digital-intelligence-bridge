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
4. `ReleaseCenter.Enabled`
5. `ReleaseCenter.BaseUrl`
6. `ReleaseCenter.Channel`
7. `ReleaseCenter.SiteId`
8. `ReleaseCenter.SiteName`
9. `ReleaseCenter.SiteRemark`
10. `ReleaseCenter.CacheDirectory`
11. `ReleaseCenter.ClientCacheDirectory`
12. `ReleaseCenter.StagingDirectory`
13. `ReleaseCenter.RuntimePluginRoot`
14. `ReleaseCenter.BackupDirectory`

## 生命周期与写入规则
1. 首次启动：如果用户目录配置不存在，系统创建白名单模板文件，不复制运行目录整份配置。
2. 运行中保存：仅回写用户白名单字段，不写基线字段。
3. 后续升级：运行目录基线变更可自动生效，前提是对应字段未进入用户白名单。

## 发布打包要求
`ReleaseCenter.AnonKey` 必须写入发布包运行目录的 `appsettings.json`。`scripts/publish-release.ps1` 会按以下顺序解析并注入：

1. 命令参数 `-ReleaseCenterAnonKey`
2. 环境变量 `RELEASE_CENTER_ANON_KEY`
3. 环境变量 `VITE_SUPABASE_ANON_KEY`
4. 环境变量 `SUPABASE_ANON_KEY`
5. `dib-release-center/.env.local` 中的同名配置

如果 `ReleaseCenter.Enabled = true` 且最终无法取得 `ReleaseCenter.AnonKey`，打包脚本必须失败，不允许产出缺少授权资源刷新能力的客户端包。

## 诊断提示
如果新机器打开插件后无法获取数据库连接，先检查运行目录 `appsettings.json` 是否包含非空 `ReleaseCenter.AnonKey`。客户端启动或资源刷新时，如果最终合并配置缺少该字段，日志会出现：

```text
ReleaseCenter.AnonKey 未配置，已跳过授权资源刷新。请检查程序目录 appsettings.json 是否包含发布中心匿名密钥。
```

此时 `%LOCALAPPDATA%/DibClient/resource-cache/authorized-resources.json` 通常不会生成。修复运行目录配置后，重启客户端并重新执行插件初始化或检查更新即可刷新资源缓存。

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
