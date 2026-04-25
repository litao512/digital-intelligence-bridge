# 插件发布闭环验证记录

## 目的

记录 `PatientRegistration` 插件独立发布闭环的本地打包、发布中心登记、manifest 发布和客户端侧验证状态。

## 本地打包

最终用于 manifest 闭环的版本：

| 项目 | 值 |
| --- | --- |
| 插件 | `PatientRegistration` |
| 插件编码 | `patient-registration` |
| 渠道 | `stable` |
| 版本 | `1.0.2-dev.1` |
| 本地目录 | `artifacts/plugin-releases/patient-registration/1.0.2-dev.1/` |
| zip | `patient-registration-1.0.2-dev.1.zip` |
| `SHA256` | `03d6c5cc5d3d2b4e236edfdb46e23a5ccea96edda3939323247236b8072a4bf0` |
| `sizeBytes` | `59335628` |
| Storage 路径 | `plugins/patient-registration/stable/1.0.2-dev.1/patient-registration-1.0.2-dev.1.zip` |

本地脚本：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\publish-plugin-release.ps1 -PluginId PatientRegistration -Version 1.0.2-dev.1 -Channel stable
```

脚本结果：

- `publish/` 已生成。
- zip 已生成。
- `plugin-release-manifest.json` 已生成。
- 仓库根 `plugins/PatientRegistration/` 已刷新。
- 构建出现 `NU1900` 警告，原因是 NuGet 漏洞数据源不可达；构建本身成功。

## 本地验证

已执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-publish-plugin-release.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\check-doc-lang.ps1
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal
```

结果：

- 插件发布脚本测试通过。
- 文档语言检查通过。
- 主项目 Debug 构建通过。
- 测试项目构建通过。
- 单元测试通过：`221/221`。

## 发布中心资产

最终用于 manifest 闭环的资产：

| 项目 | 值 |
| --- | --- |
| `release_assets.id` | `0b2a3c53-f74c-4b8a-bea3-db233ccba8f2` |
| `asset_kind` | `plugin_package` |
| `bucket_name` | `dib-releases` |
| `storage_path` | `plugins/patient-registration/stable/1.0.2-dev.1/patient-registration-1.0.2-dev.1.zip` |
| `sha256` | `03d6c5cc5d3d2b4e236edfdb46e23a5ccea96edda3939323247236b8072a4bf0` |
| `size_bytes` | `59335628` |
| `mime_type` | `application/x-zip-compressed` |

发布中心页面显示资产上传成功，并完成元数据登记。

## 插件版本

创建的最终插件版本：

| 项目 | 值 |
| --- | --- |
| 插件 | `patient-registration` |
| 版本 | `1.0.2-dev.1` |
| 渠道 | `stable` |
| DIB 范围 | `1.0.0 - 9999.9999.9999` |
| 发布状态 | 已发布 |
| 强制升级 | 否 |

发布中心插件版本列表显示该版本已发布。

## manifest 发布

已通过 `Manifest Workspace` 发布 stable 渠道 manifest。

发布中心状态：

```text
stable 渠道 manifest 已发布到 Storage，并同步写入 release_assets。
```

浏览器页面上下文公开访问验证：

| 对象 | HTTP 状态 | 长度 | 类型 |
| --- | --- | --- | --- |
| `manifests/stable/plugin-manifest.json` | `200` | `583` | `application/json` |
| `plugins/patient-registration/stable/1.0.2-dev.1/patient-registration-1.0.2-dev.1.zip` | `200` | `59335628` | `application/x-zip-compressed` |

manifest 内容确认：

- `pluginId` 是 `patient-registration`。
- `version` 是 `1.0.2-dev.1`。
- `packageUrl` 指向本次 zip。
- `sha256` 是 `03d6c5cc5d3d2b4e236edfdb46e23a5ccea96edda3939323247236b8072a4bf0`。

## 版本排序发现

最初创建过 `0.1.0-dev.1` 的已发布插件版本：

| 项目 | 值 |
| --- | --- |
| `release_assets.id` | `ac33fa12-2891-4bd3-b395-f99fc95c0310` |
| 版本 | `0.1.0-dev.1` |
| 状态 | 已发布 |
| 结果 | 未进入 stable manifest |

原因：发布中心按语义化版本选择同插件最高版本，`0.1.0-dev.1` 低于既有 `1.0.1`，因此不会被 manifest 选中。

处理：改用 `1.0.2-dev.1` 完成 manifest 闭环，并已将该规则补充到插件发布运行手册和插件发布 skill。

## 客户端侧验证状态

已完成：

- 发布中心 manifest 写入成功。
- 浏览器上下文可公开访问 manifest。
- 浏览器上下文可公开访问插件 zip。
- zip 的公开 `Content-Length` 与本地 `sizeBytes` 一致。

未完成：

- 当前机器 PowerShell 访问 `http://101.42.19.26:8000` 被拒绝连接，因此未能用本机 .NET 客户端链路直接验证下载、预安装和激活。

具体表现：

```text
由于目标计算机积极拒绝，无法连接。
```

风险判断：

- 发布中心与浏览器公开访问链路已通。
- 本机非浏览器网络访问存在环境差异，可能影响 .NET 客户端直接下载。
- 真正上线前仍需在目标客户端运行环境执行“下载插件包 -> 预安装 -> 激活 -> 宿主发现插件”的完整客户端侧回归。

## 结论

插件发布中心侧闭环已打通：

1. 本地独立插件打包成功。
2. `plugin_package` 资产上传成功。
3. 插件版本记录创建并发布。
4. `plugin-manifest.json` 发布成功。
5. 公开 manifest 与 zip 通过浏览器访问验证。

客户端本机 .NET 下载、预安装和激活链路尚未在当前环境验证完成，原因是当前机器非浏览器访问发布中心公开端口被拒绝连接。
