import type { PostgrestError } from '@supabase/supabase-js'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'
import type { PluginPackage } from '@/contracts/release-types'
import type { PluginPackageInsertPayload } from '@/services/releaseDraftService'

interface PluginPackageRow {
  id: string
  plugin_code: string
  plugin_name: string
  entry_type: string
  author: string
  description: string
  is_active: boolean
  created_at: string
  updated_at: string
}

function toPluginPackage(row: PluginPackageRow): PluginPackage {
  return {
    id: row.id,
    pluginCode: row.plugin_code,
    pluginName: row.plugin_name,
    entryType: row.entry_type,
    author: row.author,
    description: row.description,
    isActive: row.is_active,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  }
}

function throwIfError(error: PostgrestError | null, context: string): void {
  if (error) {
    throw new Error(`${context}失败：${error.message}`)
  }
}

export async function listPluginPackages(): Promise<PluginPackage[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('plugin_packages')
    .select('id, plugin_code, plugin_name, entry_type, author, description, is_active, created_at, updated_at')
    .order('plugin_code', { ascending: true })

  throwIfError(error, '查询插件定义')
  return (data ?? []).map((row: unknown) => toPluginPackage(row as PluginPackageRow))
}

export async function createPluginPackage(payload: PluginPackageInsertPayload): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('plugin_packages')
    .insert(payload)

  throwIfError(error, '新增插件定义')
}
