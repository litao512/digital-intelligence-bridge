<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Sites</p>
        <h2>站点管理</h2>
      </div>
      <span class="panel-count">{{ filteredSites.length }} / {{ sites.length }} 个站点</span>
    </header>

    <p class="form-tip">
      每条记录代表一个 DIB 安装实例。调整所属分组后，后续按站点生成的插件授权将以最新分组为准。
    </p>

    <div class="field-grid field-grid-wide page-filter-grid">
      <label>
        <span>关键字搜索</span>
        <input v-model="keyword" placeholder="站点名 / SiteId / 机器名 / 分组 / 客户端版本">
      </label>
      <label>
        <span>按分组筛选</span>
        <select v-model="groupFilterId">
          <option value="">全部分组</option>
          <option v-for="group in groups" :key="group.id" :value="group.id">
            {{ group.groupName }} / {{ group.groupCode }}
          </option>
        </select>
      </label>
      <label>
        <span>批量分配到分组</span>
        <select v-model="bulkGroupId">
          <option value="">请选择目标分组</option>
          <option v-for="group in groups" :key="group.id" :value="group.id">
            {{ group.groupName }} / {{ group.groupCode }}
          </option>
        </select>
      </label>
    </div>

    <div class="page-filter-row">
      <label class="checkbox-field compact-checkbox">
        <input v-model="onlyUnassigned" type="checkbox">
        <span>只看未分组</span>
      </label>
      <label class="checkbox-field compact-checkbox">
        <input v-model="onlyRecentlyActive" type="checkbox">
        <span>只看最近活跃</span>
      </label>
    </div>

    <div class="form-actions page-filter-actions">
      <button type="button" :disabled="selectedSiteIds.length === 0 || !bulkGroupId" @click="submitBulkAssign">批量分配分组</button>
      <button type="button" class="ghost-button" @click="resetFilters">清空筛选</button>
      <span class="form-tip">已选择 {{ selectedSiteIds.length }} 个站点</span>
    </div>

    <div v-if="filteredSites.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>
              <input type="checkbox" :checked="allFilteredSelected" @change="toggleSelectAll(($event.target as HTMLInputElement).checked)">
            </th>
            <th>站点</th>
            <th>站点标识</th>
            <th>所属分组</th>
            <th>所属单位</th>
            <th>客户端版本</th>
            <th>最近活跃</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="site in filteredSites" :key="site.id">
            <td>
              <input type="checkbox" :checked="selectedSiteIds.includes(site.id)" @change="toggleSelectSite(site.id, ($event.target as HTMLInputElement).checked)">
            </td>
            <td>
              <strong>{{ site.siteName || '未命名站点' }}</strong>
              <div class="subline">{{ site.machineName }}</div>
            </td>
            <td><code>{{ site.siteId }}</code></td>
            <td>
              <select
                :value="site.groupId ?? ''"
                @change="onGroupChange(site.id, ($event.target as HTMLSelectElement).value || null)"
              >
                <option value="">未分组</option>
                <option v-for="group in groups" :key="group.id" :value="group.id">
                  {{ group.groupName }} / {{ group.groupCode }}
                </option>
              </select>
            </td>
            <td>
              <select
                :value="site.organizationId ?? ''"
                @change="onOrganizationChange(site.id, ($event.target as HTMLSelectElement).value || null)"
              >
                <option value="">未绑定单位</option>
                <option v-for="organization in organizations" :key="organization.id" :value="organization.id">
                  {{ organization.name }} / {{ organization.code }}
                </option>
              </select>
            </td>
            <td>{{ site.clientVersion }}</td>
            <td>{{ formatDate(site.lastSeenAt) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else class="empty page-empty">当前还没有站点接入记录。</p>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { OrganizationSummary } from '@/contracts/organization-types'
import type { SiteGroup, SiteSummary } from '@/contracts/site-types'
import { createDefaultSiteFilterInput, filterSites } from '@/services/siteManagementService'

const props = defineProps<{
  sites: SiteSummary[]
  groups: SiteGroup[]
  organizations: OrganizationSummary[]
  searchSeed?: string
}>()

const emit = defineEmits<{
  assignGroup: [payload: { siteRowId: string; groupId: string | null }]
  assignOrganization: [payload: { siteRowId: string; organizationId: string | null }]
  bulkAssignGroup: [payload: { siteRowIds: string[]; groupId: string }]
}>()

const defaultFilters = createDefaultSiteFilterInput()
const keyword = ref(defaultFilters.keyword)
const groupFilterId = ref(defaultFilters.groupId)
const bulkGroupId = ref('')
const selectedSiteIds = ref<string[]>([])
const onlyUnassigned = ref(defaultFilters.onlyUnassigned ?? false)
const onlyRecentlyActive = ref(defaultFilters.onlyRecentlyActive ?? false)

watch(
  () => props.searchSeed,
  (value) => {
    if (typeof value === 'string' && value.trim()) {
      keyword.value = value
    }
  },
  { immediate: true },
)

const filteredSites = computed(() => filterSites(props.sites, {
  keyword: keyword.value,
  groupId: groupFilterId.value,
  onlyUnassigned: onlyUnassigned.value,
  onlyRecentlyActive: onlyRecentlyActive.value,
}))

const allFilteredSelected = computed(() =>
  filteredSites.value.length > 0 && filteredSites.value.every((site) => selectedSiteIds.value.includes(site.id)),
)

function onGroupChange(siteRowId: string, groupId: string | null): void {
  emit('assignGroup', { siteRowId, groupId })
}

function onOrganizationChange(siteRowId: string, organizationId: string | null): void {
  emit('assignOrganization', { siteRowId, organizationId })
}

function toggleSelectSite(siteRowId: string, checked: boolean): void {
  if (checked) {
    selectedSiteIds.value = [...new Set([...selectedSiteIds.value, siteRowId])]
    return
  }

  selectedSiteIds.value = selectedSiteIds.value.filter((item) => item !== siteRowId)
}

function toggleSelectAll(checked: boolean): void {
  if (checked) {
    selectedSiteIds.value = [...new Set([...selectedSiteIds.value, ...filteredSites.value.map((site) => site.id)])]
    return
  }

  const filteredIds = new Set(filteredSites.value.map((site) => site.id))
  selectedSiteIds.value = selectedSiteIds.value.filter((item) => !filteredIds.has(item))
}

function submitBulkAssign(): void {
  if (!bulkGroupId.value || selectedSiteIds.value.length === 0) {
    return
  }

  emit('bulkAssignGroup', {
    siteRowIds: [...selectedSiteIds.value],
    groupId: bulkGroupId.value,
  })
}

function resetFilters(): void {
  const filters = createDefaultSiteFilterInput()
  keyword.value = filters.keyword
  groupFilterId.value = filters.groupId
  onlyUnassigned.value = filters.onlyUnassigned ?? false
  onlyRecentlyActive.value = filters.onlyRecentlyActive ?? false
}

function formatDate(value: string | null): string {
  if (!value) {
    return '尚未上报'
  }

  return new Date(value).toLocaleString('zh-CN', { hour12: false })
}
</script>

<style scoped>
.compact-checkbox {
  gap: 8px;
}
</style>
