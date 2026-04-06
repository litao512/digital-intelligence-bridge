<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Sites</p>
        <h2>站点管理</h2>
      </div>
      <span class="panel-count">{{ sites.length }} 个站点</span>
    </header>

    <p class="form-tip">
      每条记录代表一个 DIB 安装实例。调整所属分组后，后续按站点生成的插件授权将以最新分组为准。
    </p>

    <div v-if="sites.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>站点</th>
            <th>站点标识</th>
            <th>所属分组</th>
            <th>客户端版本</th>
            <th>最近活跃</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="site in sites" :key="site.id">
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
            <td>{{ site.clientVersion }}</td>
            <td>{{ formatDate(site.lastSeenAt) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else class="empty">当前还没有站点接入记录。</p>
  </section>
</template>

<script setup lang="ts">
import type { SiteGroup, SiteSummary } from '@/contracts/site-types'

defineProps<{
  sites: SiteSummary[]
  groups: SiteGroup[]
}>()

const emit = defineEmits<{
  assignGroup: [payload: { siteRowId: string; groupId: string | null }]
}>()

function onGroupChange(siteRowId: string, groupId: string | null): void {
  emit('assignGroup', { siteRowId, groupId })
}

function formatDate(value: string | null): string {
  if (!value) {
    return '尚未上报'
  }

  return new Date(value).toLocaleString('zh-CN', { hour12: false })
}
</script>
