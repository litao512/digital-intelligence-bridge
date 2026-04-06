import type { SiteGroupPluginPolicy } from '@/contracts/site-types'

export interface SiteGroupPolicyDraftInput {
  groupId: string
  pluginPackageId: string
  isEnabled: boolean
  minClientVersion: string
  maxClientVersion: string
}

export interface SiteGroupPolicyUpsertPayload {
  group_id: string
  package_id: string
  is_enabled: boolean
  min_client_version: string
  max_client_version: string
}

export interface SiteOverrideDraftInput {
  siteRowId: string
  pluginPackageId: string
  action: 'allow' | 'deny'
  reason: string
  isActive: boolean
}

export interface SiteOverrideUpsertPayload {
  site_id: string
  package_id: string
  action: 'allow' | 'deny'
  reason: string
  is_active: boolean
}

function requireValue(value: string, message: string): string {
  const normalized = value.trim()
  if (!normalized) {
    throw new Error(message)
  }

  return normalized
}

export function buildGroupPolicyUpsert(input: SiteGroupPolicyDraftInput): SiteGroupPolicyUpsertPayload {
  return {
    group_id: requireValue(input.groupId, '站点分组不能为空'),
    package_id: requireValue(input.pluginPackageId, '插件定义不能为空'),
    is_enabled: input.isEnabled,
    min_client_version: requireValue(input.minClientVersion, '最低客户端版本不能为空'),
    max_client_version: requireValue(input.maxClientVersion, '最高客户端版本不能为空'),
  }
}

export function buildGroupPolicyDraftFromRecord(policy: SiteGroupPluginPolicy): SiteGroupPolicyDraftInput {
  return {
    groupId: policy.groupId,
    pluginPackageId: policy.pluginPackageId,
    isEnabled: policy.isEnabled,
    minClientVersion: policy.minClientVersion,
    maxClientVersion: policy.maxClientVersion,
  }
}

export function buildSiteOverrideUpsert(input: SiteOverrideDraftInput): SiteOverrideUpsertPayload {
  return {
    site_id: requireValue(input.siteRowId, '站点不能为空'),
    package_id: requireValue(input.pluginPackageId, '插件定义不能为空'),
    action: input.action,
    reason: input.reason.trim(),
    is_active: input.isActive,
  }
}
