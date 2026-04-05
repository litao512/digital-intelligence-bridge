import type { ClientManifest } from '@/contracts/client-manifest'
import type { PluginManifest } from '@/contracts/plugin-manifest'
import type {
  ClientVersion,
  PluginVersion,
  ReleaseAssetKind,
} from '@/contracts/release-types'
import { buildClientManifest, buildPluginManifest } from '@/services/manifestService'
import { computeSha256Hex, assertSha256Hex } from '@/utils/hash'

export interface PluginVersionDraftInput {
  packageId: string
  channelId: string
  assetId: string
  version: string
  dibMinVersion: string
  dibMaxVersion: string
  releaseNotes: string
  manifestJsonText: string
  isPublished: boolean
  isMandatory: boolean
}

export interface ClientVersionDraftInput {
  channelId: string
  assetId: string
  version: string
  minUpgradeVersion: string
  releaseNotes: string
  isPublished: boolean
  isMandatory: boolean
}

export interface ReleaseAssetDraftInput {
  bucketName: string
  storagePath: string
  fileName: string
  assetKind: ReleaseAssetKind
  sha256: string
  sizeBytes: string
  mimeType: string
}

export interface ReleaseManifestPreview {
  clientManifest: ClientManifest
  pluginManifest: PluginManifest
}

export interface PluginVersionInsertPayload {
  package_id: string
  channel_id: string
  asset_id: string
  version: string
  dib_min_version: string
  dib_max_version: string
  release_notes: string
  manifest_json: Record<string, unknown>
  is_published: boolean
  is_mandatory: boolean
  published_at: string | null
}

export interface ClientVersionInsertPayload {
  channel_id: string
  asset_id: string
  version: string
  min_upgrade_version: string
  release_notes: string
  is_published: boolean
  is_mandatory: boolean
  published_at: string | null
}

export interface ReleaseAssetInsertPayload {
  bucket_name: string
  storage_path: string
  file_name: string
  asset_kind: ReleaseAssetKind
  sha256: string
  size_bytes: number
  mime_type: string
}

export interface ManifestAssetPublishPlanItem {
  fileName: string
  storagePath: string
  content: string
  payload: ReleaseAssetInsertPayload
}

export interface ManifestPublishPlan {
  channelCode: string
  assets: ManifestAssetPublishPlanItem[]
}

function requireValue(value: string, message: string): string {
  const normalized = value.trim()
  if (!normalized) {
    throw new Error(message)
  }

  return normalized
}

function parseManifestJson(value: string): Record<string, unknown> {
  const normalized = value.trim()
  if (!normalized) {
    return {}
  }

  const parsed = JSON.parse(normalized) as unknown
  if (!parsed || Array.isArray(parsed) || typeof parsed !== 'object') {
    throw new Error('插件 manifest JSON 必须是对象')
  }

  return parsed as Record<string, unknown>
}

function resolvePublishedAt(isPublished: boolean): string | null {
  return isPublished ? new Date().toISOString() : null
}

function parseSizeBytes(value: string): number {
  const normalized = requireValue(value, '资产大小不能为空')
  const parsed = Number(normalized)
  if (!Number.isInteger(parsed) || parsed < 0) {
    throw new Error('资产大小必须是非负整数')
  }

  return parsed
}

function stringifyJson(value: unknown): string {
  return `${JSON.stringify(value, null, 2)}\n`
}

export function buildPluginVersionInsert(input: PluginVersionDraftInput): PluginVersionInsertPayload {
  return {
    package_id: requireValue(input.packageId, '插件定义不能为空'),
    channel_id: requireValue(input.channelId, '发布渠道不能为空'),
    asset_id: requireValue(input.assetId, '插件资产不能为空'),
    version: requireValue(input.version, '插件版本号不能为空'),
    dib_min_version: requireValue(input.dibMinVersion, 'DIB 最低版本不能为空'),
    dib_max_version: requireValue(input.dibMaxVersion, 'DIB 最高版本不能为空'),
    release_notes: input.releaseNotes.trim(),
    manifest_json: parseManifestJson(input.manifestJsonText),
    is_published: input.isPublished,
    is_mandatory: input.isMandatory,
    published_at: resolvePublishedAt(input.isPublished),
  }
}

export function buildClientVersionInsert(input: ClientVersionDraftInput): ClientVersionInsertPayload {
  return {
    channel_id: requireValue(input.channelId, '发布渠道不能为空'),
    asset_id: requireValue(input.assetId, '客户端资产不能为空'),
    version: requireValue(input.version, '客户端版本号不能为空'),
    min_upgrade_version: requireValue(input.minUpgradeVersion, '最低升级版本不能为空'),
    release_notes: input.releaseNotes.trim(),
    is_published: input.isPublished,
    is_mandatory: input.isMandatory,
    published_at: resolvePublishedAt(input.isPublished),
  }
}

export function buildReleaseAssetInsert(input: ReleaseAssetDraftInput): ReleaseAssetInsertPayload {
  return {
    bucket_name: requireValue(input.bucketName, '资产 bucket 不能为空'),
    storage_path: requireValue(input.storagePath, '资产路径不能为空'),
    file_name: requireValue(input.fileName, '资产文件名不能为空'),
    asset_kind: input.assetKind,
    sha256: assertSha256Hex(input.sha256, '资产'),
    size_bytes: parseSizeBytes(input.sizeBytes),
    mime_type: input.mimeType.trim(),
  }
}

export function buildManifestPreview(
  channelCode: string,
  pluginVersions: PluginVersion[],
  clientVersions: ClientVersion[],
): ReleaseManifestPreview {
  const normalizedChannelCode = requireValue(channelCode, '预览渠道不能为空')

  return {
    clientManifest: buildClientManifest(
      normalizedChannelCode,
      clientVersions.filter((item) => item.channelCode === normalizedChannelCode),
    ),
    pluginManifest: buildPluginManifest(
      normalizedChannelCode,
      pluginVersions.filter((item) => item.channelCode === normalizedChannelCode),
    ),
  }
}

export async function buildManifestPublishPlan(
  channelCode: string,
  pluginVersions: PluginVersion[],
  clientVersions: ClientVersion[],
): Promise<ManifestPublishPlan> {
  const normalizedChannelCode = requireValue(channelCode, '发布渠道不能为空')
  const preview = buildManifestPreview(normalizedChannelCode, pluginVersions, clientVersions)

  const clientContent = stringifyJson(preview.clientManifest)
  const pluginContent = stringifyJson(preview.pluginManifest)

  const clientStoragePath = `manifests/${normalizedChannelCode}/client-manifest.json`
  const pluginStoragePath = `manifests/${normalizedChannelCode}/plugin-manifest.json`

  const clientPayload: ReleaseAssetInsertPayload = {
    bucket_name: 'dib-releases',
    storage_path: clientStoragePath,
    file_name: 'client-manifest.json',
    asset_kind: 'manifest',
    sha256: await computeSha256Hex(clientContent),
    size_bytes: new TextEncoder().encode(clientContent).byteLength,
    mime_type: 'application/json',
  }

  const pluginPayload: ReleaseAssetInsertPayload = {
    bucket_name: 'dib-releases',
    storage_path: pluginStoragePath,
    file_name: 'plugin-manifest.json',
    asset_kind: 'manifest',
    sha256: await computeSha256Hex(pluginContent),
    size_bytes: new TextEncoder().encode(pluginContent).byteLength,
    mime_type: 'application/json',
  }

  return {
    channelCode: normalizedChannelCode,
    assets: [
      {
        fileName: clientPayload.file_name,
        storagePath: clientPayload.storage_path,
        content: clientContent,
        payload: clientPayload,
      },
      {
        fileName: pluginPayload.file_name,
        storagePath: pluginPayload.storage_path,
        content: pluginContent,
        payload: pluginPayload,
      },
    ],
  }
}
