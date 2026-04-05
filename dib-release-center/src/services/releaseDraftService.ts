import type { ClientManifest } from '@/contracts/client-manifest'
import type { PluginManifest } from '@/contracts/plugin-manifest'
import type { ClientVersion, PluginVersion } from '@/contracts/release-types'
import { buildClientManifest, buildPluginManifest } from '@/services/manifestService'

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
