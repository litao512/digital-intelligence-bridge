# 插件发布闭环设计

## 背景

DIB 目前仍处于开发阶段，客户端与插件都没有正式上线历史负担。因此插件发布流程可以直接以“完整闭环”为目标：本地构建插件包，上传到发布中心，登记插件版本，发布 `plugin-manifest.json`，再由客户端完成下载、预安装和激活验证。

客户端发布已经有 `client-release-publish` skill 固化了本地产包、发布中心登记、manifest 发布和验证的做法。插件发布需要采用同一套发布纪律，但不能复用客户端整包脚本作为插件独立发布入口，因为插件未来应支持单独版本节奏。

## 目标

以 `PatientRegistration` 为样例，建立可复用的插件发布闭环：

1. 本地生成独立插件 zip 包。
2. 校验插件包是自包含运行单元。
3. 上传并登记为 `plugin_package` 资产。
4. 创建或复用插件定义。
5. 创建插件版本记录并发布。
6. 发布 `plugin-manifest.json`。
7. 通过客户端发布中心能力验证下载、预安装、激活。
8. 将流程沉淀为运行手册与个人 skill。

## 范围

本次只覆盖插件发布流程闭环，不改造插件业务功能。

纳入范围：

- 新增单插件本地发布脚本。
- 新增插件发布运行手册。
- 新增插件发布个人 skill。
- 以 `PatientRegistration` 插件为例完成流程说明与验证清单。

不纳入范围：

- 插件市场 UI 改造。
- 自动化登录发布中心。
- 服务端数据库结构变更。
- 客户端热加载机制改造。
- 插件灰度策略或站点授权策略重构。

## 架构

插件发布采用“本地包 + 发布中心元数据 + manifest + 客户端激活”的四段式流程。

本地脚本负责从 `plugins-src/<PluginName>.Plugin/` 构建 Release 输出，刷新仓库根 `plugins/<PluginName>/` 中转目录，生成独立 zip 包和发布清单。发布中心负责 Storage 对象、`release_assets`、`plugin_packages`、`plugin_versions` 和 `plugin-manifest.json`。客户端只消费公开 manifest 和插件 zip 包，并按现有 `ReleaseCenterService` 执行下载、预安装和激活。

## 插件包格式

独立插件 zip 包内应直接包含插件运行目录内容，不要求外层固定目录名。

示例：

```text
patient-registration-0.1.0-dev.1.zip
├── plugin.json
├── plugin.settings.json
├── PatientRegistration.Plugin.dll
├── PatientRegistration.Plugin.deps.json
├── QRCoder.dll
├── Npgsql.dll
└── runtimes/
```

客户端当前预安装逻辑会将 zip 解压到缓存暂存目录，再递归查找 `plugin.json`，随后把 `plugin.json` 所在目录激活到运行时插件目录。因此 zip 根目录直放插件文件最简单，也避免多套目录命名约定。

## 本地产物目录

新增脚本建议为：

```powershell
.\scripts\publish-plugin-release.ps1 -PluginId PatientRegistration -Version 0.1.0-dev.1 -Channel stable
```

标准输出目录：

```text
artifacts/plugin-releases/patient-registration/0.1.0-dev.1/
├── publish/
├── patient-registration-0.1.0-dev.1.zip
└── plugin-release-manifest.json
```

`publish/` 是 zip 的源目录，必须来自 Release 构建输出，并包含 `plugin.json` 与全部运行依赖。

## 发布中心数据流

插件发布中心侧沿用现有模型：

1. 在 `发布资产` 上传 zip。
2. 资产类型使用 `plugin_package`。
3. Storage 路径使用：

```text
plugins/<plugin-code>/<channel>/<version>/<plugin-code>-<version>.zip
```

`PatientRegistration` 示例：

```text
plugins/patient-registration/stable/0.1.0-dev.1/patient-registration-0.1.0-dev.1.zip
```

4. 在 `插件发布` 页面确认插件定义存在：

```text
插件编码：patient-registration
插件名称：就诊登记
入口类型：assembly
启用：是
```

5. 创建插件版本：

```text
插件定义：patient-registration
发布渠道：stable
资产 ID：上传后生成的 release_assets.id
版本号：0.1.0-dev.1
DIB 最低版本：1.0.0
立即标记为已发布：是
强制升级：按版本策略选择
```

6. 发布 `plugin-manifest.json`。

## 校验策略

发布必须拆成三类校验。

本地校验：

- `plugin.json` 存在。
- `plugin.settings.json` 存在。
- `PatientRegistration.Plugin.dll` 存在。
- `PatientRegistration.Plugin.deps.json` 存在。
- `QRCoder.dll` 存在。
- `Npgsql.dll` 存在。
- 若存在 `runtimes/`，目录随包进入 zip。
- zip 的 `SHA256` 和字节数已记录。

发布中心校验：

- Storage 对象上传成功。
- `release_assets.asset_kind == plugin_package`。
- `release_assets.sha256` 与本地 zip 一致。
- `release_assets.size_bytes` 与本地 zip 一致。
- `plugin_versions.asset_id` 指向该资产。
- `plugin_versions.is_published == true`。

manifest 校验：

- 公开 `manifests/<channel>/plugin-manifest.json` 可访问。
- manifest 包含 `patient-registration`。
- `version` 是本次版本。
- `packageUrl` 指向本次 zip。
- `sha256` 与本地 zip 一致。

客户端校验：

- 客户端能读取插件 manifest。
- 客户端能下载插件包到 release cache。
- 客户端能预安装插件包到 staging。
- 客户端能激活插件到运行时插件目录。
- 重启或刷新插件后，宿主能发现并加载 `patient-registration`。

## 错误处理

如果上传成功但页面反馈不明确，应刷新资产列表并按 `storage_path` 查找，不应盲目重复上传。

如果同一版本需要重发，按修复流程处理：

1. 重新构建本地插件包。
2. 覆盖同一 Storage 对象。
3. 更新原 `release_assets` 的 `sha256`、`size_bytes`、`mime_type`。
4. 重新发布 `plugin-manifest.json`。
5. 重新验证公开 zip 与 manifest。

如果客户端预安装失败，优先检查 zip 内是否存在 `plugin.json` 和插件依赖 DLL，而不是修改客户端兜底逻辑。

## 与客户端发布 skill 的关系

`client-release-publish` 负责客户端整包发布，产物是 `dib-win-x64-portable-<version>.zip`，版本记录写入 `client_versions`，最终发布 `client-manifest.json`。

插件发布 skill 负责单插件独立发布，产物是 `<plugin-code>-<version>.zip`，版本记录写入 `plugin_versions`，最终发布 `plugin-manifest.json`。

两者共享以下原则：

- 发布前先本地构建并校验。
- 上传后核对 Storage 与 `release_assets`。
- manifest 只在版本应被客户端发现时发布。
- 最终必须验证公开 URL、哈希和客户端消费路径。

两者不冲突。未来发布客户端新版本时继续使用 `client-release-publish`；发布插件新版本时使用插件发布 skill。客户端整包仍可随包包含当前插件快照，但插件独立更新以 `plugin-manifest.json` 为准。
