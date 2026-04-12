import { beforeEach, describe, expect, it, vi } from 'vitest'

const uploadMock = vi.fn()

vi.mock('@/services/supabase', () => ({
  RELEASE_SCHEMA: 'dib_release',
  getSupabaseClient: () => ({
    storage: {
      from: () => ({
        upload: uploadMock,
      }),
    },
  }),
}))

import { uploadManifestAsset } from '@/repositories/releaseAssetsRepository'

describe('releaseAssetsRepository', () => {
  beforeEach(() => {
    uploadMock.mockReset()
    uploadMock.mockResolvedValue({ error: null })
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
})
