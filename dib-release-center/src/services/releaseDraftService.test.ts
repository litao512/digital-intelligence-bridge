import { describe, expect, it } from 'vitest'
import type { ClientVersion, PluginVersion } from '@/contracts/release-types'
import {
  buildClientVersionInsert,
  buildManifestPreview,
  buildPluginVersionInsert,
} from '@/services/releaseDraftService'

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
    dibMaxVersion: '1.9.99',
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

describe('releaseDraftService', () => {
  it('buildPluginVersionInsert should map draft into database payload', () => {
    const payload = buildPluginVersionInsert({
      packageId: 'package-1',
      channelId: 'channel-stable',
      assetId: 'asset-plugin-1',
      version: '1.0.1',
      dibMinVersion: '1.0.0',
      dibMaxVersion: '1.9.99',
      releaseNotes: 'plugin release',
      manifestJsonText: '{"entry":"PatientRegistration.Plugin"}',
      isPublished: true,
      isMandatory: true,
    })

    expect(payload).toMatchObject({
      package_id: 'package-1',
      channel_id: 'channel-stable',
      asset_id: 'asset-plugin-1',
      version: '1.0.1',
      dib_min_version: '1.0.0',
      dib_max_version: '1.9.99',
      release_notes: 'plugin release',
      is_published: true,
      is_mandatory: true,
      manifest_json: { entry: 'PatientRegistration.Plugin' },
    })
    expect(payload.published_at).toBeTruthy()
  })

  it('buildClientVersionInsert should reject empty version', () => {
    expect(() => buildClientVersionInsert({
      channelId: 'channel-stable',
      assetId: 'asset-client-1',
      version: '   ',
      minUpgradeVersion: '0.9.0',
      releaseNotes: '',
      isPublished: false,
      isMandatory: false,
    })).toThrow('客户端版本号不能为空')
  })

  it('buildManifestPreview should only include the requested channel and latest published versions', () => {
    const preview = buildManifestPreview('stable', [
      createPluginVersion(),
      createPluginVersion({ id: 'plugin-version-2', version: '1.0.1', publishedAt: '2026-04-05T13:00:00Z' }),
      createPluginVersion({ id: 'plugin-version-beta', channelCode: 'beta', channelId: 'channel-beta', version: '2.0.0' }),
    ], [
      createClientVersion(),
      createClientVersion({ id: 'client-version-2', version: '1.1.0', publishedAt: '2026-04-05T14:00:00Z' }),
      createClientVersion({ id: 'client-version-beta', channelCode: 'beta', channelId: 'channel-beta', version: '2.0.0' }),
    ])

    expect(preview.clientManifest.channel).toBe('stable')
    expect(preview.clientManifest.latestVersion).toBe('1.1.0')
    expect(preview.pluginManifest.channel).toBe('stable')
    expect(preview.pluginManifest.plugins).toHaveLength(1)
    expect(preview.pluginManifest.plugins[0]?.version).toBe('1.0.1')
  })
})
