import type { SiteAnalyticsIssueRow } from '@/contracts/site-types'

export interface SiteAnalyticsIssueFilterInput {
  onlyUnassigned: boolean
  onlyAuthorizationDrift: boolean
  clientVersion: string
}

export function filterIssueRows(
  rows: SiteAnalyticsIssueRow[],
  input: SiteAnalyticsIssueFilterInput,
): SiteAnalyticsIssueRow[] {
  return rows.filter((item) => {
    if (input.onlyUnassigned && !item.isUnassigned) {
      return false
    }

    if (input.onlyAuthorizationDrift && !item.hasAuthorizationDrift) {
      return false
    }

    if (input.clientVersion.trim() && item.clientVersion !== input.clientVersion.trim()) {
      return false
    }

    return true
  })
}

export function isIssueRowHighlighted(siteId: string, highlightedSiteId: string): boolean {
  return Boolean(siteId.trim() && highlightedSiteId.trim() && siteId === highlightedSiteId)
}
