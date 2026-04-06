import { describe, expect, it, vi } from 'vitest'

vi.mock('@/services/supabase', () => ({
  getSupabasePublicBaseUrl: () => 'http://101.42.19.26:8000',
}))

import type { ClientVersion, PluginVersion } from '@/contracts/release-types'
import { buildClientManifest, buildPluginManifest } from '@/services/manifestService'

function createPluginVersion(overrides: Partial<PluginVersion> = {}): PluginVersion {
  return {
    id: 'plugin-version-1',
    packageId: 'package-1',
    pluginCode: 'patient-registration',
    pluginName: '就诊登记',
    channelId: 'channel-stable',
    channelCode: 'stable',
    channelName: '稳定版',
    version: '1.0.0',
    dibMinVersion: '1.0.0',
    dibMaxVersion: '9.9.9',
    releaseNotes: 'init',
    manifestJson: {},
    isPublished: true,
    isMandatory: false,
    publishedAt: '2026-04-05T10:00:00Z',
    createdAt: '2026-04-05T10:00:00Z',
    updatedAt: '2026-04-05T10:00:00Z',
    assetId: 'asset-plugin-1',
    bucketName: 'dib-releases',
    storagePath: 'plugins/patient-registration/stable/1.0.0/package.zip',
    fileName: 'package.zip',
    assetKind: 'plugin_package',
    sha256: 'a'.repeat(64),
    sizeBytes: 1024,
    mimeType: 'application/zip',
    packageUrl: '/storage/v1/object/public/dib-releases/plugins/patient-registration/stable/1.0.0/package.zip',
    ...overrides,
  }
}

function createClientVersion(overrides: Partial<ClientVersion> = {}): ClientVersion {
  return {
    id: 'client-version-1',
    channelId: 'channel-stable',
    channelCode: 'stable',
    channelName: '稳定版',
    version: '1.0.0',
    minUpgradeVersion: '0.9.0',
    isPublished: true,
    isMandatory: false,
    releaseNotes: 'init',
    publishedAt: '2026-04-05T12:00:00Z',
    createdAt: '2026-04-05T12:00:00Z',
    updatedAt: '2026-04-05T12:00:00Z',
    assetId: 'asset-client-1',
    bucketName: 'dib-releases',
    storagePath: 'clients/stable/1.0.0/package.zip',
    fileName: 'package.zip',
    assetKind: 'client_package',
    sha256: 'b'.repeat(64),
    sizeBytes: 2048,
    mimeType: 'application/zip',
    packageUrl: '/storage/v1/object/public/dib-releases/clients/stable/1.0.0/package.zip',
    ...overrides,
  }
}

describe('manifestService', () => {
  it('buildPluginManifest should normalize relative packageUrl into absolute url', () => {
    const manifest = buildPluginManifest('stable', [createPluginVersion()])

    expect(manifest.plugins[0]?.packageUrl).toBe('http://101.42.19.26:8000/storage/v1/object/public/dib-releases/plugins/patient-registration/stable/1.0.0/package.zip')
  })

  it('buildClientManifest should normalize relative packageUrl into absolute url', () => {
    const manifest = buildClientManifest('stable', [createClientVersion()])

    expect(manifest.packageUrl).toBe('http://101.42.19.26:8000/storage/v1/object/public/dib-releases/clients/stable/1.0.0/package.zip')
  })
})

