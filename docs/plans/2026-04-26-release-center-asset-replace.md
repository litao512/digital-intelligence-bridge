# 发布中心资产覆盖实施计划

> **执行要求：** 实施时使用 `superpowers:executing-plans`，按任务逐项执行并在每项后验证。

**目标：** 为 DIB 发布中心增加已有发布资产的文件覆盖能力，并同步更新 `release_assets` 元数据。

**架构：** 覆盖操作复用现有文件摘要计算流程，但在仓储层新增专用上传方法，Storage 上传使用 `upsert: true`。UI 在每行资产上提供“覆盖文件”，App 负责串联上传、元数据更新、刷新列表和提示重新发布 manifest。

**技术栈：** Vue 3、TypeScript、Supabase JavaScript SDK、Vitest、Vite。

---

### 任务 1：仓储测试

**文件：**
- 修改：`dib-release-center/src/repositories/releaseAssetsRepository.test.ts`

**步骤 1：编写失败测试**

新增测试：

```ts
it('replaceReleaseAssetFile should upload with upsert enabled', async () => {
  const file = new File(['new package'], 'package.zip', { type: 'application/zip' })

  await replaceReleaseAssetFile('dib-releases', 'plugins/pkg.zip', file)

  expect(storageFromMock).toHaveBeenCalledWith('dib-releases')
  expect(uploadMock).toHaveBeenCalledWith(
    'plugins/pkg.zip',
    file,
    expect.objectContaining({
      upsert: true,
      contentType: 'application/zip',
    }),
  )
})
```

新增测试：

```ts
it('updateReleaseAssetMetadata should update metadata by asset id', async () => {
  const payload = {
    file_name: 'package.zip',
    sha256: 'a'.repeat(64),
    size_bytes: 123,
    mime_type: 'application/zip',
  }

  await updateReleaseAssetMetadata('asset-1', payload)

  expect(fromMock).toHaveBeenCalledWith('release_assets')
  expect(updateMock).toHaveBeenCalledWith(payload)
  expect(eqMock).toHaveBeenCalledWith('id', 'asset-1')
})
```

**步骤 2：运行测试并确认失败**

运行：

```powershell
cd dib-release-center
npm test -- --run src/repositories/releaseAssetsRepository.test.ts
```

预期：失败，因为函数尚未导出。

### 任务 2：仓储实现

**文件：**
- 修改：`dib-release-center/src/repositories/releaseAssetsRepository.ts`

**步骤 1：实现仓储函数**

新增：

```ts
export async function updateReleaseAssetMetadata(id: string, payload: ReleaseAssetMetadataUpdatePayload): Promise<void>
```

新增：

```ts
export async function replaceReleaseAssetFile(bucketName: string, storagePath: string, file: File): Promise<void>
```

**步骤 2：运行定向测试**

运行：

```powershell
cd dib-release-center
npm test -- --run src/repositories/releaseAssetsRepository.test.ts
```

预期：通过。

### 任务 3：UI 事件

**文件：**
- 修改：`dib-release-center/src/web/pages/ReleaseAssetsPage.vue`
- 修改：`dib-release-center/src/App.vue`

**步骤 1：增加行级覆盖操作**

在资产行增加“覆盖文件”按钮和隐藏文件选择框。选择文件后发出：

```ts
replace: [payload: { asset: ReleaseAsset; file: File }]
```

确认文案必须包含资产路径和旧 SHA256。

**步骤 2：串联 App 处理流程**

`App.vue` 引入 `replaceReleaseAssetFile` 和 `updateReleaseAssetMetadata`。处理流程：

1. `buildReleaseAssetUploadPlan` 计算新文件元数据。
2. `replaceReleaseAssetFile(asset.bucketName, asset.storagePath, plan.file)`。
3. `updateReleaseAssetMetadata(asset.id, plan.payload 中的 file_name/sha256/size_bytes/mime_type)`。
4. 刷新数据。
5. 状态提示“资产已覆盖，请重新发布对应渠道 manifest”。

### 任务 4：全量验证

**文件：**
- 验证：`dib-release-center`
- 验证：`docs/plans/2026-04-26-release-center-asset-replace-design.md`
- 验证：`docs/plans/2026-04-26-release-center-asset-replace.md`

**步骤 1：运行测试**

```powershell
cd dib-release-center
npm test -- --run
```

预期：全部测试通过。

**步骤 2：运行构建**

```powershell
cd dib-release-center
npm run build
```

预期：构建成功。

**步骤 3：运行文档语言检查**

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\check-doc-lang.ps1
```

预期：通过。

### 任务 5：部署和浏览器核验

**文件：**
- 部署：`dib-release-center/dist/release-center/`

**步骤 1：部署发布中心**

构建后将 `dib-release-center/dist/release-center/` 同步到 prod101 的 `/data/dib-release-center/dist/release-center/`。

**步骤 2：浏览器核验**

打开：

```text
http://101.42.19.26:8000/release-center/
```

进入“发布资产”，确认资产列表每行出现“覆盖文件”按钮。可选择一个测试资产进行覆盖演练；如果不需要实际覆盖，只确认入口和确认文案。

### 任务 6：提交

**文件：**
- 只提交任务相关文件。

**步骤 1：暂存相关文件**

```powershell
git add dib-release-center/src/repositories/releaseAssetsRepository.ts dib-release-center/src/repositories/releaseAssetsRepository.test.ts dib-release-center/src/web/pages/ReleaseAssetsPage.vue dib-release-center/src/App.vue docs/plans/2026-04-26-release-center-asset-replace-design.md docs/plans/2026-04-26-release-center-asset-replace.md
```

**步骤 2：提交**

```powershell
git commit -m "feat: 增加发布中心资产覆盖能力"
```
