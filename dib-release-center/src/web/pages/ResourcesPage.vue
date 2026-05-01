<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Resources</p>
        <h2>资源管理</h2>
      </div>
      <span class="panel-count">{{ resources.length }} 个资源</span>
    </header>

    <form class="release-form" @submit.prevent="submitDraft">
      <div class="field-grid field-grid-wide">
        <label><span>资源编码</span><input v-model="draft.resourceCode" placeholder="his-db"></label>
        <label><span>资源名称</span><input v-model="draft.resourceName" placeholder="HIS 数据库"></label>
        <label>
          <span>资源类型</span>
          <select v-model="draft.resourceType">
            <option value="PostgreSQL">PostgreSQL</option>
            <option value="SqlServer">SqlServer</option>
            <option value="Supabase">Supabase</option>
            <option value="HttpService">HttpService</option>
          </select>
        </label>
        <label>
          <span>所属单位</span>
          <select v-model="draft.ownerOrganizationId">
            <option value="">未指定</option>
            <option v-for="org in organizations" :key="org.id" :value="org.id">{{ org.name }} / {{ org.code }}</option>
          </select>
        </label>
        <label>
          <span>可见范围</span>
          <select v-model="draft.visibilityScope">
            <option value="Private">Private</option>
            <option value="Shared">Shared</option>
            <option value="Platform">Platform</option>
          </select>
        </label>
        <label><span>能力</span><input v-model="capabilityText" placeholder="registration, query"></label>
        <label><span>业务标签</span><input v-model="tagText" placeholder="门诊, 医保"></label>
        <label>
          <span>状态</span>
          <select v-model="draft.status">
            <option value="Draft">Draft</option>
            <option value="PendingApproval">PendingApproval</option>
            <option value="Active">Active</option>
            <option value="Disabled">Disabled</option>
            <option value="Archived">Archived</option>
          </select>
        </label>
      </div>
      <label class="full-field"><span>描述</span><input v-model="draft.description" placeholder="资源用途说明"></label>
      <div class="form-actions">
        <button type="submit">{{ editingId ? '保存资源' : '新增资源' }}</button>
        <button type="button" class="ghost-button" @click="resetDraft">清空</button>
      </div>
      <p class="form-tip">连接密钥仍由资源密钥表维护，本页只管理资源基础信息和归属。</p>
    </form>

    <div v-if="resources.length" class="table-wrap">
      <table>
        <thead><tr><th>资源</th><th>类型</th><th>所属单位</th><th>标签</th><th>状态</th><th>操作</th></tr></thead>
        <tbody>
          <tr v-for="item in resources" :key="item.id">
            <td><strong>{{ item.resourceName }}</strong><div class="subline">{{ item.resourceCode }}</div></td>
            <td>{{ item.resourceType }}</td>
            <td>{{ organizationName(item.ownerOrganizationId) || '未指定' }}</td>
            <td>{{ item.businessTags.join('、') || '未标注' }}</td>
            <td>{{ item.status }}</td>
            <td><button type="button" class="ghost-button inline-button" @click="editResource(item)">编辑</button></td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else class="empty page-empty">当前没有资源记录。</p>
  </section>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import type { OrganizationSummary } from '@/contracts/organization-types'
import type { ResourceStatus, ResourceSummary } from '@/contracts/resource-types'
import { normalizeBusinessTags } from '@/services/organizationManagementService'

const props = defineProps<{
  resources: ResourceSummary[]
  organizations: OrganizationSummary[]
}>()

const emit = defineEmits<{
  submit: [payload: ResourceSummaryInput]
}>()

interface ResourceSummaryInput {
  id: string | null
  resourceCode: string
  resourceName: string
  resourceType: string
  ownerOrganizationId: string | null
  visibilityScope: string
  capabilities: string[]
  businessTags: string[]
  status: ResourceStatus
  description: string
}

const editingId = ref<string | null>(null)
const capabilityText = ref('')
const tagText = ref('')
const draft = reactive({
  resourceCode: '',
  resourceName: '',
  resourceType: 'PostgreSQL',
  ownerOrganizationId: '',
  visibilityScope: 'Private',
  status: 'Active' as ResourceStatus,
  description: '',
})

function submitDraft(): void {
  emit('submit', {
    id: editingId.value,
    ...draft,
    ownerOrganizationId: draft.ownerOrganizationId || null,
    capabilities: normalizeBusinessTags(capabilityText.value),
    businessTags: normalizeBusinessTags(tagText.value),
  })
  resetDraft()
}

function editResource(item: ResourceSummary): void {
  editingId.value = item.id
  draft.resourceCode = item.resourceCode
  draft.resourceName = item.resourceName
  draft.resourceType = item.resourceType
  draft.ownerOrganizationId = item.ownerOrganizationId ?? ''
  draft.visibilityScope = item.visibilityScope
  draft.status = item.status
  draft.description = item.description
  capabilityText.value = item.capabilities.join(', ')
  tagText.value = item.businessTags.join(', ')
}

function resetDraft(): void {
  editingId.value = null
  draft.resourceCode = ''
  draft.resourceName = ''
  draft.resourceType = 'PostgreSQL'
  draft.ownerOrganizationId = ''
  draft.visibilityScope = 'Private'
  draft.status = 'Active'
  draft.description = ''
  capabilityText.value = ''
  tagText.value = ''
}

function organizationName(id: string | null): string {
  return props.organizations.find((item) => item.id === id)?.name ?? ''
}
</script>
