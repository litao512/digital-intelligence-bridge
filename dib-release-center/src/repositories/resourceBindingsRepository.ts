import type { PostgrestError } from '@supabase/supabase-js'
import type { ResourceBindingSummary } from '@/contracts/resource-types'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'

interface ResourceBindingRow {
  id: string
  site_row_id: string
  plugin_code: string
  resource_id: string
  status: ResourceBindingSummary['status']
  usage_key: string
  config_version: number
  created_at: string
  updated_at: string
}

export interface ResourceBindingInput {
  siteRowId: string
  pluginCode: string
  resourceId: string
  usageKey: string
  status?: ResourceBindingSummary['status']
}

function throwIfError(error: PostgrestError | null, context: string): void {
  if (error) {
    throw new Error(`${context}失败：${error.message}`)
  }
}

export function toResourceBindingSummary(row: ResourceBindingRow): ResourceBindingSummary {
  return {
    id: row.id,
    siteRowId: row.site_row_id,
    pluginCode: row.plugin_code,
    resourceId: row.resource_id,
    status: row.status,
    usageKey: row.usage_key,
    configVersion: row.config_version,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  }
}

export function buildResourceBindingPayload(input: ResourceBindingInput) {
  return {
    site_row_id: input.siteRowId,
    plugin_code: input.pluginCode.trim(),
    resource_id: input.resourceId,
    usage_key: input.usageKey.trim(),
    binding_scope: 'PluginAtSite',
    status: input.status ?? 'Active',
  }
}

export async function listResourceBindingsBySite(siteRowId: string): Promise<ResourceBindingSummary[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('resource_bindings')
    .select('id, site_row_id, plugin_code, resource_id, status, usage_key, config_version, created_at, updated_at')
    .eq('site_row_id', siteRowId)
    .order('plugin_code', { ascending: true })

  throwIfError(error, '查询站点资源绑定')
  return (data ?? []).map((row: unknown) => toResourceBindingSummary(row as ResourceBindingRow))
}

export async function upsertResourceBinding(input: ResourceBindingInput): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('resource_bindings')
    .upsert(buildResourceBindingPayload(input), { onConflict: 'site_row_id,plugin_code,usage_key' })

  throwIfError(error, '保存站点资源绑定')
}

export async function deactivateResourceBinding(bindingId: string): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('resource_bindings')
    .update({ status: 'Revoked' })
    .eq('id', bindingId)

  throwIfError(error, '停用站点资源绑定')
}
