import type { ClientManifest } from '@/contracts/client-manifest'
import type { PluginManifest, PluginManifestItem } from '@/contracts/plugin-manifest'
import type { ClientVersion, PluginVersion } from '@/contracts/release-types'
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

function toPluginManifestItem(version: PluginVersion): PluginManifestItem {
  return {
    pluginId: version.pluginCode,
    name: version.pluginName,
    version: version.version,
    mandatory: version.isMandatory,
    dibMinVersion: version.dibMinVersion,
    dibMaxVersion: version.dibMaxVersion,
    packageUrl: version.packageUrl,
    sha256: assertSha256Hex(version.sha256, `插件 ${version.pluginCode}`),
  }
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
    packageUrl: latestVersion.packageUrl,
    sha256: assertSha256Hex(latestVersion.sha256, `客户端 ${latestVersion.version}`),
    fileName: latestVersion.fileName,
    releaseNotes: latestVersion.releaseNotes,
    publishedAt: latestVersion.publishedAt,
  }
}

export function buildPluginManifest(channel: string, versions: PluginVersion[]): PluginManifest {
  const latestByPluginId = new Map<string, PluginVersion>()

  versions
    .filter((item) => item.isPublished)
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
    publishedAt,
    plugins,
  }
}
