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

      <div class="form-actions">
        <button type="submit">登记资产</button>
      </div>
      <p class="form-tip">当前先支持手工登记已上传文件的元数据；后续补文件直传。</p>
    </form>

    <div v-if="assets.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>文件</th>
            <th>类型</th>
            <th>Bucket/Path</th>
            <th>大小</th>
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
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else class="empty">当前没有发布资产记录。</p>
  </section>
</template>

<script setup lang="ts">
import { reactive } from 'vue'
import type { ReleaseAsset } from '@/contracts/release-types'
import type { ReleaseAssetDraftInput } from '@/services/releaseDraftService'

defineProps<{
  assets: ReleaseAsset[]
}>()

const emit = defineEmits<{
  submit: [draft: ReleaseAssetDraftInput]
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

function submitDraft(): void {
  emit('submit', { ...draft })
}
</script>
