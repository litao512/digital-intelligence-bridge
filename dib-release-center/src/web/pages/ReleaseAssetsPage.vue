<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Release Assets</p>
        <h2>发布资产</h2>
      </div>
      <span class="panel-count">{{ assets.length }} 条记录</span>
    </header>

    <form class="release-form" @submit.prevent="submitDraft">
      <div class="field-grid field-grid-wide">
        <label>
          <span>Bucket</span>
          <input v-model="draft.bucketName" placeholder="dib-releases">
        </label>
        <label>
          <span>Storage Path</span>
          <input v-model="draft.storagePath" placeholder="plugins/.../package.zip">
        </label>
        <label>
          <span>File Name</span>
          <input v-model="draft.fileName" placeholder="package.zip">
        </label>
        <label>
          <span>Asset Kind</span>
          <select v-model="draft.assetKind">
            <option value="plugin_package">plugin_package</option>
            <option value="client_package">client_package</option>
            <option value="manifest">manifest</option>
          </select>
        </label>
        <label>
          <span>SHA256</span>
          <input v-model="draft.sha256" placeholder="64位十六进制摘要">
        </label>
        <label>
          <span>Size Bytes</span>
          <input v-model="draft.sizeBytes" placeholder="1024">
        </label>
        <label>
          <span>MIME Type</span>
          <input v-model="draft.mimeType" placeholder="application/zip">
        </label>
      </div>

      <div class="field-grid field-grid-wide upload-grid">
        <label>
          <span>选择文件</span>
          <input type="file" @change="handleFileChange">
        </label>
        <div class="upload-summary" v-if="selectedFileName">
          <strong>{{ selectedFileName }}</strong>
          <span>{{ selectedFileSummary }}</span>
        </div>
      </div>

      <div class="form-actions">
        <button type="submit">登记资产</button>
        <button type="button" class="ghost-button" :disabled="!selectedFile" @click="submitUpload">
          上传并登记资产
        </button>
      </div>
      <p class="form-tip">已支持文件直传到 Storage，并自动计算文件摘要、大小与 MIME；手工登记入口继续保留给历史资产补录使用。</p>
    </form>

    <div v-if="assets.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>文件</th>
            <th>类型</th>
            <th>Bucket/Path</th>
            <th>大小</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in assets" :key="item.id">
            <td>
              <strong>{{ item.fileName }}</strong>
              <div class="subline">{{ item.sha256 }}</div>
            </td>
            <td>{{ item.assetKind }}</td>
            <td>
              <div>{{ item.bucketName }}</div>
              <div class="subline">{{ item.storagePath }}</div>
            </td>
            <td>{{ item.sizeBytes }}</td>
            <td class="action-cell">
              <label class="ghost-button inline-button file-action">
                <span>覆盖文件</span>
                <input type="file" @change="replaceAsset(item, $event)">
              </label>
              <button type="button" class="danger-button inline-button" @click="removeAsset(item, false)">
                删除记录
              </button>
              <button type="button" class="danger-button inline-button" @click="removeAsset(item, true)">
                删除记录和文件
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else class="empty">当前没有发布资产记录。</p>
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import type { ReleaseAsset } from '@/contracts/release-types'
import type { ReleaseAssetDraftInput, ReleaseAssetUploadInput } from '@/services/releaseDraftService'

defineProps<{
  assets: ReleaseAsset[]
}>()

const emit = defineEmits<{
  submit: [draft: ReleaseAssetDraftInput]
  upload: [draft: ReleaseAssetUploadInput]
  replace: [payload: { asset: ReleaseAsset; file: File }]
  delete: [payload: { asset: ReleaseAsset; removeObject: boolean }]
}>()

const draft = reactive<ReleaseAssetDraftInput>({
  bucketName: 'dib-releases',
  storagePath: '',
  fileName: '',
  assetKind: 'plugin_package',
  sha256: '',
  sizeBytes: '',
  mimeType: 'application/zip',
})

const selectedFile = ref<File | null>(null)

const selectedFileName = computed(() => selectedFile.value?.name ?? '')
const selectedFileSummary = computed(() => {
  if (!selectedFile.value) {
    return ''
  }

  const mimeType = selectedFile.value.type.trim() || 'application/octet-stream'
  return `${selectedFile.value.size} bytes / ${mimeType}`
})

function handleFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  selectedFile.value = file

  if (!file) {
    return
  }

  draft.fileName = file.name
  draft.sizeBytes = `${file.size}`
  draft.mimeType = file.type.trim() || 'application/octet-stream'

  if (!draft.storagePath.trim()) {
    draft.storagePath = file.name
  }
}

function submitDraft(): void {
  emit('submit', { ...draft })
}

function submitUpload(): void {
  if (!selectedFile.value) {
    return
  }

  emit('upload', {
    bucketName: draft.bucketName,
    storagePath: draft.storagePath,
    assetKind: draft.assetKind,
    file: selectedFile.value,
  })
}

function removeAsset(asset: ReleaseAsset, removeObject: boolean): void {
  const target = `${asset.fileName}\n${asset.bucketName}/${asset.storagePath}\nSHA256: ${asset.sha256}`
  const action = removeObject ? '删除资产记录和 Storage 文件' : '只删除资产记录'

  if (!window.confirm(`确认${action}？\n\n${target}\n\n删除后不会自动发布 manifest。`)) {
    return
  }

  emit('delete', { asset, removeObject })
}

function replaceAsset(asset: ReleaseAsset, event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  input.value = ''

  if (!file) {
    return
  }

  const target = `${asset.fileName}\n${asset.bucketName}/${asset.storagePath}\n当前 SHA256: ${asset.sha256}`
  if (!window.confirm(`确认覆盖该发布资产文件？\n\n${target}\n\n覆盖会更新 Storage 文件和资产元数据，但不会自动发布 manifest。`)) {
    return
  }

  emit('replace', { asset, file })
}
</script>

<style scoped>
.action-cell {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.danger-button {
  background: #fff1f0;
  color: #bf3d36;
}

.file-action {
  position: relative;
  overflow: hidden;
}

.file-action input {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
}
</style>
