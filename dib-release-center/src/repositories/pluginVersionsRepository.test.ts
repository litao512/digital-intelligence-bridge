import { beforeEach, describe, expect, it, vi } from 'vitest'

const eqMock = vi.fn()
const deleteMock = vi.fn()
const fromMock = vi.fn()
const schemaMock = vi.fn()

vi.mock('@/services/supabase', () => ({
  RELEASE_SCHEMA: 'dib_release',
  getSupabaseClient: () => ({
    schema: schemaMock,
  }),
}))

import { deletePluginVersion } from '@/repositories/pluginVersionsRepository'

describe('pluginVersionsRepository', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    eqMock.mockResolvedValue({ error: null })
    deleteMock.mockReturnValue({ eq: eqMock })
    fromMock.mockReturnValue({ delete: deleteMock })
    schemaMock.mockReturnValue({ from: fromMock })
  })

  it('deletePluginVersion should delete a plugin version by id', async () => {
    await deletePluginVersion('plugin-version-1')

    expect(schemaMock).toHaveBeenCalledWith('dib_release')
    expect(fromMock).toHaveBeenCalledWith('plugin_versions')
    expect(deleteMock).toHaveBeenCalledTimes(1)
    expect(eqMock).toHaveBeenCalledWith('id', 'plugin-version-1')
  })
})
