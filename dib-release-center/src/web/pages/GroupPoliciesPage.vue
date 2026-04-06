<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Group Policies</p>
        <h2>分组授权</h2>
      </div>
      <span class="panel-count">{{ filteredPolicies.length }} 条授权</span>
    </header>

    <p class="form-tip">
      先选择站点分组，再配置该分组的默认插件列表。未配置的插件默认不授权，个别例外后续再通过站点覆盖处理。
    </p>

    <div class="field-grid">
      <label>
        <span>目标分组</span>
        <select :value="selectedGroupId" @change="emitSelectGroup(($event.target as HTMLSelectElement).value)">
          <option value="">请选择分组</option>
          <option v-for="group in groups" :key="group.id" :value="group.id">
            {{ group.groupName }} / {{ group.groupCode }}
          </option>
        </select>
      </label>
    </div>

    <form class="release-form compact-form" @submit.prevent="submitDraft">
      <div class="field-grid field-grid-wide">
        <label>
          <span>插件定义</span>
          <select v-model="draft.pluginPackageId" :disabled="!selectedGroupId">
            <option value="">请选择插件</option>
            <option v-for="item in packages" :key="item.id" :value="item.id">
              {{ item.pluginName }} / {{ item.pluginCode }}
            </option>
          </select>
        </label>
        <label>
          <span>最低客户端版本</span>
          <input v-model="draft.minClientVersion" placeholder="1.0.0" :disabled="!selectedGroupId">
        </label>
        <label>
          <span>最高客户端版本</span>
          <input v-model="draft.maxClientVersion" placeholder="9999.9999.9999" :disabled="!selectedGroupId">
        </label>
        <label class="checkbox-field">
          <input v-model="draft.isEnabled" type="checkbox" :disabled="!selectedGroupId">
          <span>授权启用</span>
        </label>
      </div>

      <div class="form-actions">
        <button type="submit" :disabled="!selectedGroupId">保存分组授权</button>
      </div>
    </form>

    <div v-if="selectedGroupId && filteredPolicies.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>插件</th>
            <th>授权状态</th>
            <th>客户端范围</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in filteredPolicies" :key="item.id">
            <td>
              <strong>{{ item.pluginName }}</strong>
              <div class="subline">{{ item.pluginCode }}</div>
            </td>
            <td>{{ item.isEnabled ? '已授权' : '已禁用' }}</td>
            <td>{{ item.minClientVersion }} - {{ item.maxClientVersion }}</td>
            <td class="action-cell">
              <button type="button" class="ghost-button inline-button" @click="fillDraft(item)">编辑</button>
              <button type="button" class="danger-button inline-button" @click="removePolicy(item)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else-if="selectedGroupId" class="empty">当前分组还没有默认插件授权。</p>
    <p v-else class="empty">请选择一个站点分组开始配置默认授权。</p>
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import type { PluginPackage } from '@/contracts/release-types'
import type { SiteGroup, SiteGroupPluginPolicy } from '@/contracts/site-types'
import type { SiteGroupPolicyDraftInput } from '@/services/sitePolicyDraftService'
import { buildGroupPolicyDraftFromRecord } from '@/services/sitePolicyDraftService'

const props = defineProps<{
  groups: SiteGroup[]
  packages: PluginPackage[]
  policies: SiteGroupPluginPolicy[]
  selectedGroupId: string
}>()

const emit = defineEmits<{
  selectGroup: [groupId: string]
  submit: [draft: SiteGroupPolicyDraftInput]
  delete: [payload: { groupId: string; pluginPackageId: string }]
}>()

const draft = reactive<SiteGroupPolicyDraftInput>({
  groupId: '',
  pluginPackageId: '',
  isEnabled: true,
  minClientVersion: '1.0.0',
  maxClientVersion: '9999.9999.9999',
})

const filteredPolicies = computed(() => props.policies.filter((item) => item.groupId === props.selectedGroupId))

watch(
  () => props.selectedGroupId,
  (value) => {
    draft.groupId = value
  },
  { immediate: true },
)

watch(
  () => props.packages,
  (packages) => {
    if (!draft.pluginPackageId && packages.length > 0) {
      draft.pluginPackageId = packages[0]?.id ?? ''
    }
  },
  { immediate: true },
)

function emitSelectGroup(groupId: string): void {
  emit('selectGroup', groupId)
}

function submitDraft(): void {
  emit('submit', { ...draft, groupId: props.selectedGroupId })
}

function fillDraft(policy: SiteGroupPluginPolicy): void {
  Object.assign(draft, buildGroupPolicyDraftFromRecord(policy))
}

function removePolicy(policy: SiteGroupPluginPolicy): void {
  emit('delete', {
    groupId: policy.groupId,
    pluginPackageId: policy.pluginPackageId,
  })
}
</script>
