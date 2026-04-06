import type { PostgrestError } from '@supabase/supabase-js'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'
import type { SiteGroupPluginPolicy } from '@/contracts/site-types'

interface GroupPluginPolicyRow {
  id: string
  group_id: string
  package_id: string
  is_enabled: boolean
  min_client_version: string
  max_client_version: string
  created_at: string
  updated_at: string
  plugin_packages: {
    plugin_code: string
    plugin_name: string
  } | null
}

function throwIfError(error: PostgrestError | null, context: string): void {
  if (error) {
    throw new Error(`${context}失败：${error.message}`)
  }
}

function toGroupPluginPolicy(row: GroupPluginPolicyRow): SiteGroupPluginPolicy {
  return {
    id: row.id,
    groupId: row.group_id,
    pluginPackageId: row.package_id,
    pluginCode: row.plugin_packages?.plugin_code ?? '',
    pluginName: row.plugin_packages?.plugin_name ?? '',
    isEnabled: row.is_enabled,
    minClientVersion: row.min_client_version,
    maxClientVersion: row.max_client_version,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  }
}

export async function listGroupPluginPolicies(): Promise<SiteGroupPluginPolicy[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('group_plugin_policies')
    .select(`
      id,
      group_id,
      package_id,
      is_enabled,
      min_client_version,
      max_client_version,
      created_at,
      updated_at,
      plugin_packages (
        plugin_code,
        plugin_name
      )
    `)

  throwIfError(error, '查询分组插件授权')
  return (data ?? []).map((row: unknown) => toGroupPluginPolicy(row as GroupPluginPolicyRow))
}
