<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Plugin Releases</p>
        <h2>插件版本</h2>
      </div>
      <span class="panel-count">{{ versions.length }} 条记录</span>
    </header>

    <form class="release-form" @submit.prevent="submitPackageDraft">
      <div class="field-grid field-grid-wide">
        <label>
          <span>插件编码</span>
          <input v-model="packageDraft.pluginCode" placeholder="patient-registration">
        </label>
        <label>
          <span>插件名称</span>
          <input v-model="packageDraft.pluginName" placeholder="就诊登记">
        </label>
        <label>
          <span>入口类型</span>
          <input v-model="packageDraft.entryType" placeholder="assembly">
        </label>
        <label>
          <span>作者</span>
          <input v-model="packageDraft.author" placeholder="DIB">
        </label>
        <label class="textarea-field package-description-field">
          <span>插件说明</span>
          <textarea v-model="packageDraft.description" rows="2" placeholder="记录插件职责与用途" />
        </label>
        <label class="checkbox-field package-active-field">
          <input v-model="packageDraft.isActive" type="checkbox">
          <span>启用该插件定义</span>
        </label>
      </div>

      <div class="form-actions">
        <button type="submit" class="ghost-button">新增插件定义</button>
      </div>
      <p class="form-tip">先创建插件定义，再选择对应资产和版本信息发布插件版本。</p>
    </form>

    <form class="release-form" @submit.prevent="submitDraft">
      <div class="field-grid field-grid-wide">
        <label>
          <span>插件定义</span>
          <select v-model="draft.packageId">
            <option value="">请选择插件</option>
            <option v-for="item in packages" :key="item.id" :value="item.id">
              {{ item.pluginName }} / {{ item.pluginCode }}
            </option>
          </select>
        </label>
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
          <span>DIB 最低版本</span>
          <input v-model="draft.dibMinVersion" placeholder="1.0.0">
        </label>
        <label>
          <span>DIB 最高版本</span>
          <input v-model="draft.dibMaxVersion" placeholder="1.9.99">
        </label>
      </div>

      <label class="textarea-field">
        <span>manifest JSON</span>
        <textarea v-model="draft.manifestJsonText" rows="4" placeholder='{"entry":"PatientRegistration.Plugin"}' />
      </label>

      <label class="textarea-field">
        <span>发布说明</span>
        <textarea v-model="draft.releaseNotes" rows="3" placeholder="记录本次插件发布说明" />
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
        <button type="submit">新增插件版本</button>
      </div>
      <p class="form-tip">资产已支持直传，插件定义也可直接在当前页面新增；版本发布仍通过资产 ID 关联对应插件包。</p>
    </form>

    <div v-if="versions.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>插件</th>
            <th>版本</th>
            <th>渠道</th>
            <th>DIB 范围</th>
            <th>发布</th>
            <th>强制</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in versions" :key="item.id">
            <td>
              <strong>{{ item.pluginName }}</strong>
              <div class="subline">{{ item.pluginCode }}</div>
            </td>
            <td>{{ item.version }}</td>
            <td>{{ item.channelCode }}</td>
            <td>{{ item.dibMinVersion }} - {{ item.dibMaxVersion }}</td>
            <td>{{ item.isPublished ? '已发布' : '草稿' }}</td>
            <td>{{ item.isMandatory ? '是' : '否' }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else class="empty">当前没有插件版本记录。</p>
  </section>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { PluginPackage, PluginVersion, ReleaseChannel } from '@/contracts/release-types'
import type { PluginPackageDraftInput, PluginVersionDraftInput } from '@/services/releaseDraftService'

const props = defineProps<{
  versions: PluginVersion[]
  channels: ReleaseChannel[]
  packages: PluginPackage[]
}>()

const emit = defineEmits<{
  submit: [draft: PluginVersionDraftInput]
  createPackage: [draft: PluginPackageDraftInput]
}>()

const packageDraft = reactive<PluginPackageDraftInput>({
  pluginCode: '',
  pluginName: '',
  entryType: 'assembly',
  author: 'DIB',
  description: '',
  isActive: true,
})

const draft = reactive<PluginVersionDraftInput>({
  packageId: '',
  channelId: '',
  assetId: '',
  version: '',
  dibMinVersion: '1.0.0',
  dibMaxVersion: '9999.9999.9999',
  releaseNotes: '',
  manifestJsonText: '{}',
  isPublished: false,
  isMandatory: false,
})

watch(
  () => props.packages,
  (packages) => {
    if (!draft.packageId && packages.length > 0) {
      draft.packageId = packages[0]?.id ?? ''
    }
  },
  { immediate: true },
)

watch(
  () => props.channels,
  (channels) => {
    if (!draft.channelId && channels.length > 0) {
      draft.channelId = channels[0]?.id ?? ''
    }
  },
  { immediate: true },
)

function submitPackageDraft(): void {
  emit('createPackage', { ...packageDraft })
}

function submitDraft(): void {
  emit('submit', { ...draft })
}
</script>
