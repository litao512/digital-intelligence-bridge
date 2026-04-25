import type { PostgrestError } from '@supabase/supabase-js'
import type { ClientVersionInsertPayload } from '@/services/releaseDraftService'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'
import type { ClientVersion } from '@/contracts/release-types'

interface ClientVersionRow {
  id: string
  channel_id: string
  channel_code: string
  channel_name: string
  version: string
  min_upgrade_version: string
  is_published: boolean
  is_mandatory: boolean
  release_notes: string
  published_at: string | null
  created_at: string
  updated_at: string
  asset_id: string
  bucket_name: string
  storage_path: string
  file_name: string
  asset_kind: 'plugin_package' | 'client_package' | 'manifest'
  sha256: string
  size_bytes: number
  mime_type: string
  package_url: string
}

function toClientVersion(row: ClientVersionRow): ClientVersion {
  return {
    id: row.id,
    channelId: row.channel_id,
    channelCode: row.channel_code,
    channelName: row.channel_name,
    version: row.version,
    minUpgradeVersion: row.min_upgrade_version,
    isPublished: row.is_published,
    isMandatory: row.is_mandatory,
    releaseNotes: row.release_notes,
    publishedAt: row.published_at,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
    assetId: row.asset_id,
    bucketName: row.bucket_name,
    storagePath: row.storage_path,
    fileName: row.file_name,
    assetKind: row.asset_kind,
    sha256: row.sha256,
    sizeBytes: row.size_bytes,
    mimeType: row.mime_type,
    packageUrl: row.package_url,
  }
}

function throwIfError(error: PostgrestError | null, context: string): void {
  if (error) {
    throw new Error(`${context}失败：${error.message}`)
  }
}

export async function listClientVersions(): Promise<ClientVersion[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('client_versions_view')
    .select([
      'id',
      'channel_id',
      'channel_code',
      'channel_name',
      'version',
      'min_upgrade_version',
      'is_published',
      'is_mandatory',
      'release_notes',
      'published_at',
      'created_at',
      'updated_at',
      'asset_id',
      'bucket_name',
      'storage_path',
      'file_name',
      'asset_kind',
      'sha256',
      'size_bytes',
      'mime_type',
      'package_url',
    ].join(', '))
    .order('channel_code', { ascending: true })
    .order('published_at', { ascending: false, nullsFirst: false })

  throwIfError(error, '查询客户端版本')
  return (data ?? []).map((row: unknown) => toClientVersion(row as ClientVersionRow))
}

export async function createClientVersion(payload: ClientVersionInsertPayload): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('client_versions')
    .insert(payload)

  throwIfError(error, '新增客户端版本')
}

export async function deleteClientVersion(id: string): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('client_versions')
    .delete()
    .eq('id', id)

  throwIfError(error, '删除客户端版本')
}
