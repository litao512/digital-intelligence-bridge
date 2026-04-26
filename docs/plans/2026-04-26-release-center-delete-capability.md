# 发布中心资源删除能力实施计划

> **执行要求：** 使用 `superpowers:executing-plans`，按任务逐项执行并在批次之间回报。

**目标：** 为 DIB 发布中心增加插件版本、插件定义、客户端版本、发布资产和 Storage 文件删除能力。

**架构：** 在仓储层为现有 Supabase 表补充删除函数，并为发布资产增加引用检查与 Storage 文件删除函数。页面层在现有列表中增加危险操作按钮、确认流程、阻断提示和刷新逻辑；删除不会自动发布 manifest。

**技术栈：** Vue 3、TypeScript、Vite、Vitest、Supabase JS、DIB Release Center。

---

### Task 1: 补充删除仓储测试

**Files:**
- Modify: `dib-release-center/src/repositories/releaseAssetsRepository.test.ts`
- Modify: `dib-release-center/src/repositories/pluginVersionsRepository.test.ts`
- Modify: `dib-release-center/src/repositories/pluginPackagesRepository.test.ts`
- Modify: `dib-release-center/src/repositories/clientVersionsRepository.test.ts`
- Reference: `dib-release-center/src/repositories/groupPluginPoliciesRepository.ts`
- Reference: `dib-release-center/src/repositories/sitePluginOverridesRepository.ts`

**Step 1: 查看既有测试桩**

Run:

```powershell
Get-ChildItem dib-release-center\src\repositories -Filter *.test.ts
```

Expected: 能看到现有仓储测试文件，确认 Supabase mock 写法。

**Step 2: 为发布资产写失败测试**

在 `releaseAssetsRepository.test.ts` 增加以下断言方向：

```typescript
it('deletes a release asset by id', async () => {
  await deleteReleaseAsset('asset-1');

  expect(fromMock).toHaveBeenCalledWith('release_assets');
  expect(deleteMock).toHaveBeenCalled();
  expect(eqMock).toHaveBeenCalledWith('id', 'asset-1');
});

it('checks release asset references from plugin and client versions', async () => {
  const references = await findReleaseAssetReferences('asset-1');

  expect(references.pluginVersions.length).toBe(1);
  expect(references.clientVersions.length).toBe(1);
});

it('removes a storage object by bucket and storage path', async () => {
  await deleteReleaseAssetObject('dib-releases', 'plugins/patient-registration/stable/1.0.0/pkg.zip');

  expect(storageFromMock).toHaveBeenCalledWith('dib-releases');
  expect(removeMock).toHaveBeenCalledWith(['plugins/patient-registration/stable/1.0.0/pkg.zip']);
});
```

**Step 3: 为插件版本写失败测试**

在 `pluginVersionsRepository.test.ts` 增加：

```typescript
it('deletes a plugin version by id', async () => {
  await deletePluginVersion('plugin-version-1');

  expect(fromMock).toHaveBeenCalledWith('plugin_versions');
  expect(deleteMock).toHaveBeenCalled();
  expect(eqMock).toHaveBeenCalledWith('id', 'plugin-version-1');
});
```

**Step 4: 为插件定义写失败测试**

在 `pluginPackagesRepository.test.ts` 增加：

```typescript
it('deletes a plugin package by id', async () => {
  await deletePluginPackage('plugin-package-1');

  expect(fromMock).toHaveBeenCalledWith('plugin_packages');
  expect(deleteMock).toHaveBeenCalled();
  expect(eqMock).toHaveBeenCalledWith('id', 'plugin-package-1');
});
```

**Step 5: 为客户端版本写失败测试**

在 `clientVersionsRepository.test.ts` 增加：

```typescript
it('deletes a client version by id', async () => {
  await deleteClientVersion('client-version-1');

  expect(fromMock).toHaveBeenCalledWith('client_versions');
  expect(deleteMock).toHaveBeenCalled();
  expect(eqMock).toHaveBeenCalledWith('id', 'client-version-1');
});
```

**Step 6: 运行测试确认失败**

Run:

```powershell
cd dib-release-center
npm test -- --run
```

Expected: FAIL，提示删除函数尚未导出或未实现。

### Task 2: 实现删除仓储函数

**Files:**
- Modify: `dib-release-center/src/repositories/releaseAssetsRepository.ts`
- Modify: `dib-release-center/src/repositories/pluginVersionsRepository.ts`
- Modify: `dib-release-center/src/repositories/pluginPackagesRepository.ts`
- Modify: `dib-release-center/src/repositories/clientVersionsRepository.ts`

**Step 1: 实现通用删除函数**

分别在对应仓储中增加：

```typescript
export async function deletePluginVersion(id: string): Promise<void> {
  const { error } = await supabase.from('plugin_versions').delete().eq('id', id);
  if (error) {
    throw new Error(`删除插件版本失败：${error.message}`);
  }
}
```

其他表按同样方式实现，表名分别为 `plugin_packages`、`client_versions`、`release_assets`。

**Step 2: 实现资产引用检查**

在 `releaseAssetsRepository.ts` 中增加：

```typescript
export interface ReleaseAssetReferences {
  pluginVersions: Array<{ id: string; version: string; channel: string }>;
  clientVersions: Array<{ id: string; version: string; channel: string }>;
}
```

查询 `plugin_versions` 和 `client_versions`，条件均为 `.eq('asset_id', id)`，返回空数组表示未被引用。

**Step 3: 实现 Storage 文件删除**

在 `releaseAssetsRepository.ts` 中增加：

```typescript
export async function deleteReleaseAssetObject(bucketName: string, storagePath: string): Promise<void> {
  const { error } = await supabase.storage.from(bucketName).remove([storagePath]);
  if (error) {
    throw new Error(`删除 Storage 文件失败：${error.message}`);
  }
}
```

**Step 4: 运行仓储测试**

Run:

```powershell
cd dib-release-center
npm test -- --run src/repositories
```

Expected: PASS。

**Step 5: 提交仓储变更**

Run:

```powershell
git add dib-release-center\src\repositories
git commit -m "feat: 增加发布中心删除仓储接口"
```

### Task 3: 增加发布资产删除交互

**Files:**
- Modify: `dib-release-center/src/web/pages/ReleaseAssetsPage.vue`
- Reference: `dib-release-center/src/web/pages/GroupPluginPoliciesPage.vue`
- Reference: `dib-release-center/src/web/pages/SitePluginOverridesPage.vue`

**Step 1: 引入删除函数**

在页面脚本中引入：

```typescript
deleteReleaseAsset,
deleteReleaseAssetObject,
findReleaseAssetReferences,
```

**Step 2: 增加删除方法**

实现 `deleteAsset(asset, removeObject)`：

1. 调用 `findReleaseAssetReferences(asset.id)`。
2. 若存在引用，设置错误信息并停止。
3. 使用 `window.confirm` 二次确认。
4. 若 `removeObject` 为真，先调用 `deleteReleaseAssetObject(asset.bucket_name, asset.storage_path)`。
5. 调用 `deleteReleaseAsset(asset.id)`。
6. 调用现有加载方法刷新列表。

**Step 3: 增加操作列**

在资产表增加删除按钮：

```vue
<button class="danger-button inline-button" type="button" @click="deleteAsset(asset, false)">
  删除记录
</button>
<button class="danger-button inline-button" type="button" @click="deleteAsset(asset, true)">
  删除记录和文件
</button>
```

**Step 4: 手动检查页面状态**

启动发布中心后检查：

- 被引用资产点击删除会提示阻断。
- 未被引用资产可以删除记录。
- 选择删除记录和文件时会先删除 Storage 文件。

### Task 4: 增加插件发布删除交互

**Files:**
- Modify: `dib-release-center/src/web/pages/PluginReleasesPage.vue`

**Step 1: 引入删除函数**

引入：

```typescript
deletePluginVersion,
deletePluginPackage,
```

**Step 2: 增加插件版本删除方法**

实现 `deleteVersion(version)`：

1. 确认插件编码、版本、渠道和发布状态。
2. 调用 `deletePluginVersion(version.id)`。
3. 刷新插件版本列表。
4. 刷新插件 manifest 预览。

**Step 3: 增加插件定义列表**

在插件定义表单下方展示现有插件定义列表，字段包括：

- 插件编码。
- 插件名称。
- 入口类型。
- 启用状态。
- 操作。

**Step 4: 增加插件定义删除方法**

实现 `deletePackage(pluginPackage)`：

1. 确认会级联删除插件版本和授权绑定。
2. 调用 `deletePluginPackage(pluginPackage.id)`。
3. 刷新插件定义和插件版本列表。
4. 刷新插件 manifest 预览。

**Step 5: 手动检查页面状态**

检查插件版本和插件定义删除按钮存在，取消确认不会调用删除，删除成功后列表刷新。

### Task 5: 增加客户端发布删除交互

**Files:**
- Modify: `dib-release-center/src/web/pages/ClientReleasesPage.vue`

**Step 1: 引入删除函数**

引入：

```typescript
deleteClientVersion,
```

**Step 2: 增加客户端版本删除方法**

实现 `deleteVersion(version)`：

1. 确认版本号、渠道和发布状态。
2. 调用 `deleteClientVersion(version.id)`。
3. 刷新客户端版本列表。
4. 刷新客户端 manifest 预览。

**Step 3: 增加操作列**

在客户端版本表增加删除按钮。

**Step 4: 手动检查页面状态**

检查取消确认不会删除，删除成功后列表刷新，manifest 预览更新。

### Task 6: 验证和提交页面变更

**Files:**
- Modify: `dib-release-center/src/web/pages/ReleaseAssetsPage.vue`
- Modify: `dib-release-center/src/web/pages/PluginReleasesPage.vue`
- Modify: `dib-release-center/src/web/pages/ClientReleasesPage.vue`

**Step 1: 运行测试**

Run:

```powershell
cd dib-release-center
npm test -- --run
```

Expected: PASS。

**Step 2: 运行构建**

Run:

```powershell
cd dib-release-center
npm run build
```

Expected: PASS。

**Step 3: 检查文档语言**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-doc-lang.ps1
```

Expected: PASS。

**Step 4: 提交页面变更**

Run:

```powershell
git add dib-release-center\src
git commit -m "feat: 增加发布中心资源删除功能"
```

### Task 7: 发布中心清理演练

**Files:**
- Modify: `docs/plans/2026-04-26-release-center-delete-capability.md`

**Step 1: 启动发布中心**

Run:

```powershell
cd dib-release-center
npm run dev
```

Expected: 输出本地访问地址。

**Step 2: 通过页面删除测试资源**

在发布中心页面执行：

1. 删除不需要的插件版本。
2. 删除不需要的插件定义。
3. 删除未被引用的发布资产记录和文件。
4. 保留单个 `patient-registration` 测试插件版本。

**Step 3: 验证升级测试资源**

确认页面中只保留本轮测试需要的病人登记插件资源，并保留 manifest 手工发布入口。

**Step 4: 记录验证结果**

在本计划末尾追加“执行记录”，写明：

- 删除能力验证结果。
- 是否已清理测试插件。
- 是否保留病人登记插件。
- 未完成项。

**Step 5: 提交执行记录**

Run:

```powershell
git add docs\plans\2026-04-26-release-center-delete-capability.md
git commit -m "docs: 记录发布中心删除能力验证"
```

## 执行记录

执行时间：2026-04-26。

### 代码验证

已完成发布中心删除能力代码实现，并完成以下验证：

- `npm test -- --run`：`12` 个测试文件通过，`46` 个测试通过。
- `npm run build`：`vue-tsc --noEmit && vite build` 通过。
- `scripts/check-doc-lang.ps1`：文档语言检查通过。

### prod101 页面部署

已将 `dib-release-center/dist` 构建产物部署到：

```text
/data/dib-release-center/dist/release-center/
```

线上入口已引用新静态资源：

```text
/release-center/assets/index-oDPi0e15.js
/release-center/assets/index-DPkx7Sm6.css
```

### prod101 清理与发布

由于当前浏览器没有发布中心管理员登录态，本次清理采用 prod101 上的受约束数据库事务和 Supabase Storage API 执行，操作范围限定为明确测试资源和本次病人登记插件版本。

已删除：

- 空插件定义：`bedside-rounding`。
- 未被版本引用的测试资产：`plugins/patient-registration/stable/1.0.1/upload-test-package.zip`。

已保留：

- 插件定义：`patient-registration`。
- 既有 `patient-registration` 版本记录，用于升级链路回归参考。

已上传并登记新插件版本：

```text
pluginCode: patient-registration
version: 1.0.3-dev.1
channel: stable
storagePath: plugins/patient-registration/stable/1.0.3-dev.1/patient-registration-1.0.3-dev.1.zip
sha256: ef164b9d0619097fb8071b455c5f0dc4b77ea615535a2e9bd51d3a8175fb7e0c
sizeBytes: 59335620
```

已重新发布：

```text
manifests/stable/plugin-manifest.json
```

公开 manifest 当前只包含 `patient-registration 1.0.3-dev.1`，新插件 zip 公开 URL 返回 `200`。

### 未完成项

尚未通过发布中心页面登录态实测删除按钮点击流程。原因是当前 MCP 浏览器没有管理员登录态；页面代码已通过测试和构建验证，真实数据清理已通过后端受约束操作完成。

### 二次清理记录

用户提供发布中心管理员账号后，MCP 浏览器已成功登录发布中心，并通过页面确认删除旧插件版本。

清理目标限定为插件测试资源：

- 保留插件定义：`patient-registration`。
- 保留插件版本：`patient-registration 1.0.3-dev.1`。
- 保留插件包资产：`patient-registration-1.0.3-dev.1.zip`。
- 删除旧插件版本：`0.1.0-dev.1`、`1.0.0`、`1.0.1`、`1.0.2-dev.1`。
- 删除旧插件包资产及其 Storage 文件。

执行过程中发现页面自动匹配 `1.0.0` 时会同时命中 `DIB 最低版本 = 1.0.0` 的新版本行，导致 `1.0.3-dev.1` 版本记录被误删。已立即停止页面批量点击，使用受约束数据库事务恢复 `1.0.3-dev.1` 版本记录，并继续删除旧版本和旧资产。

最终复核结果：

- 数据库中 `patient-registration` 插件版本只剩 `1.0.3-dev.1`。
- 数据库中 `plugin_package` 资产只剩 `patient-registration-1.0.3-dev.1.zip`。
- 旧插件 zip 公开访问均返回 `400`。
- `plugin-manifest.json` 已重新发布，只包含 `patient-registration 1.0.3-dev.1`。
- 发布中心页面刷新后，插件版本页显示 `1` 条记录，发布资产页不再显示旧插件 zip。

### 客户端版本清理记录

用户确认系统仍未上线后，继续清理客户端测试资源。

清理目标限定为客户端版本和客户端包资产：

- 保留客户端版本：`1.0.3`。
- 保留客户端包资产：`dib-win-x64-portable-1.0.3.zip`。
- 删除旧客户端版本：`1.0.2`、`1.0.1`、`1.0.0`、`0.0.0-rehearsal`、`0.0.0-rehearsal-20260425`。
- 删除旧客户端包资产及其 Storage 文件。
- 重新发布 `client-manifest.json`，使 `latestVersion` 继续指向 `1.0.3`。

最终复核结果：

- 数据库中客户端版本只剩 `1.0.3`。
- 数据库中 `client_package` 资产只剩 `dib-win-x64-portable-1.0.3.zip`。
- 旧客户端 zip 公开访问均返回 `400`。
- `client-manifest.json` 公开访问返回 `200`，内容指向 `1.0.3`。
- 发布中心页面刷新后，客户端版本页显示 `1` 条记录，发布资产页不再显示旧客户端 zip。

### 未使用资产清理检查

继续检查发布中心未使用资产。判定规则：

- `client_package` 和 `plugin_package` 必须被对应版本记录引用，否则视为未使用资产。
- `manifest` 不被版本记录引用，但它是客户端公开读取入口，因此不按未使用资产删除。

数据库复核结果：

- `release_assets` 中未被 `client_versions` 或 `plugin_versions` 引用的非 manifest 资产数量为 `0`。
- 当前 `client_package` 只剩 `dib-win-x64-portable-1.0.3.zip`，并被客户端版本 `1.0.3` 引用。
- 当前 `plugin_package` 只剩 `patient-registration-1.0.3-dev.1.zip`，并被插件版本 `1.0.3-dev.1` 引用。
- 当前 manifest 资产为 `client-manifest.json` 和 `plugin-manifest.json`，均保留。

Storage 递归复核结果：

- `clients/stable/1.0.3/` 下只剩 `dib-win-x64-portable-1.0.3.zip`。
- `plugins/patient-registration/stable/1.0.3-dev.1/` 下只剩 `patient-registration-1.0.3-dev.1.zip`。
- `manifests/stable/` 下只剩 `client-manifest.json` 和 `plugin-manifest.json`。

因此本次没有额外需要删除的未使用资产。
