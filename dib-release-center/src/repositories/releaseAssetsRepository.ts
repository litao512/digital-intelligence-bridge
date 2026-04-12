import type { PostgrestError } from '@supabase/supabase-js'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'
import type { ReleaseAsset } from '@/contracts/release-types'
import type { ReleaseAssetInsertPayload } from '@/services/releaseDraftService'

interface ReleaseAssetRow {
  id: string
  bucket_name: string
  storage_path: string
  file_name: string
  asset_kind: 'plugin_package' | 'client_package' | 'manifest'
  sha256: string
  size_bytes: number
  mime_type: string
  created_at: string
  updated_at: string
}

function toReleaseAsset(row: ReleaseAssetRow): ReleaseAsset {
  return {
    id: row.id,
    bucketName: row.bucket_name,
    storagePath: row.storage_path,
    fileName: row.file_name,
    assetKind: row.asset_kind,
    sha256: row.sha256,
    sizeBytes: row.size_bytes,
    mimeType: row.mime_type,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  }
}

function throwIfError(error: PostgrestError | null, context: string): void {
  if (error) {
    throw new Error(`${context}失败：${error.message}`)
  }
}

export async function listReleaseAssets(): Promise<ReleaseAsset[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('release_assets')
    .select('id, bucket_name, storage_path, file_name, asset_kind, sha256, size_bytes, mime_type, created_at, updated_at')
    .order('created_at', { ascending: false })

  throwIfError(error, '查询发布资产')
  return (data ?? []).map((row: unknown) => toReleaseAsset(row as ReleaseAssetRow))
}

export async function createReleaseAsset(payload: ReleaseAssetInsertPayload): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('release_assets')
    .insert(payload)

  throwIfError(error, '新增发布资产')
}

export async function upsertReleaseAsset(payload: ReleaseAssetInsertPayload): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('release_assets')
    .upsert(payload, { onConflict: 'bucket_name,storage_path' })

  throwIfError(error, '写入 manifest 资产')
}

export async function uploadManifestAsset(bucketName: string, storagePath: string, content: string): Promise<void> {
  const contentBytes = new TextEncoder().encode(content)
  const { error } = await getSupabaseClient()
    .storage
    .from(bucketName)
    .upload(storagePath, contentBytes, {
      upsert: true,
      contentType: 'application/json',
    })

  if (error) {
    throw new Error(`上传 manifest 文件失败：${error.message}`)
  }
}

export async function uploadReleaseAssetFile(bucketName: string, storagePath: string, file: File): Promise<void> {
  const contentType = file.type.trim() || 'application/octet-stream'
  const { error } = await getSupabaseClient()
    .storage
    .from(bucketName)
    .upload(storagePath, file, {
      upsert: false,
      contentType,
    })

  if (error) {
    throw new Error(`上传发布资产文件失败：${error.message}`)
  }
}
