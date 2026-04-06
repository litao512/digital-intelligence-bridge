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
      </header>
      <table>
        <thead>
          <tr>
            <th>站点</th>
            <th>分组</th>
            <th>客户端版本</th>
            <th>最近活跃</th>
            <th>已授权未安装</th>
            <th>已安装未授权</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in analytics.authorizationDrift" :key="item.siteId">
            <td>{{ item.siteName }}</td>
            <td>{{ item.groupName }}</td>
            <td>{{ item.clientVersion }}</td>
            <td>{{ formatDate(item.lastSeenAt) }}</td>
            <td>{{ item.authorizedNotInstalled.join('、') || '无' }}</td>
            <td>{{ item.installedNotAuthorized.join('、') || '无' }}</td>
          </tr>
        </tbody>
      </table>
    </article>
  </section>
</template>

<script setup lang="ts">
import type { SiteAnalyticsSummary } from '@/contracts/site-types'

defineProps<{
  analytics: SiteAnalyticsSummary
}>()

function formatDate(value: string | null): string {
  if (!value) {
    return '尚未上报'
  }

  return new Date(value).toLocaleString('zh-CN', { hour12: false })
}
</script>
