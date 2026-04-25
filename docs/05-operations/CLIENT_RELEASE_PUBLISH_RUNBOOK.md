# 客户端发布上线操作手册

## 1. 适用范围

本文档用于当前仓库下 DIB 客户端（desktop client）的标准发布操作。

当前正式流程分两段：

1. 在主仓库本地产包
2. 在 `dib-release-center` 中上传资产、登记版本，并按需发布 manifest

当前不再把 `.tmp/release` 作为正式发布目录。历史 `.tmp/release` 仅视为旧流程遗留痕迹。

## 2. 当前正式产物目录

本地发布脚本统一输出到：

```text
artifacts/releases/<version>/
```

目录内应至少包含：

- `publish/`
- `dib-win-x64-portable-<version>.zip`
- `release-manifest.json`

## 3. 本地产包

在仓库根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Version <version>
```

示例：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Version 1.0.3
```

### 3.1 脚本行为

当前脚本会串行完成：

1. `restore` 主程序
2. `restore/build` `plugins-src/*.Plugin`
3. 清理插件输出目录，避免旧子目录或旧依赖残留
4. 同步插件到仓库根 `plugins/` 本地中转目录
5. `publish` 主程序
6. 复制 `appsettings.json`
7. 复制本地中转目录 `plugins/`
8. 生成 zip

这意味着：

- 只要使用该脚本发布，随包插件会先刷新为最新构建结果
- 不需要再手工把 `plugins-src` 复制到仓库根 `plugins`
- 仓库根 `plugins/` 是本地产包中转目录，已被 `.gitignore` 忽略，不作为源码提交内容

### 3.2 发布脚本防护规则

执行本地产包时必须遵守：

1. 使用 `powershell -NoProfile` 启动脚本，避免本机 PowerShell Profile 干扰发布流程。
2. 插件构建前必须清理 `plugins-src/<Plugin>.Plugin/bin/<Configuration>/net10.0`。
3. 脚本读写 `appsettings.json`、`plugin.settings.json`、`release-manifest.json` 必须显式使用 UTF-8。
4. 发布后检查 `publish/plugins/<Plugin>/`，不应包含历史残留的旧 RID 子目录或旧依赖。
5. `artifacts/releases/` 和仓库根 `plugins/` 都是本地生成目录，不提交到 Git。

### 3.3 本地校验

上传前至少确认：

- `publish/digital-intelligence-bridge.exe` 存在
- `publish/appsettings.json` 存在
- `publish/plugins/` 存在
- zip 文件存在

还必须校验 `publish/appsettings.json` 中的关键发布字段：

- `Application.Version == <version>`
- `ReleaseCenter.Enabled == true`
- `ReleaseCenter.Channel == <channel>`

建议在上传前同时记录：

- zip 文件 `SHA256`
- zip 文件大小（bytes）

如果包内版本号、更新开关或渠道不正确，禁止继续上传。此类问题必须在本地产物阶段拦住，不要依赖发布中心侧兜底。

## 4. 启动发布中心

进入：

```text
dib-release-center/
```

首次安装依赖：

```powershell
npm install
```

本地启动：

```powershell
npm run dev -- --host 127.0.0.1 --port 4173
```

本地入口：

```text
http://127.0.0.1:4173/release-center/
```

### 4.1 必需环境变量

`dib-release-center` 依赖：

- `VITE_SUPABASE_URL`
- `VITE_SUPABASE_ANON_KEY`

建议写入：

```text
dib-release-center/.env.local
```

## 5. 登录要求

必须使用已加入 `dib_release.release_center_admins` 的 Supabase 账号登录。

如果账号能登录 Supabase Auth，但未加入 `release_center_admins`，则不能执行发布操作。

## 6. 上传并登记客户端资产

进入页面：

```text
发布资产
```

推荐使用：

- `上传并登记资产`

### 6.1 字段建议

- `Bucket`: `dib-releases`
- `Asset Kind`: `client_package`
- `Storage Path`: `clients/<channel>/<version>/dib-win-x64-portable-<version>.zip`

示例：

```text
clients/stable/1.0.3/dib-win-x64-portable-1.0.3.zip
```

### 6.2 当前已验证的演练样例

这次已成功登记过一条演练资产：

- `storage_path`: `clients/stable/0.0.0-rehearsal/dib-win-x64-portable-0.0.0-rehearsal.zip`
- `asset_id`: `5a1e8fcf-3ccc-465b-8521-e3dcb196dd6b`

### 6.3 大文件上传注意事项

当前页面在上传大体积客户端 zip 时，反馈不够及时。

已观察到的现象：

1. 页面按钮点击后可能长时间没有明显成功提示
2. 实际上 Storage 对象和 `release_assets` 记录已经写入成功
3. 刷新页面后，`发布资产` 数量会变化

因此遇到反馈不明确时，应按这个顺序判断：

1. 刷新页面
2. 检查 `发布资产` 数量是否增加
3. 检查目标 `storage_path` 是否已经存在
4. 若对象已存在，不要盲目重复上传

### 6.4 上传完成后的强制核对

客户端资产上传完成后，不要只看“文件传上去了”。必须拆成三段核对：

1. Storage 文件核对

- 目标对象可访问
- `Content-Length` 与本地 zip 大小一致
- `Content-Type` 合理

2. `release_assets` 元数据核对

- `storage_path` 正确
- `sha256` 与本地 zip 一致
- `size_bytes` 与本地 zip 一致
- `mime_type` 合理

3. 客户端版本记录核对

- `asset_id` 指向刚上传的资产
- `version` 正确
- `min_upgrade_version` 正确
- `is_published` / `is_mandatory` 符合预期

只要三段中任意一段不一致，就不能进入“发布完成”状态。

## 7. 录入客户端版本

进入页面：

```text
客户端版本
```

填写：

- 发布渠道
- 资产 ID
- 版本号
- 最低升级版本
- 发布说明
- 是否立即标记为已发布
- 是否强制升级

## 8. 演练版本规则

演练或 dry-run 建议使用：

```text
0.0.0-rehearsal
```

并遵循：

1. 录入为 `草稿`
2. 不勾选 `立即标记为已发布`
3. 不发布当前渠道 manifest

这样可以验证整条链路，又不会污染正式 `stable` 客户端清单。

## 9. 正式版本规则

正式版本上线前建议依次确认：

1. 本地发布产物完整
2. 资产已成功上传并登记
3. 版本记录已创建
4. 版本号、升级门槛、发布说明正确
5. 确认这次版本应成为客户端可见的最新版本

确认后再执行：

1. 将该版本标记为已发布
2. 发布当前渠道 manifest

## 10. Manifest 发布边界

只有当某个版本应真正影响客户端更新发现时，才发布对应渠道 manifest。

不要对 rehearsal 或演练记录发布真实渠道 manifest。

发布后应验证：

- `client-manifest.json` 可公开访问
- `latestVersion` 正确
- `packageUrl` 指向刚上传的客户端包
- `sha256` 与客户端包一致
- manifest 中引用的包、哈希、版本记录彼此一致

## 11. 发布后最终核对清单

完成发布后，至少逐项确认：

1. 本地产物正确

- `publish/appsettings.json` 中版本号正确
- `ReleaseCenter.Enabled == true`
- `ReleaseCenter.Channel` 正确

2. 公开客户端包正确

- URL 可访问
- `Content-Length` 与本地 zip 一致

3. 发布资产正确

- `release_assets.sha256` 与本地 zip 一致
- `release_assets.size_bytes` 与本地 zip 一致
- `release_assets.mime_type` 正确

4. 客户端版本记录正确

- 版本号正确
- 渠道正确
- 最低升级版本正确
- 关联资产正确

5. 公开 manifest 正确

- `latestVersion` 正确
- `packageUrl` 正确
- `sha256` 正确
- `publishedAt` 符合本次发布时间

只有上述 5 组检查全部通过，才应对外宣告“客户端发布完成”。

## 12. 同版本重发修复流程

当已经发布出去的同一语义化版本存在问题，但又决定保留原版本号重发时，按“修复发布”处理，不按普通新版本发布处理。

标准步骤：

1. 重新本地产包，并重新校验：

- `publish/appsettings.json`
- zip `SHA256`
- zip 文件大小

2. 覆盖 Storage 中原有对象：

- 路径保持不变
- 覆盖后重新核对公开 URL 的 `Content-Length`

3. 修正原有 `release_assets` 元数据：

- `sha256`
- `size_bytes`
- `mime_type`

4. 重新发布当前渠道 `client-manifest.json`

5. 再做一次最终核对清单

注意：

- 仅覆盖 Storage 文件而不修正 `release_assets`，会导致版本记录与 manifest 继续引用旧元数据
- 仅修正 `release_assets` 而不重发 manifest，客户端仍可能读取到旧哈希

## 13. 异常修复建议

### 13.1 页面已登录时，优先复用发布中心页面上下文

当需要修复发布中心中的元数据或重发 manifest 时，优先使用 `dib-release-center` 页面当前登录态下的 Supabase client / repository 逻辑。

原因：

- 页面上下文已经具备可用鉴权
- 直接复用仓储与服务方法，更不容易踩 schema/profile 细节
- 比手工拼接 REST/JWT 更稳

### 13.2 Storage 成功不代表发布成功

如果看到客户端包已经能从 Storage 下载，不代表发布已经完成。

至少还要继续确认：

- `release_assets` 已同步到新元数据
- 客户端版本记录已指向正确资产
- `client-manifest.json` 已刷新到新哈希

### 13.3 客户端侧验证要单独记录

发布中心侧验证通过，只代表“发布链路可用”。

客户端自动更新是否真实可用，还需要单独记录客户端侧回归，例如：

- 检查更新是否能发现新版本
- 下载更新包是否成功
- 停止下载是否生效
- 取消后重新下载是否成功
- 如支持自动应用升级，还应验证升级动作

## 14. 已验证回归样例

### 14.1 `stable/1.0.3` 客户端自动更新回归

本仓库已在真实发布中心配置下完成一轮客户端侧模拟回归，验证基线如下：

- 起始客户端版本：`1.0.0`
- 目标渠道：`stable`
- 目标版本：`1.0.3`
- 目标包大小：`86744491`
- 目标包 `SHA256`：`807d5c59915d907c5eda49f0497b54de6ce4fb7c075c27c9ce5c839dfc61376b`

本轮已确认：

1. 检查更新成功

- 客户端可发现 `1.0.3`
- 返回信息包含最低升级版本 `1.0.0`

2. 停止下载成功

- 下载开始后可触发取消
- 取消结果为“`客户端下载已取消`”
- 取消后客户端缓存目录不保留半截 zip 文件

3. 取消后重新下载成功

- 再次发起下载可成功完成
- 下载完成后缓存文件大小与线上一致
- 下载完成后文件 `SHA256` 与 manifest 一致

如后续回归结果与本样例不一致，应优先排查：

- 目标客户端包是否已被重新覆盖
- `release_assets` 是否已同步到最新 `sha256`
- `client-manifest.json` 是否已重新发布
- 客户端是否仍在使用旧缓存目录或旧用户配置

## 15. 当前结论

本仓库当前已验证可用的客户端发布链路为：

1. `scripts/publish-release.ps1`
2. `dib-release-center` 登录
3. `发布资产` 上传并登记客户端包
4. `客户端版本` 录入版本记录
5. 发布当前渠道 `client-manifest.json`
6. 按“发布后最终核对清单”完成校验

其中本次已补充沉淀的经验包括：

- 产包阶段必须校验包内版本与更新配置
- 资产发布必须拆成 Storage / `release_assets` / manifest 三段验证
- 同版本重发必须同步覆盖 Storage、修正元数据并重发 manifest
- 客户端侧自动更新验证应与发布中心侧验证分开记录
