import { beforeEach, describe, expect, it, vi } from 'vitest'

const orderMock = vi.fn()
const selectMock = vi.fn()
const insertMock = vi.fn()
const updateEqMock = vi.fn()
const updateMock = vi.fn()
const fromMock = vi.fn()
const schemaMock = vi.fn()

vi.mock('@/services/supabase', () => ({
  RELEASE_SCHEMA: 'dib_release',
  getSupabaseClient: () => ({
    schema: schemaMock,
  }),
}))

import {
  buildOrganizationPayload,
  createOrganization,
  listOrganizations,
  toOrganizationSummary,
  updateOrganization,
} from '@/repositories/organizationsRepository'

describe('organizationsRepository', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    orderMock.mockResolvedValue({ data: [], error: null })
    selectMock.mockReturnValue({ order: orderMock })
    insertMock.mockResolvedValue({ error: null })
    updateEqMock.mockResolvedValue({ error: null })
    updateMock.mockReturnValue({ eq: updateEqMock })
    fromMock.mockReturnValue({ select: selectMock, insert: insertMock, update: updateMock })
    schemaMock.mockReturnValue({ from: fromMock })
  })

  it('toOrganizationSummary should map database row', () => {
    expect(toOrganizationSummary({
      id: 'org-1',
      code: 'hospital-a',
      name: 'A 医院',
      organization_type: 'Hospital',
      business_tags: ['门诊'],
      status: 'Active',
      created_at: '2026-04-30T01:00:00Z',
      updated_at: '2026-04-30T02:00:00Z',
    })).toEqual({
      id: 'org-1',
      code: 'hospital-a',
      name: 'A 医院',
      organizationType: 'Hospital',
      businessTags: ['门诊'],
      status: 'Active',
      createdAt: '2026-04-30T01:00:00Z',
      updatedAt: '2026-04-30T02:00:00Z',
    })
  })

  it('buildOrganizationPayload should trim fields and tags', () => {
    expect(buildOrganizationPayload({
      code: ' hospital-a ',
      name: ' A 医院 ',
      organizationType: ' ',
      businessTags: [' 门诊 ', '', ' 随访 '],
      status: 'Active',
    })).toEqual({
      code: 'hospital-a',
      name: 'A 医院',
      organization_type: 'Unknown',
      business_tags: ['门诊', '随访'],
      status: 'Active',
    })
  })

  it('listOrganizations should query organizations ordered by code', async () => {
    await listOrganizations()

    expect(schemaMock).toHaveBeenCalledWith('dib_release')
    expect(fromMock).toHaveBeenCalledWith('organizations')
    expect(selectMock).toHaveBeenCalledWith('id, code, name, organization_type, business_tags, status, created_at, updated_at')
    expect(orderMock).toHaveBeenCalledWith('code', { ascending: true })
  })

  it('createOrganization should insert normalized payload', async () => {
    await createOrganization({
      code: ' hospital-a ',
      name: ' A 医院 ',
      organizationType: 'Hospital',
      businessTags: ['门诊'],
      status: 'Active',
    })

    expect(insertMock).toHaveBeenCalledWith({
      code: 'hospital-a',
      name: 'A 医院',
      organization_type: 'Hospital',
      business_tags: ['门诊'],
      status: 'Active',
    })
  })

  it('updateOrganization should update by id', async () => {
    await updateOrganization('org-1', {
      code: 'hospital-a',
      name: 'A 医院',
      organizationType: 'Hospital',
      businessTags: [],
      status: 'Inactive',
    })

    expect(updateMock).toHaveBeenCalledWith({
      code: 'hospital-a',
      name: 'A 医院',
      organization_type: 'Hospital',
      business_tags: [],
      status: 'Inactive',
    })
    expect(updateEqMock).toHaveBeenCalledWith('id', 'org-1')
  })

  it('createOrganization should throw Chinese context when insert fails', async () => {
    insertMock.mockResolvedValue({ error: { message: 'duplicate code' } })

    await expect(createOrganization({
      code: 'hospital-a',
      name: 'A 医院',
      organizationType: 'Hospital',
      businessTags: [],
      status: 'Active',
    })).rejects.toThrow('新增单位失败：duplicate code')
  })
})
