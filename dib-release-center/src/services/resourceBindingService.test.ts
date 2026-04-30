import { describe, expect, it } from 'vitest'
import type { OrganizationPluginPermission, OrganizationResourcePermission } from '@/contracts/organization-types'
import type { ResourceSummary } from '@/contracts/resource-types'
import type { SiteSummary } from '@/contracts/site-types'
import {
  buildResourceBindingCandidateList,
  validateResourceBindingRequest,
} from '@/services/resourceBindingService'

function createSite(overrides: Partial<SiteSummary> = {}): SiteSummary {
  return {
    id: 'site-1',
    siteId: '11111111-1111-1111-1111-111111111111',
    siteName: '门诊站点',
    groupId: null,
    groupCode: null,
    groupName: null,
    organizationId: 'org-1',
    organizationCode: 'hospital-a',
    organizationName: 'A 医院',
    channelId: null,
    channelCode: null,
    channelName: null,
    clientVersion: '1.0.0',
    machineName: 'WIN-01',
    lastSeenAt: null,
    lastUpdateCheckAt: null,
    lastPluginDownloadAt: null,
    lastClientDownloadAt: null,
    installedPlugins: [],
    businessTags: [],
    isActive: true,
    createdAt: '2026-04-30T01:00:00Z',
    updatedAt: '2026-04-30T01:00:00Z',
    ...overrides,
  }
}

function createPluginPermission(overrides: Partial<OrganizationPluginPermission> = {}): OrganizationPluginPermission {
  return {
    id: 'permission-1',
    organizationId: 'org-1',
    pluginCode: 'patient-registration',
    status: 'Active',
    grantedBy: '',
    grantedAt: '2026-04-30T01:00:00Z',
    expiresAt: null,
    ...overrides,
  }
}

function createResourcePermission(overrides: Partial<OrganizationResourcePermission> = {}): OrganizationResourcePermission {
  return {
    id: 'permission-2',
    organizationId: 'org-1',
    resourceId: 'resource-1',
    status: 'Active',
    grantedBy: '',
    grantedAt: '2026-04-30T01:00:00Z',
    expiresAt: null,
    ...overrides,
  }
}

function createResource(overrides: Partial<ResourceSummary> = {}): ResourceSummary {
  return {
    id: 'resource-1',
    resourceCode: 'his-db',
    resourceName: 'HIS 数据库',
    resourceType: 'PostgreSQL',
    ownerOrganizationId: 'org-1',
    ownerOrganizationName: 'A 医院',
    visibilityScope: 'Private',
    capabilities: [],
    businessTags: [],
    status: 'Active',
    description: '',
    createdAt: '2026-04-30T01:00:00Z',
    updatedAt: '2026-04-30T01:00:00Z',
    ...overrides,
  }
}

describe('resourceBindingService', () => {
  it('should validate organization, plugin permission and resource permission', () => {
    const base = {
      site: createSite(),
      pluginCode: 'patient-registration',
      resourceId: 'resource-1',
      usageKey: 'registration-db',
      pluginPermissions: [createPluginPermission()],
      resourcePermissions: [createResourcePermission()],
      now: '2026-04-30T02:00:00Z',
    }

    expect(validateResourceBindingRequest(base)).toBeNull()
    expect(validateResourceBindingRequest({ ...base, site: createSite({ organizationId: null }) })).toBe('站点未绑定单位')
    expect(validateResourceBindingRequest({ ...base, pluginPermissions: [] })).toBe('站点所属单位未获得插件授权')
    expect(validateResourceBindingRequest({ ...base, resourcePermissions: [] })).toBe('站点所属单位未获得资源授权')
  })

  it('should build candidates from active resource permissions', () => {
    const result = buildResourceBindingCandidateList(
      [
        createResource({ id: 'resource-1' }),
        createResource({ id: 'resource-2' }),
        createResource({ id: 'resource-3', status: 'Disabled' }),
      ],
      [
        createResourcePermission({ resourceId: 'resource-1' }),
        createResourcePermission({ resourceId: 'resource-3' }),
      ],
    )

    expect(result.map((item) => item.id)).toEqual(['resource-1'])
  })
})
