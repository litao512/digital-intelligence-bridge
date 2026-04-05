<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Channels</p>
        <h2>发布渠道</h2>
      </div>
      <span class="panel-count">{{ channels.length }} 个渠道</span>
    </header>

    <div v-if="channels.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>渠道</th>
            <th>名称</th>
            <th>排序</th>
            <th>默认</th>
            <th>启用</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in channels" :key="item.id">
            <td>{{ item.channelCode }}</td>
            <td>{{ item.channelName }}</td>
            <td>{{ item.sortOrder }}</td>
            <td>{{ item.isDefault ? '是' : '否' }}</td>
            <td>{{ item.isActive ? '启用' : '停用' }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else class="empty">当前没有渠道数据。</p>
  </section>
</template>

<script setup lang="ts">
import type { ReleaseChannel } from '@/contracts/release-types'

defineProps<{
  channels: ReleaseChannel[]
}>()
</script>
