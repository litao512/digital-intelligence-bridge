<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Site Analytics</p>
        <h2>站点统计</h2>
      </div>
    </header>

    <div class="status-grid analytics-grid">
      <article class="status-card">
        <h2>站点总数</h2>
        <p>{{ analytics.overview.totalSiteCount }}</p>
      </article>
      <article class="status-card">
        <h2>近 24 小时活跃</h2>
        <p>{{ analytics.overview.activeSiteCount24h }}</p>
      </article>
      <article class="status-card">
        <h2>未分组站点</h2>
        <p>{{ analytics.overview.unassignedSiteCount }}</p>
      </article>
      <article class="status-card">
        <h2>分组授权策略</h2>
        <p>{{ analytics.overview.groupPolicyCount }}</p>
      </article>
      <article class="status-card">
        <h2>站点覆盖规则</h2>
        <p>{{ analytics.overview.overrideCount }}</p>
      </article>
      <article class="status-card">
        <h2>存在授权漂移</h2>
        <p>{{ analytics.overview.driftSiteCount }}</p>
      </article>
      <article class="status-card">
        <h2>已授权未安装</h2>
        <p>{{ analytics.overview.authorizedButNotInstalledSiteCount }}</p>
      </article>
      <article class="status-card">
        <h2>已安装未授权</h2>
        <p>{{ analytics.overview.installedButNotAuthorizedSiteCount }}</p>
      </article>
    </div>

    <div class="manifest-grid analytics-sections">
      <article class="panel manifest-card">
        <header class="panel-header tight">
          <div>
            <p class="panel-kicker">Groups</p>
            <h2>分组运营概览</h2>
          </div>
        </header>
        <table>
          <thead>
            <tr>
              <th>分组</th>
              <th>站点数</th>
              <th>24 小时活跃</th>
              <th>授权插件数</th>
              <th>漂移站点数</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="group in analytics.groupRows" :key="group.groupCode">
              <td>{{ group.groupName }} / {{ group.groupCode }}</td>
              <td>{{ group.siteCount }}</td>
              <td>{{ group.activeSiteCount24h }}</td>
              <td>{{ group.policyCount }}</td>
              <td>{{ group.driftSiteCount }}</td>
            </tr>
          </tbody>
        </table>
      </article>

      <article class="panel manifest-card">
        <header class="panel-header tight">
          <div>
            <p class="panel-kicker">Versions</p>
            <h2>客户端版本分布</h2>
          </div>
        </header>
        <table>
          <thead>
            <tr>
              <th>版本</th>
              <th>站点数</th>
              <th>24 小时活跃</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="version in analytics.versionBreakdown" :key="version.version">
              <td>{{ version.version }}</td>
              <td>{{ version.count }}</td>
              <td>{{ version.activeCount24h }}</td>
            </tr>
          </tbody>
        </table>
      </article>
    </div>

    <article class="panel">
      <header class="panel-header tight">
        <div>
          <p class="panel-kicker">Authorization Drift</p>
          <h2>授权 / 安装差异</h2>
        </div>
        <div class="filter-row">
          <label class="checkbox-field compact-checkbox">
            <input v-model="onlyUnassigned" type="checkbox">
            <span>只看未分组</span>
          </label>
          <label class="checkbox-field compact-checkbox">
            <input v-model="onlyAuthorizationDrift" type="checkbox">
            <span>只看有授权漂移</span>
          </label>
        </div>
      </header>
      <table v-if="filteredIssueRows.length > 0">
        <thead>
          <tr>
            <th>站点</th>
            <th>分组</th>
            <th>目标分组</th>
            <th>客户端版本</th>
            <th>最近活跃</th>
            <th>已授权未安装</th>
            <th>已安装未授权</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
            <tr
              v-for="item in filteredIssueRows"
              :key="item.siteId"
              :class="{ 'highlight-row': isIssueRowHighlighted(item.siteId, highlightedSiteId ?? '') }"
            >
            <td>{{ item.siteName }}</td>
            <td>{{ item.groupName }}</td>
            <td>
              <select
                :value="quickAssignSelections[item.siteId] ?? ''"
                @change="updateQuickAssignSelection(item.siteId, ($event.target as HTMLSelectElement).value)"
              >
                <option value="">请选择分组</option>
                <option v-for="group in groups" :key="group.id" :value="group.id">
                  {{ group.groupName }} / {{ group.groupCode }}
                </option>
              </select>
            </td>
            <td>{{ item.clientVersion }}</td>
            <td>{{ formatDate(item.lastSeenAt) }}</td>
            <td>{{ item.authorizedNotInstalled.join('、') || '无' }}</td>
            <td>{{ item.installedNotAuthorized.join('、') || '无' }}</td>
            <td class="action-cell">
              <button
                type="button"
                class="ghost-button inline-button"
                :disabled="!(quickAssignSelections[item.siteId] ?? '').trim()"
                @click="emit('quickAssignGroup', { siteId: item.siteId, groupId: quickAssignSelections[item.siteId] ?? '' })"
              >
                分配分组
              </button>
              <button type="button" class="ghost-button inline-button" @click="emit('openSite', item.siteId)">去站点管理</button>
              <button type="button" class="ghost-button inline-button" @click="emit('openSiteOverride', item.siteId)">去站点覆盖</button>
            </td>
          </tr>
        </tbody>
      </table>
      <p v-if="filteredIssueRows.length === 0" class="empty">当前筛选条件下没有问题站点。</p>
    </article>
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { SiteAnalyticsSummary, SiteGroup } from '@/contracts/site-types'
import { filterIssueRows, isIssueRowHighlighted } from '@/services/siteAnalyticsViewService'

const emit = defineEmits<{
  openSite: [siteId: string]
  openSiteOverride: [siteId: string]
  quickAssignGroup: [payload: { siteId: string; groupId: string }]
}>()

const props = defineProps<{
  analytics: SiteAnalyticsSummary
  groups: SiteGroup[]
  highlightedSiteId?: string
}>()

const quickAssignSelections = reactive<Record<string, string>>({})
const onlyUnassigned = ref(false)
const onlyAuthorizationDrift = ref(false)

const filteredIssueRows = computed(() => filterIssueRows(props.analytics.issueRows, {
  onlyUnassigned: onlyUnassigned.value,
  onlyAuthorizationDrift: onlyAuthorizationDrift.value,
}))

watch(
  () => filteredIssueRows.value,
  (items) => {
    const siteIds = new Set(items.map((item) => item.siteId))

    for (const key of Object.keys(quickAssignSelections)) {
      if (!siteIds.has(key)) {
        delete quickAssignSelections[key]
      }
    }
  },
  { immediate: true },
)

function updateQuickAssignSelection(siteId: string, groupId: string): void {
  quickAssignSelections[siteId] = groupId
}

function formatDate(value: string | null): string {
  if (!value) {
    return '尚未上报'
  }

  return new Date(value).toLocaleString('zh-CN', { hour12: false })
}
</script>

<style scoped>
.action-cell {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.filter-row {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.compact-checkbox {
  gap: 8px;
}

.highlight-row td {
  background: #edf9f2;
  transition: background-color 0.3s ease;
}
</style>
