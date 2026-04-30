import { describe, expect, it } from 'vitest'
import type { OrganizationSummary } from '@/contracts/organization-types'
import {
  createDefaultOrganizationFilterInput,
  filterOrganizations,
  normalizeBusinessTags,
} from '@/services/organizationManagementService'

function createOrganization(overrides: Partial<OrganizationSummary> = {}): OrganizationSummary {
  return {
    id: 'org-1',
    code: 'hospital-a',
    name: 'A 医院',
    organizationType: 'Hospital',
    businessTags: ['门诊', '随访'],
    status: 'Active',
    createdAt: '2026-04-30T01:00:00Z',
    updatedAt: '2026-04-30T01:00:00Z',
    ...overrides,
  }
}

describe('organizationManagementService', () => {
  it('should normalize business tags from text and array', () => {
    expect(normalizeBusinessTags(' 门诊,随访，数据上报、门诊\n医保 ')).toEqual(['门诊', '随访', '数据上报', '医保'])
    expect(normalizeBusinessTags([' 门诊 ', '', '随访'])).toEqual(['门诊', '随访'])
  })

  it('should filter organizations by keyword across code name type and tags', () => {
    const organizations = [
      createOrganization(),
      createOrganization({
        id: 'org-2',
        code: 'cdc-b',
        name: 'B 疾控中心',
        organizationType: 'CDC',
        businessTags: ['数据上报'],
      }),
    ]

    expect(filterOrganizations(organizations, { keyword: '疾控', organizationType: '' }).map((item) => item.id)).toEqual(['org-2'])
    expect(filterOrganizations(organizations, { keyword: 'hospital-a', organizationType: '' }).map((item) => item.id)).toEqual(['org-1'])
    expect(filterOrganizations(organizations, { keyword: '数据上报', organizationType: '' }).map((item) => item.id)).toEqual(['org-2'])
  })

  it('should filter by organization type', () => {
    const organizations = [
      createOrganization({ id: 'org-1', organizationType: 'Hospital' }),
      createOrganization({ id: 'org-2', organizationType: 'CDC', code: 'cdc-b' }),
    ]

    expect(filterOrganizations(organizations, { keyword: '', organizationType: 'CDC' }).map((item) => item.id)).toEqual(['org-2'])
  })

  it('should exclude inactive organizations by default', () => {
    const organizations = [
      createOrganization({ id: 'org-1', status: 'Active' }),
      createOrganization({ id: 'org-2', code: 'inactive', status: 'Inactive' }),
    ]

    expect(filterOrganizations(organizations, { keyword: '', organizationType: '' }).map((item) => item.id)).toEqual(['org-1'])
    expect(filterOrganizations(organizations, { keyword: '', organizationType: '', includeInactive: true }).map((item) => item.id)).toEqual(['org-1', 'org-2'])
  })

  it('should provide default organization filters', () => {
    expect(createDefaultOrganizationFilterInput()).toEqual({
      keyword: '',
      organizationType: '',
      includeInactive: false,
    })
  })
})
