<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Organizations</p>
        <h2>单位管理</h2>
      </div>
      <span class="panel-count">{{ filteredOrganizations.length }} / {{ organizations.length }} 个单位</span>
    </header>

    <form class="release-form" @submit.prevent="submitDraft">
      <div class="field-grid field-grid-wide">
        <label>
          <span>单位编码</span>
          <input v-model="draft.code" placeholder="hospital-a">
        </label>
        <label>
          <span>单位名称</span>
          <input v-model="draft.name" placeholder="A 医院">
        </label>
        <label>
          <span>单位类型</span>
          <input v-model="draft.organizationType" placeholder="Hospital / CDC / Insurance">
        </label>
        <label>
          <span>业务标签</span>
          <input v-model="tagText" placeholder="门诊, 随访, 数据上报">
        </label>
        <label>
          <span>状态</span>
          <select v-model="draft.status">
            <option value="Active">Active</option>
            <option value="Inactive">Inactive</option>
          </select>
        </label>
      </div>
      <div class="form-actions">
        <button type="submit">{{ editingId ? '保存单位' : '新增单位' }}</button>
        <button type="button" class="ghost-button" @click="resetDraft">清空</button>
      </div>
    </form>

    <div class="field-grid field-grid-wide page-filter-grid">
      <label>
        <span>关键字搜索</span>
        <input v-model="keyword" placeholder="编码 / 名称 / 类型 / 标签">
      </label>
      <label>
        <span>单位类型</span>
        <input v-model="organizationType" placeholder="留空显示全部">
      </label>
      <label class="checkbox-field compact-checkbox">
        <input v-model="includeInactive" type="checkbox">
        <span>显示停用单位</span>
      </label>
    </div>

    <div v-if="filteredOrganizations.length" class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>单位</th>
            <th>类型</th>
            <th>标签</th>
            <th>状态</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in filteredOrganizations" :key="item.id">
            <td>
              <strong>{{ item.name }}</strong>
              <div class="subline">{{ item.code }}</div>
            </td>
            <td>{{ item.organizationType }}</td>
            <td>{{ item.businessTags.join('、') || '未标注' }}</td>
            <td>{{ item.status }}</td>
            <td>
              <button type="button" class="ghost-button inline-button" @click="editOrganization(item)">编辑</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else class="empty page-empty">当前没有单位记录。</p>
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import type { OrganizationStatus, OrganizationSummary } from '@/contracts/organization-types'
import { filterOrganizations, normalizeBusinessTags } from '@/services/organizationManagementService'

const props = defineProps<{
  organizations: OrganizationSummary[]
}>()

const emit = defineEmits<{
  submit: [payload: {
    id: string | null
    code: string
    name: string
    organizationType: string
    businessTags: string[]
    status: OrganizationStatus
  }]
}>()

const editingId = ref<string | null>(null)
const keyword = ref('')
const organizationType = ref('')
const includeInactive = ref(false)
const tagText = ref('')
const draft = reactive({
  code: '',
  name: '',
  organizationType: 'Hospital',
  status: 'Active' as OrganizationStatus,
})

const filteredOrganizations = computed(() => filterOrganizations(props.organizations, {
  keyword: keyword.value,
  organizationType: organizationType.value,
  includeInactive: includeInactive.value,
}))

function submitDraft(): void {
  emit('submit', {
    id: editingId.value,
    code: draft.code,
    name: draft.name,
    organizationType: draft.organizationType,
    businessTags: normalizeBusinessTags(tagText.value),
    status: draft.status,
  })
  resetDraft()
}

function editOrganization(item: OrganizationSummary): void {
  editingId.value = item.id
  draft.code = item.code
  draft.name = item.name
  draft.organizationType = item.organizationType
  draft.status = item.status
  tagText.value = item.businessTags.join(', ')
}

function resetDraft(): void {
  editingId.value = null
  draft.code = ''
  draft.name = ''
  draft.organizationType = 'Hospital'
  draft.status = 'Active'
  tagText.value = ''
}
</script>
