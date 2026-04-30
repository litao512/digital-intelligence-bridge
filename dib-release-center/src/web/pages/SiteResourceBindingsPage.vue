<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Site Resource Bindings</p>
        <h2>站点资源绑定</h2>
      </div>
      <span class="panel-count">{{ selectedSite?.organizationName ?? '未选择站点' }}</span>
    </header>

    <div class="field-grid field-grid-wide">
      <label>
        <span>站点</span>
        <select v-model="selectedSiteRowId" @change="selectSite">
          <option value="">请选择站点</option>
          <option v-for="site in sites" :key="site.id" :value="site.id">{{ site.siteName || site.siteId }} / {{ site.organizationName ?? '未绑定单位' }}</option>
        </select>
      </label>
      <label>
        <span>插件</span>
        <select v-model="pluginCode">
          <option value="">请选择插件</option>
          <option v-for="pkg in pluginCandidates" :key="pkg.id" :value="pkg.pluginCode">{{ pkg.pluginName }} / {{ pkg.pluginCode }}</option>
        </select>
      </label>
      <label><span>usage_key</span><input v-model="usageKey" placeholder="registration-db"></label>
      <label>
        <span>资源</span>
        <select v-model="resourceId">
          <option value="">请选择资源</option>
          <option v-for="resource in resourceCandidates" :key="resource.id" :value="resource.id">{{ resource.resourceName }} / {{ resource.resourceCode }}</option>
        </select>
      </label>
    </div>

    <div class="form-actions">
      <button type="button" :disabled="!selectedSiteRowId" @click="submitBinding">保存绑定</button>
    </div>
    <p class="form-tip">{{ validationMessage || '候选插件和资源来自站点所属单位的有效授权。' }}</p>

    <div v-if="bindings.length" class="table-wrap">
      <table>
        <thead><tr><th>插件</th><th>usage_key</th><th>资源</th><th>状态</th><th>操作</th></tr></thead>
        <tbody>
          <tr v-for="item in bindings" :key="item.id">
            <td>{{ item.pluginCode }}</td>
            <td><code>{{ item.usageKey }}</code></td>
            <td>{{ resourceName(item.resourceId) }}</td>
            <td>{{ item.status }}</td>
            <td><button type="button" class="ghost-button inline-button" @click="$emit('deactivate', item.id)">停用</button></td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else class="empty page-empty">当前站点没有资源绑定。</p>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { OrganizationPluginPermission, OrganizationResourcePermission } from '@/contracts/organization-types'
import type { PluginPackage } from '@/contracts/release-types'
import type { ResourceBindingSummary, ResourceSummary } from '@/contracts/resource-types'
import type { SiteSummary } from '@/contracts/site-types'
import {
  buildPluginCandidateList,
  buildResourceBindingCandidateList,
  validateResourceBindingRequest,
} from '@/services/resourceBindingService'

const props = defineProps<{
  sites: SiteSummary[]
  packages: PluginPackage[]
  resources: ResourceSummary[]
  bindings: ResourceBindingSummary[]
  pluginPermissions: OrganizationPluginPermission[]
  resourcePermissions: OrganizationResourcePermission[]
  selectedSiteRowId: string
}>()

const emit = defineEmits<{
  selectSite: [siteRowId: string]
  submit: [payload: { siteRowId: string; pluginCode: string; resourceId: string; usageKey: string }]
  deactivate: [bindingId: string]
}>()

const selectedSiteRowId = ref(props.selectedSiteRowId)
const pluginCode = ref('')
const resourceId = ref('')
const usageKey = ref('')

watch(() => props.selectedSiteRowId, (value) => {
  selectedSiteRowId.value = value
})

const selectedSite = computed(() => props.sites.find((item) => item.id === selectedSiteRowId.value) ?? null)
const pluginCandidates = computed(() => buildPluginCandidateList(props.packages, props.pluginPermissions))
const resourceCandidates = computed(() => buildResourceBindingCandidateList(props.resources, props.resourcePermissions))
const validationMessage = computed(() => validateResourceBindingRequest({
  site: selectedSite.value,
  pluginCode: pluginCode.value,
  resourceId: resourceId.value,
  usageKey: usageKey.value,
  pluginPermissions: props.pluginPermissions,
  resourcePermissions: props.resourcePermissions,
}))

function selectSite(): void {
  pluginCode.value = ''
  resourceId.value = ''
  usageKey.value = ''
  emit('selectSite', selectedSiteRowId.value)
}

function submitBinding(): void {
  if (validationMessage.value || !selectedSite.value) {
    return
  }

  emit('submit', {
    siteRowId: selectedSite.value.id,
    pluginCode: pluginCode.value,
    resourceId: resourceId.value,
    usageKey: usageKey.value,
  })
}

function resourceName(id: string): string {
  const resource = props.resources.find((item) => item.id === id)
  return resource ? `${resource.resourceName} / ${resource.resourceCode}` : id
}
</script>
