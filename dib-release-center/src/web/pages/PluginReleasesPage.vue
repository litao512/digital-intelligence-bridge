<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Plugin Releases</p>
        <h2>插件版本</h2>
      </div>
      <span class="panel-count">{{ versions.length }} 条记录</span>
    </header>

    <form class="release-form" @submit.prevent>
      <div class="field-grid">
        <label>
          <span>插件 ID</span>
          <input :value="draft.pluginId" readonly>
        </label>
        <label>
          <span>版本号</span>
          <input :value="draft.version" readonly>
        </label>
        <label>
          <span>渠道</span>
          <input :value="draft.channel" readonly>
        </label>
        <label>
          <span>DIB 最低版本</span>
          <input :value="draft.dibMinVersion" readonly>
        </label>
        <label>
          <span>资产 URL</span>
          <input :value="draft.packageUrl" readonly>
        </label>
      </div>
      <p class="form-tip">第一阶段先展示只读最小发布表单，下一步接入新增/发布动作。</p>
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
import { computed } from 'vue'
import type { PluginVersion } from '@/contracts/release-types'

const props = defineProps<{
  versions: PluginVersion[]
}>()

const draft = computed(() => {
  const first = props.versions[0]
  if (!first) {
    return {
      pluginId: '',
      version: '',
      channel: '',
      dibMinVersion: '',
      packageUrl: '',
    }
  }

  return {
    pluginId: first.pluginCode,
    version: first.version,
    channel: first.channelCode,
    dibMinVersion: first.dibMinVersion,
    packageUrl: first.packageUrl,
  }
})
</script>
