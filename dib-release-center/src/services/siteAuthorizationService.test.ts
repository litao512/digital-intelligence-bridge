import { describe, expect, it } from 'vitest'
import type {
  SiteGroupPluginPolicy,
  SitePluginOverride,
  SiteGroup,
  SiteSummary,
} from '@/contracts/site-types'
import { aggregateSiteAnalytics, resolveSiteAuthorizedPlugins } from '@/services/siteAuthorizationService'

function createGroup(overrides: Partial<SiteGroup> = {}): SiteGroup {
  return {
    id: 'group-outpatient',
    groupCode: 'outpatient-basic',
    groupName: '门诊基础版',
    description: '',
    isActive: true,
    createdAt: '2026-04-06T12:00:00Z',
    updatedAt: '2026-04-06T12:00:00Z',
    ...overrides,
  }
}

function createSite(overrides: Partial<SiteSummary> = {}): SiteSummary {
  return {
    id: 'site-row-1',
    siteId: '11111111-1111-1111-1111-111111111111',
    siteName: '门诊登记台 1',
    groupId: 'group-outpatient',
    groupCode: 'outpatient-basic',
    groupName: '门诊基础版',
    channelId: 'channel-stable',
    channelCode: 'stable',
    channelName: '稳定版',
    clientVersion: '1.0.0',
    machineName: 'WIN-CLIENT-01',
    lastSeenAt: '2026-04-06T12:00:00Z',
    lastUpdateCheckAt: null,
    lastPluginDownloadAt: null,
    lastClientDownloadAt: null,
    installedPlugins: [],
    isActive: true,
    createdAt: '2026-04-06T12:00:00Z',
    updatedAt: '2026-04-06T12:00:00Z',
    ...overrides,
  }
}

function createGroupPolicy(overrides: Partial<SiteGroupPluginPolicy> = {}): SiteGroupPluginPolicy {
  return {
    id: 'group-policy-1',
    groupId: 'group-outpatient',
    pluginPackageId: 'package-patient-registration',
    pluginCode: 'patient-registration',
    pluginName: '就诊登记',
    isEnabled: true,
    minClientVersion: '1.0.0',
    maxClientVersion: '1.9.99',
    createdAt: '2026-04-06T12:00:00Z',
    updatedAt: '2026-04-06T12:00:00Z',
    ...overrides,
  }
}

function createOverride(overrides: Partial<SitePluginOverride> = {}): SitePluginOverride {
  return {
    id: 'override-1',
    siteRowId: 'site-row-1',
    pluginPackageId: 'package-patient-registration',
    pluginCode: 'patient-registration',
    pluginName: '就诊登记',
    action: 'deny',
    reason: '站点临时禁用',
    isActive: true,
    createdAt: '2026-04-06T12:00:00Z',
    updatedAt: '2026-04-06T12:00:00Z',
    ...overrides,
  }
}

describe('siteAuthorizationService', () => {
  it('should keep group-enabled plugin when no override exists', () => {
    const result = resolveSiteAuthorizedPlugins({
      site: createSite(),
      groupPolicies: [createGroupPolicy()],
      siteOverrides: [],
    })

    expect(result).toHaveLength(1)
    expect(result[0]).toMatchObject({
      pluginCode: 'patient-registration',
      effectiveIsEnabled: true,
      source: 'group',
    })
  })

  it('should remove plugin when site override denies it', () => {
    const result = resolveSiteAuthorizedPlugins({
      site: createSite(),
      groupPolicies: [createGroupPolicy()],
      siteOverrides: [createOverride({ action: 'deny' })],
    })

    expect(result).toHaveLength(0)
  })

  it('should exclude plugin when client version is out of allowed range', () => {
    const result = resolveSiteAuthorizedPlugins({
      site: createSite({ clientVersion: '1.0.0' }),
      groupPolicies: [
        createGroupPolicy({
          minClientVersion: '1.1.0',
          maxClientVersion: '1.9.99',
        }),
      ],
      siteOverrides: [],
    })

    expect(result).toHaveLength(0)
  })

  it('should aggregate site analytics for overview, version distribution and authorization drift', () => {
    const analytics = aggregateSiteAnalytics({
      sites: [
        createSite({
          id: 'site-row-1',
          siteName: '门诊登记台 1',
          groupId: 'group-outpatient',
          groupCode: 'outpatient-basic',
          groupName: '门诊基础版',
          clientVersion: '1.0.0',
          installedPlugins: [],
          lastSeenAt: '2026-04-06T12:00:00Z',
        }),
        createSite({
          id: 'site-row-2',
          siteId: '22222222-2222-2222-2222-222222222222',
          siteName: '门诊登记台 2',
          groupId: null,
          groupCode: null,
          groupName: null,
          clientVersion: '1.0.1',
          installedPlugins: ['bedside-rounding'],
          lastSeenAt: null,
        }),
      ],
      groups: [createGroup()],
      groupPolicies: [createGroupPolicy()],
      siteOverrides: [],
      now: '2026-04-06T12:10:00Z',
    })

    expect(analytics.totalSiteCount).toBe(2)
    expect(analytics.activeSiteCount24h).toBe(1)
    expect(analytics.unassignedSiteCount).toBe(1)
    expect(analytics.versionBreakdown).toEqual([
      { version: '1.0.0', count: 1 },
      { version: '1.0.1', count: 1 },
    ])
    expect(analytics.groupBreakdown).toEqual([
      { groupCode: 'outpatient-basic', groupName: '门诊基础版', count: 1 },
      { groupCode: 'unassigned', groupName: '未分组', count: 1 },
    ])
    expect(analytics.authorizationDrift[0]).toMatchObject({
      siteName: '门诊登记台 1',
      authorizedNotInstalled: ['patient-registration'],
    })
    expect(analytics.authorizationDrift[1]).toMatchObject({
      siteName: '门诊登记台 2',
      installedNotAuthorized: ['bedside-rounding'],
    })
  })
})
