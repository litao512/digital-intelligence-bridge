import { describe, expect, it } from 'vitest'
import type { SiteAnalyticsIssueRow } from '@/contracts/site-types'
import { filterIssueRows, isIssueRowHighlighted } from '@/services/siteAnalyticsViewService'

function createIssueRow(overrides: Partial<SiteAnalyticsIssueRow> = {}): SiteAnalyticsIssueRow {
  return {
    siteId: '11111111-1111-1111-1111-111111111111',
    siteName: '门诊登记台 1',
    groupName: '门诊基础版',
    clientVersion: '1.0.0',
    lastSeenAt: '2026-04-07T10:00:00Z',
    authorizedNotInstalled: ['patient-registration'],
    installedNotAuthorized: [],
    isUnassigned: false,
    hasAuthorizationDrift: true,
    ...overrides,
  }
}

describe('siteAnalyticsViewService', () => {
  it('should filter issue rows by unassigned and authorization drift flags', () => {
    const rows = [
      createIssueRow({ siteId: 'site-a', isUnassigned: false, hasAuthorizationDrift: true }),
      createIssueRow({ siteId: 'site-b', isUnassigned: true, hasAuthorizationDrift: false }),
      createIssueRow({ siteId: 'site-c', isUnassigned: true, hasAuthorizationDrift: true }),
    ]

    expect(filterIssueRows(rows, {
      onlyUnassigned: true,
      onlyAuthorizationDrift: false,
      clientVersion: '',
    }).map((item) => item.siteId)).toEqual(['site-b', 'site-c'])

    expect(filterIssueRows(rows, {
      onlyUnassigned: false,
      onlyAuthorizationDrift: true,
      clientVersion: '',
    }).map((item) => item.siteId)).toEqual(['site-a', 'site-c'])

    expect(filterIssueRows(rows, {
      onlyUnassigned: true,
      onlyAuthorizationDrift: true,
      clientVersion: '',
    }).map((item) => item.siteId)).toEqual(['site-c'])
  })

  it('should filter issue rows by client version when provided', () => {
    const rows = [
      createIssueRow({ siteId: 'site-a', clientVersion: '1.0.0' }),
      createIssueRow({ siteId: 'site-b', clientVersion: '1.0.1' }),
      createIssueRow({ siteId: 'site-c', clientVersion: '1.0.1' }),
    ]

    expect(filterIssueRows(rows, {
      onlyUnassigned: false,
      onlyAuthorizationDrift: false,
      clientVersion: '1.0.1',
    }).map((item) => item.siteId)).toEqual(['site-b', 'site-c'])
  })

  it('should mark only the assigned site as highlighted', () => {
    expect(isIssueRowHighlighted('site-a', 'site-a')).toBe(true)
    expect(isIssueRowHighlighted('site-a', 'site-b')).toBe(false)
    expect(isIssueRowHighlighted('site-a', '')).toBe(false)
  })
})
