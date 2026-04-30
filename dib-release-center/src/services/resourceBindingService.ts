import type { OrganizationPluginPermission, OrganizationResourcePermission } from '@/contracts/organization-types'
import type { ResourceSummary } from '@/contracts/resource-types'
import type { PluginPackage } from '@/contracts/release-types'
import type { SiteSummary } from '@/contracts/site-types'
import { getActivePluginCodes, getActiveResourceIds } from '@/services/organizationPermissionService'

export interface ResourceBindingValidationInput {
  site: SiteSummary | null
  pluginCode: string
  resourceId: string
  usageKey: string
  pluginPermissions: OrganizationPluginPermission[]
  resourcePermissions: OrganizationResourcePermission[]
  now?: string
}

export function validateResourceBindingRequest(input: ResourceBindingValidationInput): string | null {
  if (!input.site) {
    return '请选择站点'
  }

  if (!input.site.organizationId) {
    return '站点未绑定单位'
  }

  const pluginCode = input.pluginCode.trim()
  if (!pluginCode) {
    return '请选择插件'
  }

  if (!input.usageKey.trim()) {
    return '请填写 usage_key'
  }

  if (!input.resourceId) {
    return '请选择资源'
  }

  const activePluginCodes = getActivePluginCodes(input.pluginPermissions, input.now)
  if (!activePluginCodes.includes(pluginCode)) {
    return '站点所属单位未获得插件授权'
  }

  const activeResourceIds = getActiveResourceIds(input.resourcePermissions, input.now)
  if (!activeResourceIds.includes(input.resourceId)) {
    return '站点所属单位未获得资源授权'
  }

  return null
}

export function buildResourceBindingCandidateList(
  resources: ResourceSummary[],
  resourcePermissions: OrganizationResourcePermission[],
  now?: string,
): ResourceSummary[] {
  const activeResourceIds = new Set(getActiveResourceIds(resourcePermissions, now))
  return resources.filter((resource) => resource.status === 'Active' && activeResourceIds.has(resource.id))
}

export function buildPluginCandidateList(
  packages: PluginPackage[],
  pluginPermissions: OrganizationPluginPermission[],
  now?: string,
): PluginPackage[] {
  const activePluginCodes = new Set(getActivePluginCodes(pluginPermissions, now))
  return packages.filter((item) => activePluginCodes.has(item.pluginCode))
}
