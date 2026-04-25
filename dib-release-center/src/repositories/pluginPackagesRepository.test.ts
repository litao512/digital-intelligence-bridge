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

import { deletePluginPackage } from '@/repositories/pluginPackagesRepository'

describe('pluginPackagesRepository', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    eqMock.mockResolvedValue({ error: null })
    deleteMock.mockReturnValue({ eq: eqMock })
    fromMock.mockReturnValue({ delete: deleteMock })
    schemaMock.mockReturnValue({ from: fromMock })
  })

  it('deletePluginPackage should delete a plugin package by id', async () => {
    await deletePluginPackage('plugin-package-1')

    expect(schemaMock).toHaveBeenCalledWith('dib_release')
    expect(fromMock).toHaveBeenCalledWith('plugin_packages')
    expect(deleteMock).toHaveBeenCalledTimes(1)
    expect(eqMock).toHaveBeenCalledWith('id', 'plugin-package-1')
  })
})
