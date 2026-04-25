# 配置架构说明

## 1. 目标

客户端是一个桌面托盘宿主，配置体系必须简单、可预测、可排障。

当前正式规则只允许两层配置：

1. 安装目录 `appsettings.json`
2. `%LocalAppData%\DibClient\appsettings.json`

禁止再引入第三层运行时配置文件，例如 `appsettings.runtime.json`。

## 2. 设计原则

### 2.1 安装目录配置

安装目录 `appsettings.json` 负责承载部署默认配置。

适合放入：

- `Application.Name`
- `Application.Version`
- `ReleaseCenter.Enabled`
- `ReleaseCenter.BaseUrl`
- `ReleaseCenter.Channel`
- `ReleaseCenter.AnonKey`
- 默认日志路径
- 默认插件目录

不适合放入：

- `SiteId`
- `SiteName`
- `SiteRemark`
- 用户个性化偏好
- 插件业务数据库连接
- 插件外部服务凭据

### 2.2 用户配置

`%LocalAppData%\DibClient\appsettings.json` 负责承载本机用户状态。

适合放入：

- `SiteId`
- `SiteName`
- `SiteRemark`
- 主题偏好
- 托盘行为偏好
- 其他运行时会变化的状态

### 2.3 配置加载顺序

正式运行时，配置加载顺序固定为：

1. 安装目录 `appsettings.json`
2. 用户目录 `appsettings.json`
3. 环境变量

后加载的值覆盖先加载的值。

补充原则：

- 主程序配置只负责宿主部署控制和本机状态
- 插件正式业务资源不得通过主程序配置或宿主环境变量进入系统

## 3. 初始化与补齐规则

### 3.1 首次启动

如果用户配置不存在：

1. 基于当前默认配置生成“用户白名单结构”
2. 写入 `%LocalAppData%\DibClient\appsettings.json`
3. 后续仅写回用户白名单字段，不写入部署基线字段

### 3.2 已存在用户配置

如果用户配置已存在，不允许整文件覆盖，也不允许回写全量 `AppSettings`。

允许在写回用户配置时规范化为固定白名单结构，且不得写入安装目录基线字段（例如 `ReleaseCenter.BaseUrl`、`ReleaseCenter.Channel`、`Supabase.*`）。

不允许覆盖：

- `SiteId`
- `SiteName`
- `SiteRemark`
- 用户已经修改过的偏好项

## 4. 测试与联调约束

测试和联调不得读写正式用户配置目录。

测试覆盖目录通过环境变量指定：

- `DIB_CONFIG_ROOT`

使用规则：

1. 正式运行默认使用 `%LocalAppData%\DibClient`
2. 测试或联调时，通过 `DIB_CONFIG_ROOT` 指向独立沙箱目录
3. 不再支持 `DIB_CONFIG_DIR`

## 5. 禁止事项

以下做法属于违例：

1. 新增 `appsettings.runtime.json`
2. 在发布包中再引入第三层运行时配置文件
3. 测试直接写 `%LocalAppData%\DibClient`
4. 为了临时排障在仓库 `appsettings.json` 中固化测试配置

## 6. 常见错误

### 6.1 旧用户配置污染新包

现象：

- 新包已更新，但运行时仍表现为旧配置

根因：

- 目标机已存在 `%LocalAppData%\DibClient\appsettings.json`
- 程序按设计优先读取用户配置

正确处理：

1. 使用“缺字段补齐”机制修复
2. 不通过新增第三层配置绕过问题

### 6.2 测试配置污染正式环境

现象：

- `Application.Name = TestApp`
- `PluginDirectory = plugins-tests`
- `ReleaseCenter.BaseUrl = release-center.local`

正确处理：

1. 测试必须使用 `DIB_CONFIG_ROOT`
2. 启动时保留污染检测并阻止继续运行

## 7. 相关文档

- [DIB_CONFIG_SAFETY_GUIDE.md](./DIB_CONFIG_SAFETY_GUIDE.md)
- [RUNTIME_DIRECTORY_GUIDE.md](./RUNTIME_DIRECTORY_GUIDE.md)
- [NEW_MACHINE_SETUP_GUIDE.md](./NEW_MACHINE_SETUP_GUIDE.md)
- [CONFIG_SPLIT_GUIDE.md](./CONFIG_SPLIT_GUIDE.md)
