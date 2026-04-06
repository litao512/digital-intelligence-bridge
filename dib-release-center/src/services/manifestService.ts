import type { ClientManifest } from '@/contracts/client-manifest'
import type { PluginManifest, PluginManifestItem } from '@/contracts/plugin-manifest'
import type { ClientVersion, PluginVersion } from '@/contracts/release-types'
import { getSupabasePublicBaseUrl } from '@/services/supabase'
import { assertSha256Hex } from '@/utils/hash'
import { compareVersions } from '@/utils/version'

function comparePublishedAt(left: string | null, right: string | null): number {
  if (left === right) {
    return 0
  }

  if (left === null) {
    return -1
  }

  if (right === null) {
    return 1
  }

  return Date.parse(left) - Date.parse(right)
}

function comparePluginPriority(left: PluginVersion, right: PluginVersion): number {
  const versionResult = compareVersions(left.version, right.version)
  if (versionResult !== 0) {
    return versionResult
  }

  return comparePublishedAt(left.publishedAt, right.publishedAt)
}

function compareClientPriority(left: ClientVersion, right: ClientVersion): number {
  const versionResult = compareVersions(left.version, right.version)
  if (versionResult !== 0) {
    return versionResult
  }

  return comparePublishedAt(left.publishedAt, right.publishedAt)
}

function normalizePackageUrl(packageUrl: string | null): string | null {
  if (!packageUrl) {
    return null
  }

  if (/^https?:\/\//i.test(packageUrl)) {
    return packageUrl
  }

  if (!packageUrl.startsWith('/')) {
    return packageUrl
  }

  const publicBaseUrl = getSupabasePublicBaseUrl()
  if (!publicBaseUrl) {
    return packageUrl
  }

  return `${publicBaseUrl}${packageUrl}`
}

function toPluginManifestItem(version: PluginVersion): PluginManifestItem {
  return {
    pluginId: version.pluginCode,
    name: version.pluginName,
    version: version.version,
    mandatory: version.isMandatory,
    dibMinVersion: version.dibMinVersion,
    dibMaxVersion: version.dibMaxVersion,
    packageUrl: normalizePackageUrl(version.packageUrl),
    sha256: assertSha256Hex(version.sha256, `插件 ${version.pluginCode}`),
  }
}

interface BuildPluginManifestOptions {
  siteId?: string | null
  allowedPluginIds?: string[] | null
}

export function buildClientManifest(channel: string, versions: ClientVersion[]): ClientManifest {
  const publishedVersions = versions.filter((item) => item.isPublished)
  const latestVersion = [...publishedVersions].sort((left, right) => compareClientPriority(right, left))[0] ?? null

  if (!latestVersion) {
    return {
      channel,
      latestVersion: null,
      mandatory: false,
      minUpgradeVersion: null,
      packageUrl: null,
      sha256: null,
      fileName: null,
      releaseNotes: '',
      publishedAt: null,
    }
  }

  return {
    channel,
    latestVersion: latestVersion.version,
    mandatory: latestVersion.isMandatory,
    minUpgradeVersion: latestVersion.minUpgradeVersion,
    packageUrl: normalizePackageUrl(latestVersion.packageUrl),
    sha256: assertSha256Hex(latestVersion.sha256, `客户端 ${latestVersion.version}`),
    fileName: latestVersion.fileName,
    releaseNotes: latestVersion.releaseNotes,
    publishedAt: latestVersion.publishedAt,
  }
}

export function buildPluginManifest(
  channel: string,
  versions: PluginVersion[],
  options: BuildPluginManifestOptions = {},
): PluginManifest {
  const allowedPluginIds = options.allowedPluginIds?.length
    ? new Set(options.allowedPluginIds)
    : null
  const latestByPluginId = new Map<string, PluginVersion>()

  versions
    .filter((item) => item.isPublished)
    .filter((item) => !allowedPluginIds || allowedPluginIds.has(item.pluginCode))
    .forEach((item) => {
      const current = latestByPluginId.get(item.pluginCode)
      if (!current || comparePluginPriority(item, current) > 0) {
        latestByPluginId.set(item.pluginCode, item)
      }
    })

  const plugins = [...latestByPluginId.values()]
    .sort((left, right) => left.pluginCode.localeCompare(right.pluginCode))
    .map((item) => toPluginManifestItem(item))

  const publishedAt = [...latestByPluginId.values()]
    .map((item) => item.publishedAt)
    .filter((value): value is string => Boolean(value))
    .sort((left, right) => comparePublishedAt(right, left))[0] ?? null

  return {
    channel,
    siteId: options.siteId ?? null,
    publishedAt,
    plugins,
  }
}
