<template>
  <main class="shell">
    <ReleaseAuthPanel
      v-if="isSupabaseConfigured() && (!sessionReady || !isAuthenticatedAdmin)"
      :message="authMessage"
      @submit="handleSignIn"
    />

    <template v-else>
      <section class="shell-header">
        <div class="shell-header-main">
          <p class="eyebrow">Digital Intelligence Bridge</p>
          <h1>DIB 发布中心</h1>
          <p class="description">
            第一阶段已接入 prod101 的 Supabase 元数据结构，当前页面聚焦发布渠道、插件版本、资产登记与 manifest 发布的最小闭环。
          </p>
        </div>
        <div class="shell-header-side">
          <div class="status-pill">
            <span>连接状态</span>
            <strong>{{ connectionMessage }}</strong>
          </div>
          <div class="status-pill">
            <span>当前账号</span>
            <strong>{{ currentAdminSummary }}</strong>
          </div>
          <div class="status-pill subtle">
            <span>环境变量</span>
            <strong>{{ envMessage }}</strong>
          </div>
          <button type="button" class="ghost-button inline-button" @click="handleSignOut">退出登录</button>
        </div>
      </section>

      <section class="metric-strip" aria-label="release center overview">
        <article class="metric-pill">
          <span>渠道</span>
          <strong>{{ channels.length }}</strong>
        </article>
        <article class="metric-pill">
          <span>插件版本</span>
          <strong>{{ pluginVersions.length }}</strong>
        </article>
        <article class="metric-pill">
          <span>客户端版本</span>
          <strong>{{ clientVersions.length }}</strong>
        </article>
        <article class="metric-pill">
          <span>发布资产</span>
          <strong>{{ releaseAssets.length }}</strong>
        </article>
      </section>

      <section v-if="statusMessage" class="banner success">{{ statusMessage }}</section>
      <section v-if="loadError" class="banner error">{{ loadError }}</section>
      <section v-else-if="isLoading" class="banner">正在加载发布中心数据...</section>

      <nav class="tabbar" aria-label="release center tabs">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          type="button"
          class="tab"
          :class="{ active: activeTab === tab.id }"
          @click="activeTab = tab.id"
        >
          {{ tab.label }}
        </button>
      </nav>

      <section class="workspace">
        <ChannelsPage v-if="activeTab === 'channels'" :channels="channels" />
        <SitesPage
        v-else-if="activeTab === 'sites'"
        :sites="sites"
        :groups="siteGroups"
        :search-seed="siteSearchSeed"
        @assign-group="handleAssignSiteGroup"
        @bulk-assign-group="handleBulkAssignSiteGroup"
      />
      <GroupPoliciesPage
        v-else-if="activeTab === 'group-policies'"
        :groups="siteGroups"
        :packages="pluginPackages"
        :policies="groupPluginPolicies"
        :selected-group-id="selectedPolicyGroupId"
        @select-group="selectedPolicyGroupId = $event"
        @submit="handleUpsertGroupPolicy"
        @delete="handleDeleteGroupPolicy"
      />
      <SiteOverridesPage
        v-else-if="activeTab === 'site-overrides'"
        :sites="sites"
        :packages="pluginPackages"
        :overrides="sitePluginOverrides"
        :selected-site-row-id="selectedOverrideSiteRowId"
        @select-site="selectedOverrideSiteRowId = $event"
        @submit="handleUpsertSiteOverride"
        @delete="handleDeleteSiteOverride"
      />
      <SiteAnalyticsPage
        v-else-if="activeTab === 'site-analytics'"
        :analytics="siteAnalytics"
        :groups="siteGroups"
        :highlighted-site-id="highlightedAnalyticsSiteId"
        @open-site="handleOpenAnalyticsSite"
        @open-site-override="handleOpenAnalyticsSiteOverride"
        @quick-assign-group="handleQuickAssignAnalyticsGroup"
      />
      <PluginReleasesPage
        v-else-if="activeTab === 'plugins'"
        :versions="pluginVersions"
        :channels="channels"
        :packages="pluginPackages"
        @submit="handleCreatePluginVersion"
        @create-package="handleCreatePluginPackage"
        @delete-version="handleDeletePluginVersion"
        @delete-package="handleDeletePluginPackage"
      />
      <ClientReleasesPage
        v-else-if="activeTab === 'clients'"
        :versions="clientVersions"
        :channels="channels"
        @submit="handleCreateClientVersion"
        @delete-version="handleDeleteClientVersion"
      />
      <ReleaseAssetsPage
        v-else
        :assets="releaseAssets"
        @submit="handleCreateReleaseAsset"
        @upload="handleUploadReleaseAsset"
        @delete="handleDeleteReleaseAsset"
      />
      </section>

      <section class="manifest-panel panel">
        <button
          type="button"
          class="manifest-toggle"
          :aria-expanded="isManifestPanelOpen"
          @click="isManifestPanelOpen = !isManifestPanelOpen"
        >
          <div>
            <p class="panel-kicker">Manifest Workspace</p>
            <h2>清单发布与预览</h2>
            <p class="subline">
              仅在需要确认渠道输出、刷新预览或发布最新 manifest 时展开，避免打断站点与授权类高频操作。
            </p>
          </div>
          <span class="toggle-indicator">{{ isManifestPanelOpen ? '收起' : '展开' }}</span>
        </button>

        <div v-if="isManifestPanelOpen" class="manifest-panel-body">
          <section class="preview-bar">
            <label>
              <span>manifest 预览渠道</span>
              <select v-model="previewChannelCode">
                <option v-for="item in channels" :key="item.id" :value="item.channelCode">
                  {{ item.channelName }} / {{ item.channelCode }}
                </option>
              </select>
            </label>
            <button type="button" class="ghost-button" @click="refreshPreview">刷新预览</button>
            <button type="button" @click="publishManifest">发布当前渠道 manifest</button>
          </section>

          <section class="manifest-grid">
            <article class="panel manifest-card">
              <header class="panel-header tight">
                <div>
                  <p class="panel-kicker">Client Manifest</p>
                  <h2>客户端清单预览</h2>
                </div>
              </header>
              <pre>{{ clientManifestText }}</pre>
            </article>
            <article class="panel manifest-card">
              <header class="panel-header tight">
                <div>
                  <p class="panel-kicker">Plugin Manifest</p>
                  <h2>插件清单预览</h2>
                </div>
              </header>
              <pre>{{ pluginManifestText }}</pre>
            </article>
          </section>
        </div>
      </section>
    </template>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import type { Session } from '@supabase/supabase-js'
import type { ClientVersion, PluginPackage, PluginVersion, ReleaseAsset, ReleaseChannel } from '@/contracts/release-types'
import type { SiteAnalyticsSummary, SiteGroup, SiteGroupPluginPolicy, SitePluginOverride, SiteSummary } from '@/contracts/site-types'
import { createClientVersion, deleteClientVersion, listClientVersions } from '@/repositories/clientVersionsRepository'
import { deleteGroupPluginPolicy, listGroupPluginPolicies, upsertGroupPluginPolicy } from '@/repositories/groupPluginPoliciesRepository'
import { listSiteGroups } from '@/repositories/siteGroupsRepository'
import { deleteSitePluginOverride, listSitePluginOverrides, upsertSitePluginOverride } from '@/repositories/sitePluginOverridesRepository'
import { listSites, updateSiteGroup } from '@/repositories/sitesRepository'
import { createPluginPackage, deletePluginPackage, listPluginPackages } from '@/repositories/pluginPackagesRepository'
import { createPluginVersion, deletePluginVersion, listPluginVersions } from '@/repositories/pluginVersionsRepository'
import {
  createReleaseAsset,
  deleteReleaseAsset,
  deleteReleaseAssetObject,
  findReleaseAssetReferences,
  listReleaseAssets,
  upsertReleaseAsset,
  uploadManifestAsset,
  uploadReleaseAssetFile,
} from '@/repositories/releaseAssetsRepository'
import { getCurrentAdminProfile, linkCurrentAdminUser, type ReleaseCenterAdmin } from '@/repositories/releaseCenterAdminsRepository'
import { listReleaseChannels } from '@/repositories/releaseChannelsRepository'
import {
  getCurrentAuthState,
  onReleaseAuthStateChange,
  signInWithPassword,
  signOutReleaseCenter,
} from '@/services/releaseAuthService'
import { isSupabaseConfigured } from '@/services/supabase'
import {
  buildClientVersionInsert,
  buildManifestPreview,
  buildManifestPublishPlan,
  buildPluginPackageInsert,
  buildPluginVersionInsert,
  buildReleaseAssetInsert,
  buildReleaseAssetUploadPlan,
  type ClientVersionDraftInput,
  type PluginPackageDraftInput,
  type PluginVersionDraftInput,
  type ReleaseAssetDraftInput,
  type ReleaseAssetUploadInput,
} from '@/services/releaseDraftService'
import ReleaseAuthPanel from '@/web/components/ReleaseAuthPanel.vue'
import ChannelsPage from '@/web/pages/ChannelsPage.vue'
import ClientReleasesPage from '@/web/pages/ClientReleasesPage.vue'
import PluginReleasesPage from '@/web/pages/PluginReleasesPage.vue'
import ReleaseAssetsPage from '@/web/pages/ReleaseAssetsPage.vue'
import SiteAnalyticsPage from '@/web/pages/SiteAnalyticsPage.vue'
import GroupPoliciesPage from '@/web/pages/GroupPoliciesPage.vue'
import SiteOverridesPage from '@/web/pages/SiteOverridesPage.vue'
import SitesPage from '@/web/pages/SitesPage.vue'
import { aggregateSiteAnalytics } from '@/services/siteAuthorizationService'
import { buildGroupPolicyUpsert, buildSiteOverrideUpsert, type SiteGroupPolicyDraftInput, type SiteOverrideDraftInput } from '@/services/sitePolicyDraftService'
import { buildQuickAssignGroupPayload, findSiteRowBySiteId, getSiteSearchSeed } from '@/services/siteManagementService'

const tabs = [
  { id: 'channels', label: '发布渠道' },
  { id: 'sites', label: '站点管理' },
  { id: 'group-policies', label: '分组授权' },
  { id: 'site-overrides', label: '站点覆盖' },
  { id: 'site-analytics', label: '站点统计' },
  { id: 'plugins', label: '插件版本' },
  { id: 'clients', label: '客户端版本' },
  { id: 'assets', label: '发布资产' },
] as const

type TabId = (typeof tabs)[number]['id']

const activeTab = ref<TabId>('channels')
const isManifestPanelOpen = ref(false)
const isLoading = ref(false)
const loadError = ref('')
const statusMessage = ref('')
const authMessage = ref('')
const sessionReady = ref(false)
const session = ref<Session | null>(null)
const currentAdmin = ref<ReleaseCenterAdmin | null>(null)
const channels = ref<ReleaseChannel[]>([])
const siteGroups = ref<SiteGroup[]>([])
const selectedPolicyGroupId = ref('')
const selectedOverrideSiteRowId = ref('')
const siteSearchSeed = ref('')
const highlightedAnalyticsSiteId = ref('')
const sites = ref<SiteSummary[]>([])
const groupPluginPolicies = ref<SiteGroupPluginPolicy[]>([])
const sitePluginOverrides = ref<SitePluginOverride[]>([])
const pluginPackages = ref<PluginPackage[]>([])
const pluginVersions = ref<PluginVersion[]>([])
const clientVersions = ref<ClientVersion[]>([])
const releaseAssets = ref<ReleaseAsset[]>([])
const previewChannelCode = ref('stable')
let unsubscribeAuth: (() => void) | null = null
let highlightedAnalyticsTimer: ReturnType<typeof setTimeout> | null = null

const isAuthenticatedAdmin = computed(() => Boolean(session.value && currentAdmin.value?.isActive))

const envMessage = computed(() => {
  if (isSupabaseConfigured()) {
    return '已检测到 Supabase 运行时配置，可直接读取 prod101 元数据。'
  }

  return '缺少 VITE_SUPABASE_URL 或 VITE_SUPABASE_ANON_KEY。请参考 .env.example 配置。'
})

const currentAdminSummary = computed(() => {
  if (!isAuthenticatedAdmin.value || !currentAdmin.value) {
    return '未登录发布中心管理员。'
  }

  return `${currentAdmin.value.email}${currentAdmin.value.displayName ? ` / ${currentAdmin.value.displayName}` : ''}`
})

const connectionMessage = computed(() => {
  if (loadError.value) {
    return '连接失败，需先修复配置、认证或数据库权限。'
  }

  if (isLoading.value) {
    return '正在读取发布中心元数据。'
  }

  if (!isSupabaseConfigured()) {
    return '尚未配置 Supabase 客户端，页面处于静态展示模式。'
  }

  if (!isAuthenticatedAdmin.value) {
    return '请先使用管理员账号登录。'
  }

  return '已接入 prod101 Supabase，并通过发布中心管理员鉴权。'
})

const preview = computed(() => {
  try {
    return buildManifestPreview(previewChannelCode.value, pluginVersions.value, clientVersions.value)
  } catch {
    return buildManifestPreview('stable', [], [])
  }
})

const clientManifestText = computed(() => JSON.stringify(preview.value.clientManifest, null, 2))
const pluginManifestText = computed(() => JSON.stringify(preview.value.pluginManifest, null, 2))
const siteAnalytics = computed<SiteAnalyticsSummary>(() => aggregateSiteAnalytics({
  sites: sites.value,
  groups: siteGroups.value,
  groupPolicies: groupPluginPolicies.value,
  siteOverrides: sitePluginOverrides.value,
}))

watch(channels, (items) => {
  if (!items.some((item) => item.channelCode === previewChannelCode.value)) {
    previewChannelCode.value = items[0]?.channelCode ?? 'stable'
  }
})

watch(siteGroups, (items) => {
  if (!items.some((item) => item.id === selectedPolicyGroupId.value)) {
    selectedPolicyGroupId.value = items[0]?.id ?? ''
  }
})

watch(sites, (items) => {
  if (!items.some((item) => item.id === selectedOverrideSiteRowId.value)) {
    selectedOverrideSiteRowId.value = items[0]?.id ?? ''
  }
})

async function refreshAuthState(): Promise<void> {
  if (!isSupabaseConfigured()) {
    sessionReady.value = true
    session.value = null
    currentAdmin.value = null
    return
  }

  const authState = await getCurrentAuthState()
  session.value = authState.session

  if (!authState.session) {
    currentAdmin.value = null
    sessionReady.value = true
    return
  }

  await linkCurrentAdminUser()
  currentAdmin.value = await getCurrentAdminProfile()
  sessionReady.value = true
}

async function loadData(): Promise<void> {
  if (!isSupabaseConfigured() || !isAuthenticatedAdmin.value) {
    channels.value = []
    siteGroups.value = []
    sites.value = []
    groupPluginPolicies.value = []
    sitePluginOverrides.value = []
    pluginPackages.value = []
    pluginVersions.value = []
    clientVersions.value = []
    releaseAssets.value = []
    return
  }

  isLoading.value = true
  loadError.value = ''

  try {
    const [channelData, siteGroupData, siteData, groupPolicyData, siteOverrideData, packageData, pluginVersionData, clientVersionData, assetData] = await Promise.all([
      listReleaseChannels(),
      listSiteGroups(),
      listSites(),
      listGroupPluginPolicies(),
      listSitePluginOverrides(),
      listPluginPackages(),
      listPluginVersions(),
      listClientVersions(),
      listReleaseAssets(),
    ])

    channels.value = channelData
    siteGroups.value = siteGroupData
    sites.value = siteData
    groupPluginPolicies.value = groupPolicyData
    sitePluginOverrides.value = siteOverrideData
    pluginPackages.value = packageData
    pluginVersions.value = pluginVersionData
    clientVersions.value = clientVersionData
    releaseAssets.value = assetData
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '加载发布中心数据失败。'
  } finally {
    isLoading.value = false
  }
}

async function handleSignIn(payload: { email: string; password: string }): Promise<void> {
  authMessage.value = ''
  loadError.value = ''
  statusMessage.value = ''

  try {
    await signInWithPassword(payload.email, payload.password)
    await refreshAuthState()

    if (!currentAdmin.value) {
      await signOutReleaseCenter()
      authMessage.value = '该账号未加入 release_center_admins，无法使用发布中心。'
      return
    }

    statusMessage.value = '发布中心管理员登录成功。'
    await loadData()
  } catch (error) {
    authMessage.value = error instanceof Error ? error.message : '登录失败。'
  }
}

async function handleSignOut(): Promise<void> {
  authMessage.value = ''
  loadError.value = ''
  statusMessage.value = ''

  try {
    await signOutReleaseCenter()
    await refreshAuthState()
    statusMessage.value = '已退出发布中心登录。'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '退出登录失败。'
  }
}

async function handleCreatePluginPackage(draft: PluginPackageDraftInput): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await createPluginPackage(buildPluginPackageInsert(draft))
    statusMessage.value = '插件定义已提交，请在插件版本下拉中确认结果。'
    await loadData()
    activeTab.value = 'plugins'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '新增插件定义失败。'
  }
}

async function handleAssignSiteGroup(payload: { siteRowId: string; groupId: string | null }): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await updateSiteGroup(payload.siteRowId, payload.groupId)
    statusMessage.value = '站点分组已更新。'
    await loadData()
    activeTab.value = 'sites'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '更新站点分组失败。'
  }
}

async function handleBulkAssignSiteGroup(payload: { siteRowIds: string[]; groupId: string }): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await Promise.all(payload.siteRowIds.map((siteRowId) => updateSiteGroup(siteRowId, payload.groupId)))
    statusMessage.value = `已批量更新 ${payload.siteRowIds.length} 个站点的分组。`
    await loadData()
    activeTab.value = 'sites'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '批量更新站点分组失败。'
  }
}

function handleOpenAnalyticsSite(siteId: string): void {
  const site = findSiteRowBySiteId(sites.value, siteId)

  if (!site) {
    loadError.value = `未找到站点 ${siteId}，无法跳转到站点管理。`
    return
  }

  statusMessage.value = ''
  loadError.value = ''
  siteSearchSeed.value = getSiteSearchSeed(site)
  activeTab.value = 'sites'
}

function handleOpenAnalyticsSiteOverride(siteId: string): void {
  const site = findSiteRowBySiteId(sites.value, siteId)

  if (!site) {
    loadError.value = `未找到站点 ${siteId}，无法跳转到站点覆盖。`
    return
  }

  statusMessage.value = ''
  loadError.value = ''
  selectedOverrideSiteRowId.value = site.id
  activeTab.value = 'site-overrides'
}

async function handleQuickAssignAnalyticsGroup(payload: { siteId: string; groupId: string }): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  const assignment = buildQuickAssignGroupPayload(sites.value, payload)

  if (!assignment) {
    loadError.value = `未找到站点 ${payload.siteId} 或目标分组无效，无法快捷分配。`
    return
  }

  try {
    await updateSiteGroup(assignment.siteRowId, assignment.groupId)
    statusMessage.value = '问题站点已快捷分配到目标分组。'
    await loadData()
    setHighlightedAnalyticsSite(payload.siteId)
    activeTab.value = 'site-analytics'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '快捷分配站点分组失败。'
  }
}

function setHighlightedAnalyticsSite(siteId: string): void {
  highlightedAnalyticsSiteId.value = siteId

  if (highlightedAnalyticsTimer) {
    clearTimeout(highlightedAnalyticsTimer)
  }

  highlightedAnalyticsTimer = setTimeout(() => {
    highlightedAnalyticsSiteId.value = ''
    highlightedAnalyticsTimer = null
  }, 5000)
}

async function handleUpsertGroupPolicy(draft: SiteGroupPolicyDraftInput): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await upsertGroupPluginPolicy(buildGroupPolicyUpsert(draft))
    statusMessage.value = '分组默认授权已保存。'
    await loadData()
    activeTab.value = 'group-policies'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '保存分组授权失败。'
  }
}

async function handleDeleteGroupPolicy(payload: { groupId: string; pluginPackageId: string }): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await deleteGroupPluginPolicy(payload.groupId, payload.pluginPackageId)
    statusMessage.value = '分组默认授权已删除。'
    await loadData()
    activeTab.value = 'group-policies'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '删除分组授权失败。'
  }
}

async function handleUpsertSiteOverride(draft: SiteOverrideDraftInput): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await upsertSitePluginOverride(buildSiteOverrideUpsert(draft))
    statusMessage.value = '站点覆盖已保存。'
    await loadData()
    activeTab.value = 'site-overrides'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '保存站点覆盖失败。'
  }
}

async function handleDeleteSiteOverride(payload: { siteRowId: string; pluginPackageId: string }): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await deleteSitePluginOverride(payload.siteRowId, payload.pluginPackageId)
    statusMessage.value = '站点覆盖已删除。'
    await loadData()
    activeTab.value = 'site-overrides'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '删除站点覆盖失败。'
  }
}

async function handleCreatePluginVersion(draft: PluginVersionDraftInput): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await createPluginVersion(buildPluginVersionInsert(draft))
    statusMessage.value = '插件版本已提交，请在列表和 manifest 预览中确认结果。'
    await loadData()
    activeTab.value = 'plugins'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '新增插件版本失败。'
  }
}

async function handleDeletePluginVersion(version: PluginVersion): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await deletePluginVersion(version.id)
    statusMessage.value = `插件版本 ${version.pluginCode} ${version.version} 已删除。请按需要重新发布 manifest。`
    await loadData()
    activeTab.value = 'plugins'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '删除插件版本失败。'
  }
}

async function handleDeletePluginPackage(pluginPackage: PluginPackage): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await deletePluginPackage(pluginPackage.id)
    statusMessage.value = `插件定义 ${pluginPackage.pluginCode} 已删除。请按需要重新发布 manifest。`
    await loadData()
    activeTab.value = 'plugins'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '删除插件定义失败。'
  }
}

async function handleCreateClientVersion(draft: ClientVersionDraftInput): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await createClientVersion(buildClientVersionInsert(draft))
    statusMessage.value = '客户端版本已提交，请在列表和 manifest 预览中确认结果。'
    await loadData()
    activeTab.value = 'clients'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '新增客户端版本失败。'
  }
}

async function handleDeleteClientVersion(version: ClientVersion): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await deleteClientVersion(version.id)
    statusMessage.value = `客户端版本 ${version.version} 已删除。请按需要重新发布 manifest。`
    await loadData()
    activeTab.value = 'clients'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '删除客户端版本失败。'
  }
}

async function handleCreateReleaseAsset(draft: ReleaseAssetDraftInput): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    await createReleaseAsset(buildReleaseAssetInsert(draft))
    statusMessage.value = '发布资产元数据已登记。'
    await loadData()
    activeTab.value = 'assets'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '新增发布资产失败。'
  }
}

async function handleDeleteReleaseAsset(payload: { asset: ReleaseAsset; removeObject: boolean }): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    const references = await findReleaseAssetReferences(payload.asset.id)
    if (references.pluginVersions.length > 0 || references.clientVersions.length > 0) {
      const pluginSummary = references.pluginVersions
        .map((item) => `${item.pluginCode} ${item.version} / ${item.channelCode}`)
        .join('；')
      const clientSummary = references.clientVersions
        .map((item) => `${item.version} / ${item.channelCode}`)
        .join('；')
      loadError.value = [
        '该发布资产仍被版本记录引用，请先删除引用它的版本。',
        pluginSummary ? `插件版本：${pluginSummary}` : '',
        clientSummary ? `客户端版本：${clientSummary}` : '',
      ].filter(Boolean).join(' ')
      activeTab.value = 'assets'
      return
    }

    if (payload.removeObject) {
      await deleteReleaseAssetObject(payload.asset.bucketName, payload.asset.storagePath)
    }

    await deleteReleaseAsset(payload.asset.id)
    statusMessage.value = payload.removeObject
      ? `发布资产 ${payload.asset.fileName} 的记录和 Storage 文件已删除。`
      : `发布资产 ${payload.asset.fileName} 的记录已删除，Storage 文件已保留。`
    await loadData()
    activeTab.value = 'assets'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '删除发布资产失败。'
  }
}

async function handleUploadReleaseAsset(draft: ReleaseAssetUploadInput): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    const plan = await buildReleaseAssetUploadPlan(draft)
    await uploadReleaseAssetFile(plan.payload.bucket_name, plan.payload.storage_path, plan.file)
    await createReleaseAsset(plan.payload)
    statusMessage.value = '发布资产文件已上传，并完成元数据登记。'
    await loadData()
    activeTab.value = 'assets'
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '上传发布资产失败。'
  }
}

async function publishManifest(): Promise<void> {
  statusMessage.value = ''
  loadError.value = ''

  try {
    const plan = await buildManifestPublishPlan(previewChannelCode.value, pluginVersions.value, clientVersions.value)
    for (const asset of plan.assets) {
      await uploadManifestAsset(asset.payload.bucket_name, asset.storagePath, asset.content)
      await upsertReleaseAsset(asset.payload)
    }

    statusMessage.value = `${plan.channelCode} 渠道 manifest 已发布到 Storage，并同步写入 release_assets。`
    await loadData()
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '发布 manifest 失败。'
  }
}

function refreshPreview(): void {
  statusMessage.value = `已刷新 ${previewChannelCode.value} 渠道的 manifest 预览。`
}

onMounted(async () => {
  try {
    await refreshAuthState()
    await loadData()
  } catch (error) {
    authMessage.value = error instanceof Error ? error.message : '初始化登录状态失败。'
  }

  unsubscribeAuth = onReleaseAuthStateChange(async (state) => {
    session.value = state.session
    if (!state.session) {
      currentAdmin.value = null
      sessionReady.value = true
      return
    }

    try {
      await refreshAuthState()
      await loadData()
    } catch (error) {
      authMessage.value = error instanceof Error ? error.message : '刷新登录状态失败。'
    }
  })
})

onUnmounted(() => {
  unsubscribeAuth?.()
  if (highlightedAnalyticsTimer) {
    clearTimeout(highlightedAnalyticsTimer)
  }
})
</script>

<style scoped>
:global(*) {
  box-sizing: border-box;
}

:global(body) {
  margin: 0;
  min-width: 320px;
  min-height: 100vh;
  font-family: 'Segoe UI', 'PingFang SC', 'Microsoft YaHei', sans-serif;
  color: #132238;
  background:
    radial-gradient(circle at top left, rgba(7, 114, 201, 0.12), transparent 34%),
    radial-gradient(circle at bottom right, rgba(11, 163, 96, 0.1), transparent 28%),
    linear-gradient(180deg, #f6fbff 0%, #eef6fb 100%);
}

:global(#app) {
  min-height: 100vh;
}

:global(code),
:global(pre) {
  font-family: 'Cascadia Code', 'Consolas', monospace;
}

:global(table) {
  width: 100%;
  border-collapse: collapse;
}

:global(th),
:global(td) {
  padding: 14px 16px;
  text-align: left;
  border-bottom: 1px solid #d8e3ee;
  vertical-align: top;
}

:global(th) {
  font-size: 0.82rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #52708f;
}

:global(input),
:global(select),
:global(textarea) {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #c7d7e7;
  border-radius: 12px;
  background: #f8fbfd;
  color: #17324d;
}

:global(textarea) {
  resize: vertical;
}

:global(label span) {
  display: block;
  margin-bottom: 8px;
  font-size: 0.86rem;
  color: #47637f;
}

:global(button) {
  padding: 11px 18px;
  border: 0;
  border-radius: 999px;
  background: linear-gradient(135deg, #0a7ac9 0%, #0b9f79 100%);
  color: #fff;
  cursor: pointer;
}

.shell {
  width: min(1280px, 100%);
  margin: 0 auto;
  padding: 40px 24px 56px;
}

.shell-header,
.metric-strip,
.manifest-panel {
  border: 1px solid #d4e2ef;
  border-radius: 24px;
  background: rgba(255, 255, 255, 0.9);
  box-shadow: 0 20px 50px rgba(37, 83, 125, 0.06);
}

.shell-header {
  display: grid;
  grid-template-columns: minmax(0, 1.6fr) minmax(280px, 0.9fr);
  gap: 16px;
  padding: 22px 24px;
}

.shell-header-side {
  display: flex;
  flex-direction: column;
  gap: 10px;
  align-items: stretch;
  justify-content: center;
}

.eyebrow {
  margin: 0 0 12px;
  font-size: 0.85rem;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: #0a6ab7;
}

h1 {
  margin: 0;
  font-size: clamp(2.4rem, 7vw, 4.1rem);
  line-height: 1.02;
  color: #10263f;
}

.description {
  margin: 12px 0 0;
  max-width: 48rem;
  line-height: 1.7;
  color: #4f6883;
}

.status-pill,
.metric-pill,
.panel {
  border: 1px solid #d4e2ef;
  border-radius: 22px;
  background: rgba(255, 255, 255, 0.9);
  box-shadow: 0 20px 50px rgba(37, 83, 125, 0.06);
}

.status-pill {
  padding: 14px 16px;
  background: linear-gradient(180deg, rgba(246, 251, 255, 0.95) 0%, rgba(255, 255, 255, 0.9) 100%);
}

.status-pill span,
.metric-pill span {
  display: block;
  font-size: 0.82rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #5d7b96;
}

.status-pill strong,
.metric-pill strong {
  display: block;
  margin-top: 6px;
  line-height: 1.5;
  color: #103253;
}

.status-pill.subtle {
  background: rgba(244, 249, 252, 0.9);
}

.metric-strip {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
  margin-top: 16px;
  padding: 14px;
}

.metric-pill {
  min-width: 140px;
  padding: 12px 16px;
  border-radius: 18px;
  background: #f7fbfe;
  box-shadow: none;
}

.metric-pill strong {
  font-size: 1.5rem;
}

.workspace {
  margin-top: 20px;
}

.manifest-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.inline-button {
  padding: 8px 14px;
  align-self: flex-start;
}

.tabbar {
  display: flex;
  gap: 12px;
  margin: 24px 0 16px;
}

.tab {
  padding: 12px 18px;
  border: 1px solid #c8d9e8;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.78);
  color: #305171;
}

.tab.active {
  border-color: #0a7ac9;
  background: linear-gradient(135deg, #0a7ac9 0%, #0b9f79 100%);
  color: #fff;
}

.manifest-panel {
  margin-top: 20px;
  padding: 18px 20px;
}

.manifest-toggle {
  display: flex;
  width: 100%;
  justify-content: space-between;
  gap: 16px;
  align-items: flex-start;
  padding: 0;
  border: 0;
  border-radius: 0;
  background: transparent;
  color: inherit;
  text-align: left;
}

.manifest-toggle h2 {
  margin: 0;
  font-size: 1.2rem;
  color: #0f2c49;
}

.toggle-indicator {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 72px;
  padding: 10px 14px;
  border-radius: 999px;
  background: #e9f4fb;
  color: #0a5f99;
  font-weight: 600;
}

.manifest-panel-body {
  margin-top: 18px;
  padding-top: 18px;
  border-top: 1px solid #d8e3ee;
}

.banner {
  margin-bottom: 16px;
  padding: 14px 18px;
  border-radius: 16px;
  background: #eff7fd;
  color: #0c5d9c;
}

.banner.error {
  background: #fff1f0;
  color: #bf3d36;
}

.banner.success {
  background: #edf9f2;
  color: #0b7b55;
}

.preview-bar {
  display: flex;
  gap: 12px;
  align-items: end;
  margin-bottom: 16px;
}

.ghost-button {
  background: #e9f4fb;
  color: #0a5f99;
}

.panel {
  padding: 24px;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: flex-start;
  margin-bottom: 18px;
}

.panel-header.tight {
  margin-bottom: 12px;
}

.panel-kicker {
  margin: 0 0 8px;
  font-size: 0.8rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: #6e89a1;
}

.panel-header h2 {
  margin: 0;
  font-size: 1.4rem;
  color: #0f2c49;
}

.panel-count {
  color: #5e7690;
}

.table-wrap {
  overflow-x: auto;
}

.release-form {
  margin-bottom: 20px;
  padding: 18px;
  border-radius: 18px;
  background: #f4f9fc;
}

.field-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.field-grid-wide {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

:global(.page-filter-grid) {
  margin-top: 16px;
}

:global(.page-filter-row) {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
  margin-top: 16px;
}

:global(.page-filter-actions) {
  justify-content: flex-start;
  padding-top: 4px;
}

:global(.page-empty) {
  padding: 16px 18px;
  border: 1px dashed #c9d9e8;
  border-radius: 16px;
  background: #f7fbfe;
}

.toggle-row,
.form-actions {
  display: flex;
  gap: 16px;
  align-items: center;
  margin-top: 16px;
}

.form-tip,
.empty,
.subline {
  margin: 12px 0 0;
  color: #647b93;
}

.subline {
  margin-top: 6px;
  font-size: 0.88rem;
  word-break: break-all;
}

.manifest-card pre {
  margin: 0;
  min-height: 220px;
  overflow: auto;
  padding: 16px;
  border-radius: 16px;
  background: #0f1f33;
  color: #dbe9f5;
}

@media (max-width: 920px) {
  .shell-header,
  .manifest-grid,
  .field-grid,
  .field-grid-wide {
    grid-template-columns: 1fr;
  }

  .tabbar,
  .metric-strip,
  .preview-bar,
  .toggle-row,
  .form-actions {
    flex-wrap: wrap;
  }
}
</style>

