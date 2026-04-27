# 2026-04-25 客户端本地发布演练记录

> 状态：历史记录（不作为当前待办）

## 范围

本记录保存 `0.0.0-rehearsal` 客户端包的本地演练证据。

本次只验证本地产包、zip 内容、发布中心前端本地测试和构建。不上传发布中心，不登记客户端版本，不发布 manifest。

## 本地产包

执行：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\publish-release.ps1 -Version 0.0.0-rehearsal
```

结果：

- 产物目录：`artifacts/releases/0.0.0-rehearsal/`
- 发布目录：`artifacts/releases/0.0.0-rehearsal/publish/`
- zip 包：`artifacts/releases/0.0.0-rehearsal/dib-win-x64-portable-0.0.0-rehearsal.zip`
- 产包命令退出码：`0`

产包过程中出现 `NU1900` 警告，原因是当前环境无法访问 `https://api.nuget.org/v3/index.json` 获取 NuGet 漏洞数据。该警告未阻断 restore、build、publish 和 zip 打包。

## 本地包校验

基础文件检查：

| 项目 | 结果 |
|---|---|
| `publish/` | 存在 |
| `publish/digital-intelligence-bridge.exe` | 存在 |
| `publish/appsettings.json` | 存在 |
| `publish/plugins/` | 存在 |
| zip 包 | 存在 |

配置检查：

| 字段 | 值 |
|---|---|
| `Application.Name` | `DIB客户端` |
| `Application.Version` | `0.0.0-rehearsal` |
| `ReleaseCenter.Enabled` | `true` |
| `ReleaseCenter.BaseUrl` | `http://101.42.19.26:8000` |
| `ReleaseCenter.Channel` | `stable` |
| `ReleaseCenter.AnonKey` | 未写入 |
| `Supabase.AnonKey` | 未写入 |

包摘要：

| 项目 | 值 |
|---|---|
| zip 大小 | `75,983,381` bytes |
| zip `SHA256` | `3fc098fdc00deee3479f118441b119aae12f8d3bcc2847c642bbbf150464d099` |

## zip 内容校验

| 项目 | 结果 |
|---|---|
| zip 文件条目数 | `200` |
| zip 文件条目中的文件数 | `184` |
| `publish/` 文件数 | `184` |
| `digital-intelligence-bridge.exe` | 存在 |
| `appsettings.json` | 存在 |
| `plugins/MedicalDrugImport/MedicalDrugImport.Plugin.dll` | 存在 |
| `plugins/MedicalDrugImport/MedicalDrugImport.Plugin.deps.json` | 存在 |
| `plugins/PatientRegistration/PatientRegistration.Plugin.dll` | 存在 |
| `plugins/PatientRegistration/PatientRegistration.Plugin.deps.json` | 存在 |

发布目录文件大小合计为 `176,373,489` bytes。zip 压缩后大小为 `75,983,381` bytes。

## 随包插件校验

| 插件 | 主 DLL | `.deps.json` | 文件数 |
|---|---|---|---|
| `MedicalDrugImport` | 存在 | 存在 | `7` |
| `PatientRegistration` | 存在 | 存在 | `44` |

## 发布中心本地校验

在 `dib-release-center/` 下执行：

```powershell
npm test
npm run build
```

默认沙箱中两条命令都因 `spawn EPERM` 失败。按沙箱权限规则改为沙箱外执行后：

- `npm test`：`9` 个测试文件通过，`40` 个测试通过。
- `npm run build`：`vue-tsc --noEmit && vite build` 通过，生产构建生成 `dist/`。

## 未执行事项

本次未执行：

- 上传客户端 zip 到 Supabase Storage。
- 登记 `release_assets`。
- 创建客户端版本记录。
- 发布 `client-manifest.json`。
- 验证客户端自动更新下载路径。

## 真实发布中心联调补充

### 1. 公开访问检查

真实发布中心页面可访问：

```text
http://101.42.19.26:8000/release-center/
```

公开 Storage manifest 可访问：

| 路径 | 状态 | `Content-Length` |
|---|---:|---:|
| `/storage/v1/object/public/dib-releases/manifests/stable/client-manifest.json` | `200` | `540` |
| `/storage/v1/object/public/dib-releases/manifests/stable/plugin-manifest.json` | `200` | `556` |

### 2. REST 权限检查

使用 `.env.local` 中的 anon key 直接读取 `dib_release.release_channels` 时，服务端返回：

```text
permission denied for table release_channels
```

结论：管理侧 REST 读写不能仅依赖 anon key，需要使用发布中心页面登录 Supabase 管理员账号。后续上传资产、登记版本和发布 manifest 必须走已加入 `dib_release.release_center_admins` 的登录态。

### 3. 旧 rehearsal 路径冲突

检查旧目标路径：

```text
/storage/v1/object/public/dib-releases/clients/stable/0.0.0-rehearsal/dib-win-x64-portable-0.0.0-rehearsal.zip
```

结果：

- 远端已存在。
- 远端 `Content-Length` 为 `86,743,426` bytes。
- 本地 `0.0.0-rehearsal` zip 大小为 `75,983,381` bytes。

结论：不能继续用 `0.0.0-rehearsal` 作为上传目标，否则会进入同版本修复流。

### 4. 新 rehearsal 包

为避免覆盖旧路径，生成新的本地演练版本：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\publish-release.ps1 -Version 0.0.0-rehearsal-20260425 -Channel stable
```

结果：

| 项目 | 值 |
|---|---|
| 发布目录 | `artifacts/releases/0.0.0-rehearsal-20260425/publish/` |
| zip 包 | `artifacts/releases/0.0.0-rehearsal-20260425/dib-win-x64-portable-0.0.0-rehearsal-20260425.zip` |
| `Application.Version` | `0.0.0-rehearsal-20260425` |
| `ReleaseCenter.Enabled` | `true` |
| `ReleaseCenter.Channel` | `stable` |
| zip 大小 | `75,983,468` bytes |
| zip `SHA256` | `f5f77157495fca72e6d5936c2d9c4f8ffe05e3ed35505b42b4c9514ac71f4d89` |

远端新目标路径当前未发现同名公开对象：

```text
/storage/v1/object/public/dib-releases/clients/stable/0.0.0-rehearsal-20260425/dib-win-x64-portable-0.0.0-rehearsal-20260425.zip
```

### 5. 本地发布中心 UI

本地发布中心已启动：

```text
http://127.0.0.1:4173/release-center/
```

管理员已通过本地发布中心页面完成以下操作：

1. 进入 `发布资产`。
2. 使用 `上传并登记资产` 上传新 rehearsal zip。
3. 填写：
   - `Bucket`: `dib-releases`
   - `Asset Kind`: `client_package`
   - `Storage Path`: `clients/stable/0.0.0-rehearsal-20260425/dib-win-x64-portable-0.0.0-rehearsal-20260425.zip`
4. 上传后核对 `release_assets` 中的 `sha256` 和 `size_bytes` 与本记录一致。
5. 进入 `客户端版本` 创建草稿版本记录，不发布 manifest。

登记结果：

| 项目 | 值 |
|---|---|
| `release_assets.id` | `8a6a1099-6475-4af9-adf1-4b3589abf79b` |
| `asset_kind` | `client_package` |
| `storage_path` | `clients/stable/0.0.0-rehearsal-20260425/dib-win-x64-portable-0.0.0-rehearsal-20260425.zip` |
| `sha256` | `f5f77157495fca72e6d5936c2d9c4f8ffe05e3ed35505b42b4c9514ac71f4d89` |
| `size_bytes` | `75,983,468` |

公开对象检查：

| 项目 | 值 |
|---|---|
| HTTP 状态 | `200` |
| `Content-Length` | `75,983,468` |

客户端版本记录：

| 项目 | 值 |
|---|---|
| `version` | `0.0.0-rehearsal-20260425` |
| `channel_code` | `stable` |
| `is_published` | `false` |
| `is_mandatory` | `false` |
| `asset_id` | `8a6a1099-6475-4af9-adf1-4b3589abf79b` |

stable 客户端 manifest 复核：

| 项目 | 值 |
|---|---|
| `latestVersion` | `1.0.3` |
| `packageUrl` | `http://101.42.19.26:8000/storage/v1/object/public/dib-releases/clients/stable/1.0.3/dib-win-x64-portable-1.0.3.zip` |
| `sha256` | `807d5c59915d907c5eda49f0497b54de6ce4fb7c075c27c9ce5c839dfc61376b` |

结论：本次 rehearsal 已完成资产上传、元数据登记和客户端版本草稿创建；未发布 manifest，stable 客户端 manifest 仍保持 `1.0.3`。

## 结论

`0.0.0-rehearsal` 本地产包与本地发布中心校验通过，可作为后续正式发布前的本地证据基线。

真实发布中心联调已确认公开访问正常，`0.0.0-rehearsal-20260425` 已作为草稿版本登记，不影响 stable 客户端更新发现。
