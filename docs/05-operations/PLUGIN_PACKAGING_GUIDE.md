# DIB 插件打包与目录规范

## 目的

本文档用于固定 DIB 插件的运行时边界，避免再次出现“插件主程序集存在，但依赖 DLL 缺失，运行时报 `Could not load file or assembly`”的问题。

适用范围：

- 所有通过 DIB 宿主加载的外部插件
- 所有发布中心下载、预安装、激活到 `%LOCALAPPDATA%\\DibClient\\plugins` 的插件包

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

- `%LOCALAPPDATA%\\DibClient\\plugins\\<plugin-id>`

不要把“主程序目录里刚好有某个 DLL”当成插件可运行的前提。

## 当前目录模型

### 源码、中转、运行与发布目录边界

当前插件目录分为四层，不能混用：

1. 插件源码目录：`plugins-src/<PluginName>.Plugin/`
2. 本地产包中转目录：仓库根 `plugins/<PluginName>/`
3. 客户端正式运行目录：`%LOCALAPPDATA%\\DibClient\\plugins\\<PluginName>`
4. 发布包内插件目录：`artifacts/releases/<version>/publish/plugins/<PluginName>`

职责边界如下：

- `plugins-src/` 是插件代码和模板配置的维护入口，必须提交到 Git。
- 仓库根 `plugins/` 由发布脚本或本地重建脚本生成，已被 `.gitignore` 忽略，不提交到 Git。
- `%LOCALAPPDATA%\\DibClient\\plugins` 是客户端实际加载、发布中心激活和回滚使用的运行时目录。
- `publish/plugins/` 是最终 zip 包内的随包插件目录，必须完整包含插件运行依赖。

发布脚本必须从 `plugins-src/` 重新构建插件，再刷新仓库根 `plugins/` 中转目录，最后复制到 `publish/plugins/`。不要手工把旧的 `plugins/` 内容当作源码真相，也不要把 `plugins/` 或 `artifacts/` 加入提交。

### 主程序安装目录

职责：

- 启动宿主
- 提供安装默认配置 `appsettings.json`

不负责：

- 存储运行时插件
- 为插件提供缺失依赖

### 插件正式目录

路径：

- `%LOCALAPPDATA%\\DibClient\\plugins\\<plugin-id>`

职责：

- 存放插件主程序集
- 存放插件运行依赖
- 存放 `plugin.json`、`plugin.settings.json`
- 开发联调时可额外放置 `plugin.development.json`

补充说明：

- 仓库根目录 `plugins\\<plugin-id>` 不是正式运行目录
- 手工回归时，如需验证最新插件，必须把构建产物同步到 `%LOCALAPPDATA%\\DibClient\\plugins\\<plugin-id>`
- 若设置了 `DIB_CONFIG_ROOT`，则应同步到 `<DIB_CONFIG_ROOT>\\plugins\\<plugin-id>`

### 发布中心相关目录

- 下载缓存：`%LOCALAPPDATA%\\DibClient\\release-cache\\plugins\\<channel>`
- 预安装目录：`%LOCALAPPDATA%\\DibClient\\release-staging\\plugins\\<channel>`
- 正式插件目录：`%LOCALAPPDATA%\\DibClient\\plugins`

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

补充说明：

- `plugin.settings.json` 只放非敏感本地配置
- `plugin.development.json` 仅用于开发模式下的本地敏感资源配置
- 正式发布包不建议包含真实的 `plugin.development.json`

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

## 独立插件发布入口

插件随客户端整包发布时，由 `scripts/publish-release.ps1` 自动刷新仓库根 `plugins/` 中转目录，并把插件复制到客户端发布包内。

插件需要独立发布到发布中心时，使用单插件发布脚本：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\publish-plugin-release.ps1 -PluginId PatientRegistration -Version 0.1.0-dev.1 -Channel stable
```

该脚本会：

1. 构建 `plugins-src/<PluginName>.Plugin/`。
2. 刷新仓库根 `plugins/<PluginName>/` 中转目录。
3. 生成 `artifacts/plugin-releases/<plugin-code>/<version>/publish/`。
4. 生成 `<plugin-code>-<version>.zip`。
5. 写入 `plugin-release-manifest.json`，记录发布中心 Storage 路径、`SHA256` 和文件大小。

完整发布步骤见 `docs/05-operations/PLUGIN_RELEASE_PUBLISH_RUNBOOK.md`。

## 发布前检查清单

发布一个插件包前，至少检查：

1. 插件输出目录存在主程序集
2. 插件输出目录存在 `.deps.json`
3. 关键第三方依赖 DLL 已复制
4. 若有原生依赖，`runtimes/` 目录完整
5. 将输出目录整体打包，而不是只拷主 DLL
6. 手工部署后，检查 `%LOCALAPPDATA%\\DibClient\\logs\\app-*.log`，确认宿主记录了插件初始化成功或失败原因

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

同时应遵守另一条运维规则：

> 判断插件是否真正生效，以运行时插件目录和 `%LOCALAPPDATA%\\DibClient\\logs\\app-*.log` 为准，而不是以仓库目录或编译输出目录为准。
