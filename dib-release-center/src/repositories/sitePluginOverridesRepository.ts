import type { PostgrestError } from '@supabase/supabase-js'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'
import type { SitePluginOverride } from '@/contracts/site-types'
import type { SiteOverrideUpsertPayload } from '@/services/sitePolicyDraftService'

interface SitePluginOverrideRow {
  id: string
  site_id: string
  package_id: string
  action: 'allow' | 'deny'
  reason: string
  is_active: boolean
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

function toSitePluginOverride(row: SitePluginOverrideRow): SitePluginOverride {
  return {
    id: row.id,
    siteRowId: row.site_id,
    pluginPackageId: row.package_id,
    pluginCode: row.plugin_packages?.plugin_code ?? '',
    pluginName: row.plugin_packages?.plugin_name ?? '',
    action: row.action,
    reason: row.reason,
    isActive: row.is_active,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  }
}

export async function listSitePluginOverrides(): Promise<SitePluginOverride[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('site_plugin_overrides')
    .select(`
      id,
      site_id,
      package_id,
      action,
      reason,
      is_active,
      created_at,
      updated_at,
      plugin_packages (
        plugin_code,
        plugin_name
      )
    `)

  throwIfError(error, '查询站点插件覆盖')
  return (data ?? []).map((row: unknown) => toSitePluginOverride(row as SitePluginOverrideRow))
}

export async function upsertSitePluginOverride(payload: SiteOverrideUpsertPayload): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('site_plugin_overrides')
    .upsert(payload, {
      onConflict: 'site_id,package_id',
    })

  throwIfError(error, '保存站点插件覆盖')
}

export async function deleteSitePluginOverride(siteRowId: string, packageId: string): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('site_plugin_overrides')
    .delete()
    .eq('site_id', siteRowId)
    .eq('package_id', packageId)

  throwIfError(error, '删除站点插件覆盖')
}
