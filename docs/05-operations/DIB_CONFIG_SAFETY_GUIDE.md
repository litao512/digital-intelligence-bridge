# DIB 配置安全指南

## 背景

桌面客户端的正式运行配置默认位于：

- `%LOCALAPPDATA%\\UniversalTrayTool\\appsettings.json`

此前联调和测试曾把以下测试值写入真实用户配置，导致正式运行反复误连测试环境：

- `Application.Name = TestApp`
- `Plugin.PluginDirectory = plugins-tests`
- `ReleaseCenter.BaseUrl = http://release-center.local`

当前已增加启动前污染检测。命中上述测试特征时，DIB 会直接阻止继续启动，并提示修复用户配置。

## 正式配置与测试配置

### 正式运行

- 配置根目录：`%LOCALAPPDATA%\\UniversalTrayTool`
- 主配置：`appsettings.json`
- 安装目录默认配置：安装目录下的 `appsettings.json`

### 测试/联调

- 必须使用独立沙箱目录
- 通过环境变量 `DIB_CONFIG_ROOT` 指向临时目录
- 如需允许测试配置值，额外设置：`DIB_ALLOW_UNSAFE_CONFIG=1`

说明：

- `DIB_CONFIG_ROOT` 用于切换配置根目录
- `DIB_ALLOW_UNSAFE_CONFIG=1` 仅用于测试，允许 `TestApp`、`plugins-tests`、`release-center.local` 这类测试特征通过校验

## 如何判断配置被测试环境污染

出现以下任一情况，应优先检查用户配置文件：

1. 启动时报“检测到测试配置污染”
2. 设置页或日志里出现：
   - `TestApp`
   - `plugins-tests`
   - `release-center.local`
3. 初始化插件时反复请求测试地址，而不是 `prod101`

重点文件：

- `%LOCALAPPDATA%\\UniversalTrayTool\\appsettings.json`

## 恢复正式运行配置

### 方案一：直接修复用户配置

至少确认这些值为正式环境值：

- `Application.Name = 通用工具箱`
- `Plugin.PluginDirectory = plugins`
- `ReleaseCenter.BaseUrl = http://101.42.19.26:8000`
- `ReleaseCenter.Channel = stable`

### 方案二：删除用户配置后重建

删除：

- `%LOCALAPPDATA%\\UniversalTrayTool\\appsettings.json`

然后重新启动 DIB，程序会从安装目录默认配置复制一份新的用户配置。

## 当前最佳实践

对于这个托盘插件宿主，配置只保留两层：

1. 安装目录 `appsettings.json`
- 用于分发默认配置
- 应包含目标环境的 `ReleaseCenter` 默认值

2. `%LOCALAPPDATA%\\UniversalTrayTool\\appsettings.json`
- 用于保存本机用户状态
- 例如 `SiteId`、`SiteName`、主题、托盘偏好

不再使用 `appsettings.runtime.json`。

## 测试约束

从现在开始，所有会写配置文件的单元测试都必须：

1. 使用临时配置根目录
2. 不得写 `%LOCALAPPDATA%\\UniversalTrayTool`
3. 如需使用测试配置值，必须显式设置 `DIB_ALLOW_UNSAFE_CONFIG=1`

## 排障建议

若怀疑再次误用了测试配置，按此顺序排查：

1. 查看 `%LOCALAPPDATA%\\UniversalTrayTool\\appsettings.json`
2. 搜索是否存在：
   - `TestApp`
   - `plugins-tests`
   - `release-center.local`
3. 确认当前实际运行的是正式 `ReleaseCenter.BaseUrl`
4. 如无法快速修复，直接删除用户配置并重启应用
