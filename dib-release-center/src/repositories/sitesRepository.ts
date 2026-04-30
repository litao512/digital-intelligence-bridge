import type { PostgrestError } from '@supabase/supabase-js'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'
import type { SiteSummary } from '@/contracts/site-types'

interface SiteOverviewRow {
  id: string
  site_id: string
  site_name: string
  group_id: string | null
  group_code: string | null
  group_name: string | null
  organization_id: string | null
  organization_code: string | null
  organization_name: string | null
  channel_id: string | null
  channel_code: string | null
  channel_name: string | null
  client_version: string
  machine_name: string
  last_seen_at: string | null
  last_update_check_at: string | null
  last_plugin_download_at: string | null
  last_client_download_at: string | null
  installed_plugins_json: string[] | null
  business_tags: string[] | null
  is_active: boolean
  created_at: string
  updated_at: string
}

interface SiteGroupAssignmentPayload {
  group_id: string | null
}

function throwIfError(error: PostgrestError | null, context: string): void {
  if (error) {
    throw new Error(`${context}失败：${error.message}`)
  }
}

export function toSiteSummary(row: SiteOverviewRow): SiteSummary {
  return {
    id: row.id,
    siteId: row.site_id,
    siteName: row.site_name,
    groupId: row.group_id,
    groupCode: row.group_code,
    groupName: row.group_name,
    organizationId: row.organization_id,
    organizationCode: row.organization_code,
    organizationName: row.organization_name,
    channelId: row.channel_id,
    channelCode: row.channel_code,
    channelName: row.channel_name,
    clientVersion: row.client_version,
    machineName: row.machine_name,
    lastSeenAt: row.last_seen_at,
    lastUpdateCheckAt: row.last_update_check_at,
    lastPluginDownloadAt: row.last_plugin_download_at,
    lastClientDownloadAt: row.last_client_download_at,
    installedPlugins: row.installed_plugins_json ?? [],
    businessTags: row.business_tags ?? [],
    isActive: row.is_active,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  }
}

export async function listSites(): Promise<SiteSummary[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('site_overview')
    .select(`
      id,
      site_id,
      site_name,
      group_id,
      group_code,
      group_name,
      organization_id,
      organization_code,
      organization_name,
      channel_id,
      channel_code,
      channel_name,
      client_version,
      machine_name,
      last_seen_at,
      last_update_check_at,
      last_plugin_download_at,
      last_client_download_at,
      installed_plugins_json,
      business_tags,
      is_active,
      created_at,
      updated_at
    `)
    .order('updated_at', { ascending: false })

  throwIfError(error, '查询站点列表')
  return (data ?? []).map((row: unknown) => toSiteSummary(row as SiteOverviewRow))
}

export function buildSiteGroupAssignmentUpdate(groupId: string | null): SiteGroupAssignmentPayload {
  return {
    group_id: groupId,
  }
}

export async function updateSiteGroup(siteRowId: string, groupId: string | null): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('sites')
    .update(buildSiteGroupAssignmentUpdate(groupId))
    .eq('id', siteRowId)

  throwIfError(error, '更新站点分组')
}
