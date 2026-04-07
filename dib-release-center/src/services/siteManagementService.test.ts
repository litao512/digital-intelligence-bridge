import { describe, expect, it } from 'vitest'
import type { SiteSummary } from '@/contracts/site-types'
import {
  buildQuickAssignGroupPayload,
  filterSites,
  findSiteRowBySiteId,
  getSiteSearchSeed,
  filterSitesForSelection,
} from '@/services/siteManagementService'

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

  it('should filter only unassigned sites when requested', () => {
    const sites = [
      createSite({ id: 'site-row-1', groupId: 'group-a' }),
      createSite({ id: 'site-row-2', groupId: null, groupCode: null, groupName: null, siteId: '22222222-2222-2222-2222-222222222222' }),
    ]

    const result = filterSites(sites, {
      keyword: '',
      groupId: '',
      onlyUnassigned: true,
      onlyRecentlyActive: false,
    })

    expect(result.map((item) => item.id)).toEqual(['site-row-2'])
  })

  it('should filter only recently active sites within 24 hours', () => {
    const sites = [
      createSite({ id: 'site-row-1', lastSeenAt: '2026-04-07T10:00:00Z' }),
      createSite({ id: 'site-row-2', siteId: '22222222-2222-2222-2222-222222222222', lastSeenAt: '2026-04-05T09:00:00Z' }),
      createSite({ id: 'site-row-3', siteId: '33333333-3333-3333-3333-333333333333', lastSeenAt: null }),
    ]

    const result = filterSites(sites, {
      keyword: '',
      groupId: '',
      onlyUnassigned: false,
      onlyRecentlyActive: true,
      now: '2026-04-07T12:00:00Z',
    })

    expect(result.map((item) => item.id)).toEqual(['site-row-1'])
  })

  it('should find site row by stable site id for cross-page navigation', () => {
    const sites = [
      createSite({ id: 'site-row-1', siteId: '11111111-1111-1111-1111-111111111111' }),
      createSite({ id: 'site-row-2', siteId: '22222222-2222-2222-2222-222222222222' }),
    ]

    const result = findSiteRowBySiteId(sites, '22222222-2222-2222-2222-222222222222')

    expect(result?.id).toBe('site-row-2')
  })

  it('should prefer site name as search seed and fallback to site id', () => {
    expect(getSiteSearchSeed(createSite({ siteName: '门诊登记台 2', siteId: '22222222-2222-2222-2222-222222222222' }))).toBe('门诊登记台 2')
    expect(getSiteSearchSeed(createSite({ siteName: '', siteId: '33333333-3333-3333-3333-333333333333' }))).toBe('33333333-3333-3333-3333-333333333333')
  })

  it('should keep selected site visible when override search keyword excludes it', () => {
    const sites = [
      createSite({ id: 'site-row-1', siteName: '门诊登记台 1', siteId: '11111111-1111-1111-1111-111111111111' }),
      createSite({ id: 'site-row-2', siteName: '病区治疗台', siteId: '22222222-2222-2222-2222-222222222222' }),
    ]

    const result = filterSitesForSelection(sites, {
      keyword: '病区',
      selectedSiteRowId: 'site-row-1',
    })

    expect(result.map((item) => item.id)).toEqual(['site-row-2', 'site-row-1'])
  })

  it('should build quick assign payload from analytics site id and target group', () => {
    const sites = [
      createSite({ id: 'site-row-1', siteId: '11111111-1111-1111-1111-111111111111' }),
      createSite({ id: 'site-row-2', siteId: '22222222-2222-2222-2222-222222222222' }),
    ]

    const result = buildQuickAssignGroupPayload(sites, {
      siteId: '22222222-2222-2222-2222-222222222222',
      groupId: 'group-b',
    })

    expect(result).toEqual({
      siteRowId: 'site-row-2',
      groupId: 'group-b',
    })
  })

  it('should return null for quick assign when target group missing or site not found', () => {
    const sites = [createSite({ id: 'site-row-1', siteId: '11111111-1111-1111-1111-111111111111' })]

    expect(buildQuickAssignGroupPayload(sites, {
      siteId: '11111111-1111-1111-1111-111111111111',
      groupId: '',
    })).toBeNull()

    expect(buildQuickAssignGroupPayload(sites, {
      siteId: '99999999-9999-9999-9999-999999999999',
      groupId: 'group-a',
    })).toBeNull()
  })
})
