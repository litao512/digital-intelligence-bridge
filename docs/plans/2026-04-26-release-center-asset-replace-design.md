# 发布中心资产覆盖设计

## 背景

发布中心已经具备资产上传、资产登记、资产删除和 manifest 发布能力。插件同版本修复时仍需要覆盖既有 Storage 文件，并同步 `release_assets` 中的 `sha256`、`size_bytes`、`mime_type`。当前只能通过服务器侧手工操作完成，容易遗漏元数据或 manifest 重发。

## 目标

在发布中心 UI 中为已有资产增加“覆盖文件”能力，用于修复测试阶段的同版本客户端包或插件包。

## 行为规则

- 覆盖操作只更新目标 Storage 对象和同一条 `release_assets` 记录。
- 允许覆盖被 `plugin_versions` 或 `client_versions` 引用的资产。
- 覆盖前必须明确确认，确认文案展示文件名、路径、旧哈希和新文件摘要将在上传后更新。
- 覆盖不会自动发布 manifest。
- 覆盖完成后提示用户重新发布当前渠道 manifest。
- 删除能力维持现有保护：被版本引用的资产仍不能删除。

## 数据流

1. 用户在资产列表某一行点击“覆盖文件”。
2. 页面打开该行专属文件选择框。
3. 用户选择本地文件。
4. 前端计算文件 SHA256、大小和 MIME。
5. 用户确认覆盖。
6. 前端以 `upsert: true` 上传到该资产的 `bucket_name` 和 `storage_path`。
7. 前端按资产 `id` 更新 `release_assets` 的 `file_name`、`sha256`、`size_bytes`、`mime_type`。
8. 刷新资产列表，显示新的元数据。

## 错误处理

- 未选择文件时不触发覆盖。
- Storage 上传失败时不更新数据库。
- 数据库更新失败时显示错误；这时需要人工检查 Storage 是否已覆盖。
- 覆盖完成后不隐式发布 manifest，避免用户误以为客户端发现链路已更新。

## 测试策略

- 仓储测试验证 `replaceReleaseAssetFile` 使用 `upsert: true`。
- 仓储测试验证 `updateReleaseAssetMetadata` 通过 `id` 更新目标记录。
- 草稿服务沿用现有上传计划计算逻辑，覆盖操作复用同一套 SHA256 计算。
- 组件测试暂不新增；通过仓储测试、构建和浏览器手工验证覆盖入口。
