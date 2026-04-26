import { beforeEach, describe, expect, it, vi } from 'vitest'

const uploadMock = vi.fn()
const removeMock = vi.fn()
const eqMock = vi.fn()
const deleteMock = vi.fn()
const selectMock = vi.fn()
const updateMock = vi.fn()
const fromMock = vi.fn()
const schemaMock = vi.fn()
const storageFromMock = vi.fn()

vi.mock('@/services/supabase', () => ({
  RELEASE_SCHEMA: 'dib_release',
  getSupabaseClient: () => ({
    schema: schemaMock,
    storage: {
      from: storageFromMock,
    },
  }),
}))

import {
  deleteReleaseAsset,
  deleteReleaseAssetObject,
  findReleaseAssetReferences,
  replaceReleaseAssetFile,
  updateReleaseAssetMetadata,
  uploadManifestAsset,
} from '@/repositories/releaseAssetsRepository'

describe('releaseAssetsRepository', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    uploadMock.mockReset()
    uploadMock.mockResolvedValue({ error: null })
    removeMock.mockReset()
    removeMock.mockResolvedValue({ error: null })
    eqMock.mockReset()
    eqMock.mockResolvedValue({ error: null })
    deleteMock.mockReset()
    deleteMock.mockReturnValue({ eq: eqMock })
    selectMock.mockReset()
    selectMock.mockReturnValue({ eq: eqMock })
    updateMock.mockReset()
    updateMock.mockReturnValue({ eq: eqMock })
    fromMock.mockReset()
    fromMock.mockReturnValue({
      delete: deleteMock,
      select: selectMock,
      update: updateMock,
    })
    schemaMock.mockReset()
    schemaMock.mockReturnValue({ from: fromMock })
    storageFromMock.mockReset()
    storageFromMock.mockReturnValue({
      upload: uploadMock,
      remove: removeMock,
    })
  })

  it('uploadManifestAsset should upload manifest content as raw bytes instead of Blob', async () => {
    await uploadManifestAsset('dib-releases', 'manifests/stable/client-manifest.json', '{"channel":"stable"}')

    expect(uploadMock).toHaveBeenCalledTimes(1)
    expect(uploadMock).toHaveBeenCalledWith(
      'manifests/stable/client-manifest.json',
      expect.any(Uint8Array),
      expect.objectContaining({
        upsert: true,
        contentType: 'application/json',
      }),
    )
  })

  it('deleteReleaseAsset should delete a release asset by id', async () => {
    await deleteReleaseAsset('asset-1')

    expect(schemaMock).toHaveBeenCalledWith('dib_release')
    expect(fromMock).toHaveBeenCalledWith('release_assets')
    expect(deleteMock).toHaveBeenCalledTimes(1)
    expect(eqMock).toHaveBeenCalledWith('id', 'asset-1')
  })

  it('findReleaseAssetReferences should return plugin and client version references', async () => {
    eqMock
      .mockResolvedValueOnce({
        data: [
          {
            id: 'plugin-version-1',
            plugin_code: 'patient-registration',
            version: '1.0.0',
            channel_code: 'stable',
          },
        ],
        error: null,
      })
      .mockResolvedValueOnce({
        data: [
          {
            id: 'client-version-1',
            version: '1.0.0',
            channel_code: 'stable',
          },
        ],
        error: null,
      })

    const references = await findReleaseAssetReferences('asset-1')

    expect(fromMock).toHaveBeenCalledWith('plugin_versions')
    expect(fromMock).toHaveBeenCalledWith('client_versions')
    expect(eqMock).toHaveBeenCalledWith('asset_id', 'asset-1')
    expect(references.pluginVersions).toEqual([
      {
        id: 'plugin-version-1',
        pluginCode: 'patient-registration',
        version: '1.0.0',
        channelCode: 'stable',
      },
    ])
    expect(references.clientVersions).toEqual([
      {
        id: 'client-version-1',
        version: '1.0.0',
        channelCode: 'stable',
      },
    ])
  })

  it('deleteReleaseAssetObject should remove a storage object by bucket and path', async () => {
    await deleteReleaseAssetObject('dib-releases', 'plugins/patient-registration/stable/1.0.0/pkg.zip')

    expect(storageFromMock).toHaveBeenCalledWith('dib-releases')
    expect(removeMock).toHaveBeenCalledWith(['plugins/patient-registration/stable/1.0.0/pkg.zip'])
  })

  it('replaceReleaseAssetFile should upload with upsert enabled', async () => {
    const file = new File(['new package'], 'package.zip', { type: 'application/zip' })

    await replaceReleaseAssetFile('dib-releases', 'plugins/pkg.zip', file)

    expect(storageFromMock).toHaveBeenCalledWith('dib-releases')
    expect(uploadMock).toHaveBeenCalledWith(
      'plugins/pkg.zip',
      file,
      expect.objectContaining({
        upsert: true,
        contentType: 'application/zip',
      }),
    )
  })

  it('updateReleaseAssetMetadata should update metadata by asset id', async () => {
    const payload = {
      file_name: 'package.zip',
      sha256: 'a'.repeat(64),
      size_bytes: 123,
      mime_type: 'application/zip',
    }

    await updateReleaseAssetMetadata('asset-1', payload)

    expect(fromMock).toHaveBeenCalledWith('release_assets')
    expect(updateMock).toHaveBeenCalledWith(payload)
    expect(eqMock).toHaveBeenCalledWith('id', 'asset-1')
  })
})
