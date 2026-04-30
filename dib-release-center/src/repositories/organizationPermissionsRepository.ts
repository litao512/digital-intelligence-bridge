import type { PostgrestError } from '@supabase/supabase-js'
import type {
  OrganizationPluginPermission,
  OrganizationResourcePermission,
  OrganizationStatus,
} from '@/contracts/organization-types'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'

interface OrganizationPluginPermissionRow {
  id: string
  organization_id: string
  plugin_code: string
  status: OrganizationStatus
  granted_by: string
  granted_at: string
  expires_at: string | null
}

interface OrganizationResourcePermissionRow {
  id: string
  organization_id: string
  resource_id: string
  status: OrganizationStatus
  granted_by: string
  granted_at: string
  expires_at: string | null
}

export interface OrganizationPluginPermissionInput {
  organizationId: string
  pluginCode: string
  status?: OrganizationStatus
  grantedBy?: string
  expiresAt?: string | null
}

export interface OrganizationResourcePermissionInput {
  organizationId: string
  resourceId: string
  status?: OrganizationStatus
  grantedBy?: string
  expiresAt?: string | null
}

function throwIfError(error: PostgrestError | null, context: string): void {
  if (error) {
    throw new Error(`${context}失败：${error.message}`)
  }
}

export function toOrganizationPluginPermission(row: OrganizationPluginPermissionRow): OrganizationPluginPermission {
  return {
    id: row.id,
    organizationId: row.organization_id,
    pluginCode: row.plugin_code,
    status: row.status,
    grantedBy: row.granted_by,
    grantedAt: row.granted_at,
    expiresAt: row.expires_at,
  }
}

export function toOrganizationResourcePermission(row: OrganizationResourcePermissionRow): OrganizationResourcePermission {
  return {
    id: row.id,
    organizationId: row.organization_id,
    resourceId: row.resource_id,
    status: row.status,
    grantedBy: row.granted_by,
    grantedAt: row.granted_at,
    expiresAt: row.expires_at,
  }
}

export function buildOrganizationPluginPermissionPayload(input: OrganizationPluginPermissionInput) {
  return {
    organization_id: input.organizationId,
    plugin_code: input.pluginCode.trim(),
    status: input.status ?? 'Active',
    granted_by: input.grantedBy?.trim() ?? '',
    expires_at: input.expiresAt ?? null,
  }
}

export function buildOrganizationResourcePermissionPayload(input: OrganizationResourcePermissionInput) {
  return {
    organization_id: input.organizationId,
    resource_id: input.resourceId,
    status: input.status ?? 'Active',
    granted_by: input.grantedBy?.trim() ?? '',
    expires_at: input.expiresAt ?? null,
  }
}

export async function listOrganizationPluginPermissions(organizationId: string): Promise<OrganizationPluginPermission[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('organization_plugin_permissions')
    .select('id, organization_id, plugin_code, status, granted_by, granted_at, expires_at')
    .eq('organization_id', organizationId)
    .order('plugin_code', { ascending: true })

  throwIfError(error, '查询单位插件授权')
  return (data ?? []).map((row: unknown) => toOrganizationPluginPermission(row as OrganizationPluginPermissionRow))
}

export async function upsertOrganizationPluginPermission(input: OrganizationPluginPermissionInput): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('organization_plugin_permissions')
    .upsert(buildOrganizationPluginPermissionPayload(input), { onConflict: 'organization_id,plugin_code' })

  throwIfError(error, '保存单位插件授权')
}

export async function deactivateOrganizationPluginPermission(organizationId: string, pluginCode: string): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('organization_plugin_permissions')
    .update({ status: 'Inactive' })
    .eq('organization_id', organizationId)
    .eq('plugin_code', pluginCode)

  throwIfError(error, '停用单位插件授权')
}

export async function listOrganizationResourcePermissions(organizationId: string): Promise<OrganizationResourcePermission[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('organization_resource_permissions')
    .select('id, organization_id, resource_id, status, granted_by, granted_at, expires_at')
    .eq('organization_id', organizationId)
    .order('resource_id', { ascending: true })

  throwIfError(error, '查询单位资源授权')
  return (data ?? []).map((row: unknown) => toOrganizationResourcePermission(row as OrganizationResourcePermissionRow))
}

export async function upsertOrganizationResourcePermission(input: OrganizationResourcePermissionInput): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('organization_resource_permissions')
    .upsert(buildOrganizationResourcePermissionPayload(input), { onConflict: 'organization_id,resource_id' })

  throwIfError(error, '保存单位资源授权')
}

export async function deactivateOrganizationResourcePermission(organizationId: string, resourceId: string): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('organization_resource_permissions')
    .update({ status: 'Inactive' })
    .eq('organization_id', organizationId)
    .eq('resource_id', resourceId)

  throwIfError(error, '停用单位资源授权')
}
