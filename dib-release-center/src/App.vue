<template>
  <main class="shell">
    <section class="hero">
      <div>
        <p class="eyebrow">Digital Intelligence Bridge</p>
        <h1>DIB 发布中心</h1>
        <p class="description">
          第一阶段已接入 prod101 的 Supabase 元数据结构，当前页面聚焦发布渠道、插件版本和客户端版本的最小闭环展示。
        </p>
      </div>
      <div class="hero-side">
        <div class="metric-card">
          <span>渠道</span>
          <strong>{{ channels.length }}</strong>
        </div>
        <div class="metric-card">
          <span>插件版本</span>
          <strong>{{ pluginVersions.length }}</strong>
        </div>
        <div class="metric-card">
          <span>客户端版本</span>
          <strong>{{ clientVersions.length }}</strong>
        </div>
      </div>
    </section>

    <section class="status-grid">
      <article class="status-card">
        <h2>连接状态</h2>
        <p>{{ connectionMessage }}</p>
      </article>
      <article class="status-card">
        <h2>环境变量</h2>
        <p>{{ envMessage }}</p>
      </article>
      <article class="status-card">
        <h2>目标库</h2>
        <p>prod101 Supabase / schema: <code>dib_release</code></p>
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

    <ChannelsPage v-if="activeTab === 'channels'" :channels="channels" />
    <PluginReleasesPage
      v-else-if="activeTab === 'plugins'"
      :versions="pluginVersions"
      :channels="channels"
      :packages="pluginPackages"
      @submit="handleCreatePluginVersion"
    />
    <ClientReleasesPage
      v-else
      :versions="clientVersions"
      :channels="channels"
      @submit="handleCreateClientVersion"
    />
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import type { ClientVersion, PluginPackage, PluginVersion, ReleaseChannel } from '@/contracts/release-types'
import { createClientVersion, listClientVersions } from '@/repositories/clientVersionsRepository'
import { listPluginPackages } from '@/repositories/pluginPackagesRepository'
import { createPluginVersion, listPluginVersions } from '@/repositories/pluginVersionsRepository'
import { listReleaseChannels } from '@/repositories/releaseChannelsRepository'
import { isSupabaseConfigured } from '@/services/supabase'
import {
  buildClientVersionInsert,
  buildManifestPreview,
  buildPluginVersionInsert,
  type ClientVersionDraftInput,
  type PluginVersionDraftInput,
} from '@/services/releaseDraftService'
import ChannelsPage from '@/web/pages/ChannelsPage.vue'
import ClientReleasesPage from '@/web/pages/ClientReleasesPage.vue'
import PluginReleasesPage from '@/web/pages/PluginReleasesPage.vue'

const tabs = [
  { id: 'channels', label: '发布渠道' },
  { id: 'plugins', label: '插件版本' },
  { id: 'clients', label: '客户端版本' },
] as const

type TabId = (typeof tabs)[number]['id']

const activeTab = ref<TabId>('channels')
const isLoading = ref(false)
const loadError = ref('')
const statusMessage = ref('')
const channels = ref<ReleaseChannel[]>([])
const pluginPackages = ref<PluginPackage[]>([])
const pluginVersions = ref<PluginVersion[]>([])
const clientVersions = ref<ClientVersion[]>([])
const previewChannelCode = ref('stable')

const envMessage = computed(() => {
  if (isSupabaseConfigured()) {
    return '已检测到 Supabase 运行时配置，可直接读取 prod101 元数据。'
  }

  return '缺少 VITE_SUPABASE_URL 或 VITE_SUPABASE_ANON_KEY。请参考 .env.example 配置。'
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

  return '已接入 prod101 Supabase，可读取和提交发布中心元数据。'
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

watch(channels, (items) => {
  if (!items.some((item) => item.channelCode === previewChannelCode.value)) {
    previewChannelCode.value = items[0]?.channelCode ?? 'stable'
  }
})

async function loadData(): Promise<void> {
  if (!isSupabaseConfigured()) {
    channels.value = []
    pluginPackages.value = []
    pluginVersions.value = []
    clientVersions.value = []
    return
  }

  isLoading.value = true
  loadError.value = ''

  try {
    const [channelData, packageData, pluginVersionData, clientVersionData] = await Promise.all([
      listReleaseChannels(),
      listPluginPackages(),
      listPluginVersions(),
      listClientVersions(),
    ])

    channels.value = channelData
    pluginPackages.value = packageData
    pluginVersions.value = pluginVersionData
    clientVersions.value = clientVersionData
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '加载发布中心数据失败。'
  } finally {
    isLoading.value = false
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

function refreshPreview(): void {
  statusMessage.value = `已刷新 ${previewChannelCode.value} 渠道的 manifest 预览。`
}

onMounted(() => {
  void loadData()
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

.hero {
  display: grid;
  grid-template-columns: minmax(0, 1.5fr) minmax(280px, 0.9fr);
  gap: 20px;
  padding: 28px;
  border: 1px solid #d4e2ef;
  border-radius: 28px;
  background: rgba(255, 255, 255, 0.88);
  box-shadow: 0 24px 60px rgba(33, 74, 112, 0.08);
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
  margin: 18px 0 0;
  max-width: 44rem;
  line-height: 1.8;
  color: #4f6883;
}

.hero-side {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
  align-content: start;
}

.metric-card,
.status-card,
.panel {
  border: 1px solid #d4e2ef;
  border-radius: 22px;
  background: rgba(255, 255, 255, 0.9);
  box-shadow: 0 20px 50px rgba(37, 83, 125, 0.06);
}

.metric-card {
  padding: 18px;
}

.metric-card span {
  display: block;
  font-size: 0.82rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #5d7b96;
}

.metric-card strong {
  display: block;
  margin-top: 10px;
  font-size: 2rem;
  color: #103253;
}

.status-grid,
.manifest-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
  margin-top: 20px;
}

.manifest-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
  margin-bottom: 20px;
}

.status-card {
  padding: 18px 20px;
}

.status-card h2 {
  margin: 0 0 10px;
  font-size: 1rem;
  color: #113251;
}

.status-card p {
  margin: 0;
  line-height: 1.7;
  color: #5a7087;
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

.textarea-field {
  display: block;
  margin-top: 16px;
}

.toggle-row,
.form-actions {
  display: flex;
  gap: 16px;
  align-items: center;
  margin-top: 16px;
}

.checkbox-field {
  display: inline-flex;
  gap: 10px;
  align-items: center;
}

.checkbox-field input {
  width: auto;
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
  .hero,
  .status-grid,
  .manifest-grid,
  .field-grid,
  .field-grid-wide {
    grid-template-columns: 1fr;
  }

  .hero-side {
    grid-template-columns: 1fr;
  }

  .tabbar,
  .preview-bar,
  .toggle-row,
  .form-actions {
    flex-wrap: wrap;
  }
}
</style>
