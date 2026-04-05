import type { PostgrestError } from '@supabase/supabase-js'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'
import type { PluginVersion } from '@/contracts/release-types'

interface PluginVersionRow {
  id: string
  package_id: string
  plugin_code: string
  plugin_name: string
  channel_id: string
  channel_code: string
  channel_name: string
  version: string
  dib_min_version: string
  dib_max_version: string
  release_notes: string
  manifest_json: Record<string, unknown>
  is_published: boolean
  is_mandatory: boolean
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

function toPluginVersion(row: PluginVersionRow): PluginVersion {
  return {
    id: row.id,
    packageId: row.package_id,
    pluginCode: row.plugin_code,
    pluginName: row.plugin_name,
    channelId: row.channel_id,
    channelCode: row.channel_code,
    channelName: row.channel_name,
    version: row.version,
    dibMinVersion: row.dib_min_version,
    dibMaxVersion: row.dib_max_version,
    releaseNotes: row.release_notes,
    manifestJson: row.manifest_json,
    isPublished: row.is_published,
    isMandatory: row.is_mandatory,
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

export async function listPluginVersions(): Promise<PluginVersion[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('plugin_versions_view')
    .select([
      'id',
      'package_id',
      'plugin_code',
      'plugin_name',
      'channel_id',
      'channel_code',
      'channel_name',
      'version',
      'dib_min_version',
      'dib_max_version',
      'release_notes',
      'manifest_json',
      'is_published',
      'is_mandatory',
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
    .order('plugin_code', { ascending: true })
    .order('published_at', { ascending: false, nullsFirst: false })

  throwIfError(error, '查询插件版本')
  return (data ?? []).map((row: unknown) => toPluginVersion(row as PluginVersionRow))
}
