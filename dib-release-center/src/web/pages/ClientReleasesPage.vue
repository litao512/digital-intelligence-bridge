<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Client Releases</p>
        <h2>DIB 客户端版本</h2>
      </div>
      <span class="panel-count">{{ versions.length }} 条记录</span>
    </header>

    <form class="release-form" @submit.prevent>
      <div class="field-grid">
        <label>
          <span>版本号</span>
          <input :value="draft.version" readonly>
        </label>
        <label>
          <span>渠道</span>
          <input :value="draft.channel" readonly>
        </label>
        <label>
          <span>最低升级版本</span>
          <input :value="draft.minUpgradeVersion" readonly>
        </label>
        <label>
          <span>资产 URL</span>
          <input :value="draft.packageUrl" readonly>
        </label>
      </div>
      <p class="form-tip">当前为第一阶段只读展示，下一步接入客户端发布录入与 manifest 发布。</p>
    </form>

    <div v-if="versions.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>版本</th>
            <th>渠道</th>
            <th>最低升级版本</th>
            <th>发布</th>
            <th>强制</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in versions" :key="item.id">
            <td>{{ item.version }}</td>
            <td>{{ item.channelCode }}</td>
            <td>{{ item.minUpgradeVersion }}</td>
            <td>{{ item.isPublished ? '已发布' : '草稿' }}</td>
            <td>{{ item.isMandatory ? '是' : '否' }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else class="empty">当前没有客户端版本记录。</p>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ClientVersion } from '@/contracts/release-types'

const props = defineProps<{
  versions: ClientVersion[]
}>()

const draft = computed(() => {
  const first = props.versions[0]
  if (!first) {
    return {
      version: '',
      channel: '',
      minUpgradeVersion: '',
      packageUrl: '',
    }
  }

  return {
    version: first.version,
    channel: first.channelCode,
    minUpgradeVersion: first.minUpgradeVersion,
    packageUrl: first.packageUrl,
  }
})
</script>
