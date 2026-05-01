# DIB 客户端配置分层设计

## 背景

新电脑注册站点后，用户目录和程序目录的 `appsettings.json` 存在重复字段，排查过程中也暴露出配置边界不够直观的问题。当前代码已经倾向于把用户目录配置收敛为“用户可变项”，但缺少显式规则和诊断提示，导致现场容易误判 `ReleaseCenter.AnonKey`、站点资料、资源缓存之间的关系。

## 目标

明确两层配置职责：

- 程序目录 `appsettings.json` 保存随版本发布的默认配置、连接配置和非用户身份配置。
- 用户目录 `%LocalAppData%\DibClient\appsettings.json` 保存本机可变配置、站点身份和本机路径覆盖。

## 配置边界

程序目录配置保留：

- `Application.Name`
- `Application.Version`
- `Plugin`
- `Logging`
- `Supabase`
- `ReleaseCenter.Enabled`
- `ReleaseCenter.BaseUrl`
- `ReleaseCenter.Channel`
- `ReleaseCenter.AnonKey`

用户目录配置保留：

- `Application.MinimizeToTray`
- `Application.StartWithSystem`
- `Tray.ShowNotifications`
- `ReleaseCenter.Enabled`
- `ReleaseCenter.BaseUrl`
- `ReleaseCenter.Channel`
- `ReleaseCenter.SiteId`
- `ReleaseCenter.SiteName`
- `ReleaseCenter.SiteRemark`
- `ReleaseCenter.CacheDirectory`
- `ReleaseCenter.ClientCacheDirectory`
- `ReleaseCenter.StagingDirectory`
- `ReleaseCenter.RuntimePluginRoot`
- `ReleaseCenter.BackupDirectory`

`ReleaseCenter.Enabled`、`BaseUrl`、`Channel` 在用户目录中保留，是为了支持现场临时切换环境。`ReleaseCenter.AnonKey` 不写入用户目录，运行时从程序目录默认配置合并进入最终配置。

## 数据流

客户端启动时先加载程序目录 `appsettings.json`，再加载用户目录 `appsettings.json`，最后加载环境变量。用户配置只覆盖本机可变字段；默认连接配置仍由程序目录提供。保存站点资料时只更新用户目录中的站点资料字段，不应写入 `AnonKey`、`Plugin`、`Logging`、`Supabase` 等程序默认配置。

## 错误处理

如果最终合并后的 `ReleaseCenter` 缺少 `AnonKey`，资源中心 RPC 不会执行。需要在日志或设置检查中给出更明确的诊断，说明“发布中心匿名密钥缺失，授权资源无法刷新”，避免用户只看到插件缺数据库连接。

## 测试策略

新增或调整配置测试，覆盖：

- 首次生成用户配置时不包含 `ReleaseCenter.AnonKey`、`Plugin`、`Logging`。
- 旧用户配置如果包含 `ReleaseCenter.AnonKey`，归一化后会移除。
- 旧用户配置缺少 `Enabled`、`BaseUrl`、`Channel` 时，会从程序目录默认配置回填。
- 用户配置缺少 `AnonKey` 时，最终合并配置仍能从程序目录默认配置获得 `AnonKey`。
- 保存站点资料后，用户配置仍不包含 `ReleaseCenter.AnonKey`。

## 非目标

本次不把 `BaseUrl`、`Channel`、`Enabled` 从用户配置中移除，因为它们是有用的本机环境覆盖项。本次也不把数据库连接串或资源密钥写入任何用户配置文件。
