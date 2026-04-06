import { describe, expect, it } from 'vitest'
import type { SiteSummary } from '@/contracts/site-types'
import { filterSites } from '@/services/siteManagementService'

function createSite(overrides: Partial<SiteSummary> = {}): SiteSummary {
  return {
    id: 'site-row-1',
    siteId: '11111111-1111-1111-1111-111111111111',
    siteName: '门诊登记台 1',
    groupId: 'group-a',
    groupCode: 'outpatient-basic',
    groupName: '门诊基础版',
    channelId: 'channel-stable',
    channelCode: 'stable',
    channelName: '稳定版',
    clientVersion: '1.0.0',
    machineName: 'WIN-CLIENT-01',
    lastSeenAt: null,
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

describe('siteManagementService', () => {
  it('should filter by keyword across name, site id and machine name', () => {
    const sites = [
      createSite(),
      createSite({
        id: 'site-row-2',
        siteId: '22222222-2222-2222-2222-222222222222',
        siteName: '病区治疗台',
        machineName: 'WARD-CLIENT-02',
      }),
    ]

    expect(filterSites(sites, { keyword: 'ward-client', groupId: '' })).toHaveLength(1)
    expect(filterSites(sites, { keyword: '登记台 1', groupId: '' })).toHaveLength(1)
    expect(filterSites(sites, { keyword: '22222222', groupId: '' })).toHaveLength(1)
  })

  it('should filter by group id when provided', () => {
    const sites = [
      createSite({ id: 'site-row-1', groupId: 'group-a' }),
      createSite({ id: 'site-row-2', groupId: 'group-b', siteId: '22222222-2222-2222-2222-222222222222' }),
    ]

    const result = filterSites(sites, { keyword: '', groupId: 'group-b' })

    expect(result).toHaveLength(1)
    expect(result[0].groupId).toBe('group-b')
  })
})
