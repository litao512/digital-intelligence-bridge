<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Site Overrides</p>
        <h2>站点覆盖</h2>
      </div>
      <span class="panel-count">{{ filteredOverrides.length }} 条覆盖</span>
    </header>

    <p class="form-tip">
      站点覆盖只处理少量例外。默认先走分组授权，再用这里做单站点 `allow/deny` 覆盖。
    </p>

    <div class="field-grid field-grid-wide">
      <label>
        <span>站点搜索</span>
        <input v-model="siteKeyword" placeholder="站点名 / SiteId / 机器名 / 分组 / 客户端版本">
      </label>
      <label>
        <span>目标站点</span>
        <select :value="selectedSiteRowId" @change="emitSelectSite(($event.target as HTMLSelectElement).value)">
          <option value="">请选择站点</option>
          <option v-for="site in selectableSites" :key="site.id" :value="site.id">
            {{ site.siteName || '未命名站点' }} / {{ site.siteId }}
          </option>
        </select>
      </label>
    </div>

    <p v-if="selectedSiteSummary" class="subline">
      当前站点：{{ selectedSiteSummary.siteName || '未命名站点' }} / {{ selectedSiteSummary.siteId }} / {{ selectedSiteSummary.groupName || '未分组' }}
    </p>

    <form class="release-form compact-form" @submit.prevent="submitDraft">
      <div class="field-grid field-grid-wide">
        <label>
          <span>插件定义</span>
          <select v-model="draft.pluginPackageId" :disabled="!selectedSiteRowId">
            <option value="">请选择插件</option>
            <option v-for="item in packages" :key="item.id" :value="item.id">
              {{ item.pluginName }} / {{ item.pluginCode }}
            </option>
          </select>
        </label>
        <label>
          <span>覆盖动作</span>
          <select v-model="draft.action" :disabled="!selectedSiteRowId">
            <option value="allow">allow</option>
            <option value="deny">deny</option>
          </select>
        </label>
        <label class="checkbox-field">
          <input v-model="draft.isActive" type="checkbox" :disabled="!selectedSiteRowId">
          <span>覆盖生效</span>
        </label>
      </div>

      <label class="textarea-field">
        <span>原因说明</span>
        <textarea v-model="draft.reason" rows="2" placeholder="记录为什么要做站点覆盖" :disabled="!selectedSiteRowId" />
      </label>

      <div class="form-actions">
        <button type="submit" :disabled="!selectedSiteRowId">保存站点覆盖</button>
      </div>
    </form>

    <div v-if="selectedSiteRowId && filteredOverrides.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>插件</th>
            <th>动作</th>
            <th>状态</th>
            <th>原因</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in filteredOverrides" :key="item.id">
            <td>
              <strong>{{ item.pluginName }}</strong>
              <div class="subline">{{ item.pluginCode }}</div>
            </td>
            <td><code>{{ item.action }}</code></td>
            <td>{{ item.isActive ? '生效中' : '已停用' }}</td>
            <td>{{ item.reason || '未填写' }}</td>
            <td class="action-cell">
              <button type="button" class="ghost-button inline-button" @click="fillDraft(item)">编辑</button>
              <button type="button" class="danger-button inline-button" @click="removeOverride(item)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else-if="selectedSiteRowId" class="empty">当前站点还没有单独覆盖规则。</p>
    <p v-else class="empty">请选择一个站点开始配置例外授权。</p>
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { PluginPackage } from '@/contracts/release-types'
import type { SitePluginOverride, SiteSummary } from '@/contracts/site-types'
import type { SiteOverrideDraftInput } from '@/services/sitePolicyDraftService'
import { filterSitesForSelection } from '@/services/siteManagementService'

const props = defineProps<{
  sites: SiteSummary[]
  packages: PluginPackage[]
  overrides: SitePluginOverride[]
  selectedSiteRowId: string
}>()

const emit = defineEmits<{
  selectSite: [siteRowId: string]
  submit: [draft: SiteOverrideDraftInput]
  delete: [payload: { siteRowId: string; pluginPackageId: string }]
}>()

const draft = reactive<SiteOverrideDraftInput>({
  siteRowId: '',
  pluginPackageId: '',
  action: 'deny',
  reason: '',
  isActive: true,
})
const siteKeyword = ref('')

const filteredOverrides = computed(() => props.overrides.filter((item) => item.siteRowId === props.selectedSiteRowId))
const selectableSites = computed(() => filterSitesForSelection(props.sites, {
  keyword: siteKeyword.value,
  selectedSiteRowId: props.selectedSiteRowId,
}))
const selectedSiteSummary = computed(() =>
  props.sites.find((item) => item.id === props.selectedSiteRowId) ?? null,
)

watch(
  () => props.selectedSiteRowId,
  (value) => {
    draft.siteRowId = value
  },
  { immediate: true },
)

watch(
  () => props.packages,
  (packages) => {
    if (!draft.pluginPackageId && packages.length > 0) {
      draft.pluginPackageId = packages[0]?.id ?? ''
    }
  },
  { immediate: true },
)

function emitSelectSite(siteRowId: string): void {
  emit('selectSite', siteRowId)
}

function submitDraft(): void {
  emit('submit', { ...draft, siteRowId: props.selectedSiteRowId })
}

function fillDraft(item: SitePluginOverride): void {
  draft.siteRowId = item.siteRowId
  draft.pluginPackageId = item.pluginPackageId
  draft.action = item.action
  draft.reason = item.reason
  draft.isActive = item.isActive
}

function removeOverride(item: SitePluginOverride): void {
  emit('delete', {
    siteRowId: item.siteRowId,
    pluginPackageId: item.pluginPackageId,
  })
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
