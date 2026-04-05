import type { PostgrestError } from '@supabase/supabase-js'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'
import type { ReleaseChannel } from '@/contracts/release-types'

interface ReleaseChannelRow {
  id: string
  channel_code: string
  channel_name: string
  description: string
  sort_order: number
  is_default: boolean
  is_active: boolean
  created_at: string
  updated_at: string
}

function toReleaseChannel(row: ReleaseChannelRow): ReleaseChannel {
  return {
    id: row.id,
    channelCode: row.channel_code,
    channelName: row.channel_name,
    description: row.description,
    sortOrder: row.sort_order,
    isDefault: row.is_default,
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

export async function listReleaseChannels(): Promise<ReleaseChannel[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('release_channels')
    .select('id, channel_code, channel_name, description, sort_order, is_default, is_active, created_at, updated_at')
    .order('sort_order', { ascending: true })

  throwIfError(error, '查询发布渠道')
  return (data ?? []).map((row: unknown) => toReleaseChannel(row as ReleaseChannelRow))
}
