<template>
  <section class="panel">
    <header class="panel-header">
      <div>
        <p class="panel-kicker">Organization Permissions</p>
        <h2>单位授权</h2>
      </div>
      <span class="panel-count">{{ selectedOrganization?.name ?? '未选择单位' }}</span>
    </header>

    <div class="field-grid field-grid-wide">
      <label>
        <span>单位</span>
        <select v-model="selectedOrganizationId" @change="emitSelect">
          <option value="">请选择单位</option>
          <option v-for="org in organizations" :key="org.id" :value="org.id">{{ org.name }} / {{ org.code }}</option>
        </select>
      </label>
      <label>
        <span>授权插件</span>
        <select v-model="pluginCode">
          <option value="">请选择插件</option>
          <option v-for="pkg in packages" :key="pkg.id" :value="pkg.pluginCode">{{ pkg.pluginName }} / {{ pkg.pluginCode }}</option>
        </select>
      </label>
      <label>
        <span>授权资源</span>
        <select v-model="resourceId">
          <option value="">请选择资源</option>
          <option v-for="resource in resources" :key="resource.id" :value="resource.id">{{ resource.resourceName }} / {{ resource.resourceCode }}</option>
        </select>
      </label>
    </div>

    <div class="form-actions">
      <button type="button" :disabled="!selectedOrganizationId || !pluginCode" @click="grantPlugin">授权插件</button>
      <button type="button" :disabled="!selectedOrganizationId || !resourceId" @click="grantResource">授权资源</button>
    </div>

    <section class="split-grid">
      <article>
        <h3>插件授权</h3>
        <div class="table-wrap">
          <table>
            <thead><tr><th>插件</th><th>状态</th><th>到期</th><th>操作</th></tr></thead>
            <tbody>
              <tr v-for="item in pluginPermissions" :key="item.id">
                <td>{{ item.pluginCode }}</td>
                <td>{{ item.status }}</td>
                <td>{{ item.expiresAt ?? '长期' }}</td>
                <td><button type="button" class="ghost-button inline-button" @click="$emit('deactivatePlugin', item)">停用</button></td>
              </tr>
            </tbody>
          </table>
        </div>
      </article>
      <article>
        <h3>资源授权</h3>
        <div class="table-wrap">
          <table>
            <thead><tr><th>资源</th><th>状态</th><th>到期</th><th>操作</th></tr></thead>
            <tbody>
              <tr v-for="item in resourcePermissions" :key="item.id">
                <td>{{ resourceName(item.resourceId) }}</td>
                <td>{{ item.status }}</td>
                <td>{{ item.expiresAt ?? '长期' }}</td>
                <td><button type="button" class="ghost-button inline-button" @click="$emit('deactivateResource', item)">停用</button></td>
              </tr>
            </tbody>
          </table>
        </div>
      </article>
    </section>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { OrganizationPluginPermission, OrganizationResourcePermission, OrganizationSummary } from '@/contracts/organization-types'
import type { ResourceSummary } from '@/contracts/resource-types'
import type { PluginPackage } from '@/contracts/release-types'

const props = defineProps<{
  organizations: OrganizationSummary[]
  packages: PluginPackage[]
  resources: ResourceSummary[]
  selectedOrganizationId: string
  pluginPermissions: OrganizationPluginPermission[]
  resourcePermissions: OrganizationResourcePermission[]
}>()

const emit = defineEmits<{
  select: [organizationId: string]
  grantPlugin: [payload: { organizationId: string; pluginCode: string }]
  grantResource: [payload: { organizationId: string; resourceId: string }]
  deactivatePlugin: [permission: OrganizationPluginPermission]
  deactivateResource: [permission: OrganizationResourcePermission]
}>()

const selectedOrganizationId = ref(props.selectedOrganizationId)
const pluginCode = ref('')
const resourceId = ref('')

watch(() => props.selectedOrganizationId, (value) => {
  selectedOrganizationId.value = value
})

const selectedOrganization = computed(() => props.organizations.find((item) => item.id === selectedOrganizationId.value))

function emitSelect(): void {
  emit('select', selectedOrganizationId.value)
}

function grantPlugin(): void {
  emit('grantPlugin', { organizationId: selectedOrganizationId.value, pluginCode: pluginCode.value })
}

function grantResource(): void {
  emit('grantResource', { organizationId: selectedOrganizationId.value, resourceId: resourceId.value })
}

function resourceName(id: string): string {
  const resource = props.resources.find((item) => item.id === id)
  return resource ? `${resource.resourceName} / ${resource.resourceCode}` : id
}
</script>

<style scoped>
.split-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
  margin-top: 20px;
}
</style>
