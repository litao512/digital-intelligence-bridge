import type { SiteAnalyticsIssueRow } from '@/contracts/site-types'

export interface SiteAnalyticsIssueFilterInput {
  onlyUnassigned: boolean
  onlyAuthorizationDrift: boolean
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

    return true
  })
}

export function isIssueRowHighlighted(siteId: string, highlightedSiteId: string): boolean {
  return Boolean(siteId.trim() && highlightedSiteId.trim() && siteId === highlightedSiteId)
}
