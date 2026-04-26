# Release Center Asset Replace Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 为 DIB 发布中心增加已有发布资产的文件覆盖能力，并同步更新 `release_assets` 元数据。

**Architecture:** 覆盖操作复用现有文件摘要计算流程，但在仓储层新增专用上传方法，Storage 上传使用 `upsert: true`。UI 在每行资产上提供“覆盖文件”，App 负责串联上传、元数据更新、刷新列表和提示重新发布 manifest。

**Tech Stack:** Vue 3、TypeScript、Supabase JavaScript SDK、Vitest、Vite。

---

### Task 1: 仓储测试

**Files:**
- Modify: `dib-release-center/src/repositories/releaseAssetsRepository.test.ts`

**Step 1: Write the failing test**

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

**Step 2: Run test to verify it fails**

Run:

```powershell
cd dib-release-center
npm test -- --run src/repositories/releaseAssetsRepository.test.ts
```

Expected: FAIL，因为函数尚未导出。

### Task 2: 仓储实现

**Files:**
- Modify: `dib-release-center/src/repositories/releaseAssetsRepository.ts`

**Step 1: Implement repository functions**

新增：

```ts
export async function updateReleaseAssetMetadata(id: string, payload: ReleaseAssetMetadataUpdatePayload): Promise<void>
```

新增：

```ts
export async function replaceReleaseAssetFile(bucketName: string, storagePath: string, file: File): Promise<void>
```

**Step 2: Run targeted test**

Run:

```powershell
cd dib-release-center
npm test -- --run src/repositories/releaseAssetsRepository.test.ts
```

Expected: PASS。

### Task 3: UI 事件

**Files:**
- Modify: `dib-release-center/src/web/pages/ReleaseAssetsPage.vue`
- Modify: `dib-release-center/src/App.vue`

**Step 1: Add row-level replace action**

在资产行增加“覆盖文件”按钮和隐藏文件选择框。选择文件后发出：

```ts
replace: [payload: { asset: ReleaseAsset; file: File }]
```

确认文案必须包含资产路径和旧 SHA256。

**Step 2: Wire App handler**

`App.vue` 引入 `replaceReleaseAssetFile` 和 `updateReleaseAssetMetadata`。处理流程：

1. `buildReleaseAssetUploadPlan` 计算新文件元数据。
2. `replaceReleaseAssetFile(asset.bucketName, asset.storagePath, plan.file)`。
3. `updateReleaseAssetMetadata(asset.id, plan.payload 中的 file_name/sha256/size_bytes/mime_type)`。
4. 刷新数据。
5. 状态提示“资产已覆盖，请重新发布对应渠道 manifest”。

### Task 4: 全量验证

**Files:**
- Verify: `dib-release-center`
- Verify: `docs/plans/2026-04-26-release-center-asset-replace-design.md`
- Verify: `docs/plans/2026-04-26-release-center-asset-replace.md`

**Step 1: Run tests**

```powershell
cd dib-release-center
npm test -- --run
```

Expected: all tests pass.

**Step 2: Run build**

```powershell
cd dib-release-center
npm run build
```

Expected: build succeeds.

**Step 3: Run doc language check**

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\check-doc-lang.ps1
```

Expected: pass.

### Task 5: 部署和浏览器核验

**Files:**
- Deploy: `dib-release-center/dist/release-center/`

**Step 1: Deploy release center**

构建后将 `dib-release-center/dist/release-center/` 同步到 prod101 的 `/data/dib-release-center/dist/release-center/`。

**Step 2: Browser verify**

打开：

```text
http://101.42.19.26:8000/release-center/
```

进入“发布资产”，确认资产列表每行出现“覆盖文件”按钮。可选择一个测试资产进行覆盖演练；如果不需要实际覆盖，只确认入口和确认文案。

### Task 6: Commit

**Files:**
- Commit only task-related files.

**Step 1: Stage related files**

```powershell
git add dib-release-center/src/repositories/releaseAssetsRepository.ts dib-release-center/src/repositories/releaseAssetsRepository.test.ts dib-release-center/src/web/pages/ReleaseAssetsPage.vue dib-release-center/src/App.vue docs/plans/2026-04-26-release-center-asset-replace-design.md docs/plans/2026-04-26-release-center-asset-replace.md
```

**Step 2: Commit**

```powershell
git commit -m "feat: 增加发布中心资产覆盖能力"
```
