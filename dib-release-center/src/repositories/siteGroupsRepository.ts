import type { PostgrestError } from '@supabase/supabase-js'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'
import type { SiteGroup } from '@/contracts/site-types'

interface SiteGroupRow {
  id: string
  group_code: string
  group_name: string
  description: string
  is_active: boolean
  created_at: string
  updated_at: string
}

function toSiteGroup(row: SiteGroupRow): SiteGroup {
  return {
    id: row.id,
    groupCode: row.group_code,
    groupName: row.group_name,
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

export async function listSiteGroups(): Promise<SiteGroup[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('site_groups')
    .select('id, group_code, group_name, description, is_active, created_at, updated_at')
    .order('group_code', { ascending: true })

  throwIfError(error, '查询站点分组')
  return (data ?? []).map((row: unknown) => toSiteGroup(row as SiteGroupRow))
}
