# DIB 插件发布运行手册

## 目的

本文档用于发布 DIB 独立插件包。它适用于插件单独升级、插件发布中心登记、`plugin-manifest.json` 发布，以及客户端侧下载、预安装、激活验证。

客户端整包发布仍使用 `docs/05-operations/CLIENT_RELEASE_PUBLISH_RUNBOOK.md` 和 `client-release-publish` skill。插件发布使用本文档和 `plugin-release-publish` skill。

## 当前标准入口

在仓库根目录执行：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\publish-plugin-release.ps1 -PluginId PatientRegistration -Version 0.1.0-dev.1 -Channel stable
```

标准输出目录：

```text
artifacts/plugin-releases/patient-registration/0.1.0-dev.1/
├── publish/
├── patient-registration-0.1.0-dev.1.zip
└── plugin-release-manifest.json
```

其中：

- `publish/` 是 zip 包源目录。
- `patient-registration-0.1.0-dev.1.zip` 是发布中心上传文件。
- `plugin-release-manifest.json` 是本次发布的本地核对清单。

## 发布前本地检查

运行脚本后，先检查 `plugin-release-manifest.json`：

```powershell
Get-Content -LiteralPath .\artifacts\plugin-releases\patient-registration\0.1.0-dev.1\plugin-release-manifest.json
```

必须确认：

- `pluginCode` 是 `patient-registration`。
- `version` 是本次版本。
- `channel` 是目标渠道。
- `storagePath` 符合 `plugins/<plugin-code>/<channel>/<version>/<plugin-code>-<version>.zip`。
- `sha256` 非空。
- `sizeBytes` 大于 `0`。

还必须确认 `publish/` 中至少存在：

```text
plugin.json
plugin.settings.json
PatientRegistration.Plugin.dll
PatientRegistration.Plugin.deps.json
QRCoder.dll
Npgsql.dll
```

如果插件有 `runtimes/` 目录，该目录必须随包进入 zip。

## 版本号选择

发布中心生成 `plugin-manifest.json` 时，会在同一插件、同一渠道下选择已发布的最高版本。

因此，若本次版本需要被客户端发现，版本号必须高于该插件当前已发布最高版本。例如当前 `patient-registration` 已有 `1.0.1`，则 `0.1.0-dev.1` 即使标记为已发布，也不会进入 stable manifest；应使用 `1.0.2-dev.1` 或更高版本。

开发期可以使用预发布版本打通闭环，但仍应遵守语义化版本排序规则。

## 发布中心资产登记

打开发布中心：

```powershell
cd .\dib-release-center
npm run dev -- --host 127.0.0.1 --port 4173
```

本地入口：

```text
http://127.0.0.1:4173/release-center/
```

进入 `发布资产` 页面，使用 `上传并登记资产`。

`PatientRegistration` 示例字段：

```text
Bucket: dib-releases
Asset Kind: plugin_package
Storage Path: plugins/patient-registration/stable/0.1.0-dev.1/patient-registration-0.1.0-dev.1.zip
File: artifacts/plugin-releases/patient-registration/0.1.0-dev.1/patient-registration-0.1.0-dev.1.zip
```

上传后必须记录 `release_assets.id`，并核对：

- `asset_kind` 是 `plugin_package`。
- `sha256` 等于本地 `plugin-release-manifest.json` 的 `sha256`。
- `size_bytes` 等于本地 `plugin-release-manifest.json` 的 `sizeBytes`。
- `storage_path` 等于本地 `plugin-release-manifest.json` 的 `storagePath`。

不要把 Storage 上传成功等同于插件发布完成。资产登记只是闭环中的第一段。

## 插件定义

进入 `插件发布` 页面，确认插件定义存在。若不存在，创建：

```text
插件编码：patient-registration
插件名称：就诊登记
入口类型：assembly
作者：DIB
插件说明：就诊登记插件
启用：是
```

插件编码必须与插件包内 `plugin.json.id` 一致。

## 插件版本

在 `插件发布` 页面创建插件版本：

```text
插件定义：patient-registration
发布渠道：stable
资产 ID：上一步 release_assets.id
版本号：0.1.0-dev.1
DIB 最低版本：1.0.0
DIB 最高版本：留空，除非需要限制
manifest JSON：留空或填写兼容说明
发布说明：填写本次插件变化
立即标记为已发布：是
强制升级：按版本策略选择
```

开发期软件尚未正式上线时，可以直接发布 `stable` 的开发版本，便于打通客户端真实发现链路。正式上线后，演练版本应保持草稿，避免误触发客户端更新。

创建后必须核对：

- 版本记录指向本次 `release_assets.id`。
- `is_published` 为预期值。
- 渠道为目标渠道。
- 版本号为本次版本。

## 发布 manifest

当插件版本应该被客户端发现时，发布目标渠道 manifest。

发布后检查公开地址：

```text
<ReleaseCenter.BaseUrl>/storage/v1/object/public/dib-releases/manifests/stable/plugin-manifest.json
```

必须确认：

- HTTP 状态为 `200`。
- `plugins` 中包含 `patient-registration`。
- `version` 是本次版本。
- `packageUrl` 指向本次 zip。
- `sha256` 等于本地 `plugin-release-manifest.json` 的 `sha256`。

manifest 发布完成仍不代表客户端已经完成安装，只代表客户端可以发现该插件版本。

## 客户端验证

客户端侧必须至少验证三段：

1. 下载插件包。
2. 预安装插件包。
3. 激活插件包。

对应运行目录：

```text
%LOCALAPPDATA%\DibClient\release-cache\plugins\<channel>
%LOCALAPPDATA%\DibClient\release-staging\plugins\<channel>
%LOCALAPPDATA%\DibClient\plugins
```

若设置了 `DIB_CONFIG_ROOT`，实际目录以该配置根为准。

验证完成后确认：

- 缓存目录存在本次 zip。
- 预安装目录曾成功解压并识别 `plugin.json`。
- 激活后 `%LOCALAPPDATA%\DibClient\plugins\patient-registration` 存在。
- 宿主能发现并加载“就诊登记”。
- 日志中没有插件依赖缺失或初始化失败。

## 同版本修复流程

如果必须重发同一版本，按修复流程处理，不要只覆盖文件：

1. 重新运行 `scripts/publish-plugin-release.ps1`。
2. 重新核对本地 `sha256` 和 `sizeBytes`。
3. 覆盖同一 Storage 对象。
4. 更新原 `release_assets` 行的 `sha256`、`size_bytes`、`mime_type`。
5. 重新发布目标渠道 `plugin-manifest.json`。
6. 再次验证公开 zip、manifest 和客户端激活链路。

只覆盖 Storage 文件会导致 `release_assets` 和 manifest 继续引用旧哈希。只更新 `release_assets` 不重发 manifest，会导致客户端仍读取旧 manifest。

## 常见错误

### 只上传 DLL

不成立。插件包必须是自包含目录，不能只上传主程序集。

### zip 外层目录不一致

客户端会递归查找 `plugin.json`，因此 zip 根目录直接放插件文件最简单。若增加外层目录，也必须保证外层目录内完整包含插件文件。

### 忘记发布 manifest

版本记录创建成功后，客户端仍不会发现新版本。必须发布 `plugin-manifest.json`。

### 只验证 manifest

manifest 可访问只说明客户端能发现版本，不说明插件能下载、解压、激活或加载。最终完成条件必须包含客户端侧验证。

### 重复上传同一路径

如果页面反馈不明确，先刷新资产列表并按 `storage_path` 查找。不要盲目重复上传同一个对象。

## 最终完成标准

一次插件发布只有在以下条件都满足时才算完成：

- 本地 zip 存在。
- 本地 `sha256` 和 `sizeBytes` 已记录。
- Storage 对象可访问。
- `release_assets` 元数据与本地 zip 一致。
- 插件版本记录指向正确资产。
- `plugin-manifest.json` 指向正确版本和哈希。
- 客户端完成下载、预安装、激活。
- 宿主能发现并加载插件。
