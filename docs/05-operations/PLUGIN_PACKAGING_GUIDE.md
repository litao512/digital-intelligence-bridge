# DIB 插件打包与目录规范

## 目的

本文档用于固定 DIB 插件的运行时边界，避免再次出现“插件主程序集存在，但依赖 DLL 缺失，运行时报 `Could not load file or assembly`”的问题。

适用范围：

- 所有通过 DIB 宿主加载的外部插件
- 所有发布中心下载、预安装、激活到 `%LOCALAPPDATA%\\UniversalTrayTool\\plugins` 的插件包

## 核心原则

### 1. 插件目录必须自包含

每个插件目录都必须是一个**完整可运行单元**。

不能假设以下位置会替插件补依赖：

- 主程序安装目录
- NuGet 全局缓存
- 其他插件目录
- 发布中心缓存目录

宿主只保证：

1. 扫描插件目录
2. 读取 `plugin.json`
3. 从插件目录内解析程序集依赖

如果插件运行所需 DLL 不在插件目录内，运行时失败是预期行为。

### 2. 宿主不兜底插件依赖

当前宿主的 `PluginLoadContext` 只会在**当前插件目录**内按程序集名查找 DLL。

这意味着：

- 插件缺什么依赖，就必须把什么依赖打进插件目录
- `.deps.json` 仅描述依赖关系，不会自动把 DLL 变出来

### 3. 程序目录与插件目录职责分离

程序安装目录只放：

- `digital-intelligence-bridge.exe`
- 宿主依赖
- 默认配置模板

运行时插件统一放：

- `%LOCALAPPDATA%\\UniversalTrayTool\\plugins\\<plugin-id>`

不要把“主程序目录里刚好有某个 DLL”当成插件可运行的前提。

## 当前目录模型

### 主程序安装目录

职责：

- 启动宿主
- 提供安装默认配置 `appsettings.json`

不负责：

- 存储运行时插件
- 为插件提供缺失依赖

### 插件正式目录

路径：

- `%LOCALAPPDATA%\\UniversalTrayTool\\plugins\\<plugin-id>`

职责：

- 存放插件主程序集
- 存放插件运行依赖
- 存放 `plugin.json`、`plugin.settings.json`

### 发布中心相关目录

- 下载缓存：`%LOCALAPPDATA%\\UniversalTrayTool\\release-cache\\plugins\\<channel>`
- 预安装目录：`%LOCALAPPDATA%\\UniversalTrayTool\\release-staging\\plugins\\<channel>`
- 正式插件目录：`%LOCALAPPDATA%\\UniversalTrayTool\\plugins`

发布中心只负责“下载 -> 预安装 -> 激活”，不负责修复插件缺依赖。

## 插件目录最低要求

每个插件目录至少应包含：

1. `plugin.json`
2. `plugin.settings.json`
3. 插件主程序集，例如 `PatientRegistration.Plugin.dll`
4. 插件 `.deps.json`
5. 所有运行时依赖 DLL
6. 必要的 `runtimes/` 原生依赖目录

如果插件引用了第三方包，例如 `QRCoder`，则插件目录内必须存在：

- `QRCoder.dll`

## 推荐构建方式

对于 SDK 风格插件项目，建议在插件项目文件中显式启用：

```xml
<PropertyGroup>
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>
```

作用：

- 将 NuGet 运行时依赖复制到插件输出目录
- 使插件输出更接近最终可部署目录

这不是“可选优化”，而是当前宿主加载模型下的必要条件。

## 发布前检查清单

发布一个插件包前，至少检查：

1. 插件输出目录存在主程序集
2. 插件输出目录存在 `.deps.json`
3. 关键第三方依赖 DLL 已复制
4. 若有原生依赖，`runtimes/` 目录完整
5. 将输出目录整体打包，而不是只拷主 DLL

## 测试建议

建议为每个关键插件补一条“包装完整性测试”，至少断言：

- 关键依赖 DLL 存在于输出目录

例如：

- `PatientRegistration.Plugin` 需要断言 `QRCoder.dll` 存在

这样能在发布前直接暴露缺依赖问题，而不是等用户点击业务按钮时才失败。

## 常见错误认识

### 错误 1：主程序目录里有 DLL，所以插件也能用

不成立。

当前插件加载器按插件目录解析依赖，不按主程序目录兜底。

### 错误 2：有 `.deps.json` 就够了

不成立。

`.deps.json` 只描述依赖关系，前提仍然是物理 DLL 已存在。

### 错误 3：下载成功就等于插件完整

不成立。

下载成功只能说明 zip 拿到了，不能说明 zip 里的内容完整。

## 当前结论

后续所有 DIB 外部插件都应遵守这一条规则：

> 插件目录必须是完整、独立、可运行的最小部署单元。

任何依赖主程序目录、依赖其他插件目录、依赖 NuGet 缓存“碰巧可用”的做法，都视为不合格打包方案。
