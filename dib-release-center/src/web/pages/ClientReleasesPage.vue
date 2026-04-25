<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Client Releases</p>
        <h2>DIB 客户端版本</h2>
      </div>
      <span class="panel-count">{{ versions.length }} 条记录</span>
    </header>

    <form class="release-form" @submit.prevent="submitDraft">
      <div class="field-grid">
        <label>
          <span>发布渠道</span>
          <select v-model="draft.channelId">
            <option value="">请选择渠道</option>
            <option v-for="item in channels" :key="item.id" :value="item.id">
              {{ item.channelName }} / {{ item.channelCode }}
            </option>
          </select>
        </label>
        <label>
          <span>资产 ID</span>
          <input v-model="draft.assetId" placeholder="release_assets.id">
        </label>
        <label>
          <span>版本号</span>
          <input v-model="draft.version" placeholder="1.0.0">
        </label>
        <label>
          <span>最低升级版本</span>
          <input v-model="draft.minUpgradeVersion" placeholder="0.9.0">
        </label>
      </div>

      <label class="textarea-field">
        <span>发布说明</span>
        <textarea v-model="draft.releaseNotes" rows="3" placeholder="记录本次客户端发布说明" />
      </label>

      <div class="toggle-row">
        <label class="checkbox-field">
          <input v-model="draft.isPublished" type="checkbox">
          <span>立即标记为已发布</span>
        </label>
        <label class="checkbox-field">
          <input v-model="draft.isMandatory" type="checkbox">
          <span>强制升级</span>
        </label>
      </div>

      <div class="form-actions">
        <button type="submit">新增客户端版本</button>
      </div>
      <p class="form-tip">第一阶段先使用资产 ID 录入客户端包，后续补 Storage 直传。</p>
    </form>

    <div v-if="versions.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>版本</th>
            <th>渠道</th>
            <th>最低升级版本</th>
            <th>发布</th>
            <th>强制</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in versions" :key="item.id">
            <td>{{ item.version }}</td>
            <td>{{ item.channelCode }}</td>
            <td>{{ item.minUpgradeVersion }}</td>
            <td>{{ item.isPublished ? '已发布' : '草稿' }}</td>
            <td>{{ item.isMandatory ? '是' : '否' }}</td>
            <td class="action-cell">
              <button type="button" class="danger-button inline-button" @click="removeVersion(item)">删除版本</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else class="empty">当前没有客户端版本记录。</p>
  </section>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { ClientVersion, ReleaseChannel } from '@/contracts/release-types'
import type { ClientVersionDraftInput } from '@/services/releaseDraftService'

const props = defineProps<{
  versions: ClientVersion[]
  channels: ReleaseChannel[]
}>()

const emit = defineEmits<{
  submit: [draft: ClientVersionDraftInput]
  deleteVersion: [version: ClientVersion]
}>()

const draft = reactive<ClientVersionDraftInput>({
  channelId: '',
  assetId: '',
  version: '',
  minUpgradeVersion: '0.0.0',
  releaseNotes: '',
  isPublished: false,
  isMandatory: false,
})

watch(
  () => props.channels,
  (channels) => {
    if (!draft.channelId && channels.length > 0) {
      draft.channelId = channels[0]?.id ?? ''
    }
  },
  { immediate: true },
)

function submitDraft(): void {
  emit('submit', { ...draft })
}

function removeVersion(version: ClientVersion): void {
  if (!window.confirm(`确认删除客户端版本？\n\n版本：${version.version}\n渠道：${version.channelCode}\n状态：${version.isPublished ? '已发布' : '草稿'}\n\n删除后不会自动发布 manifest。`)) {
    return
  }

  emit('deleteVersion', version)
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
</style>
