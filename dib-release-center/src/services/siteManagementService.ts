import type { SiteSummary } from '@/contracts/site-types'

export interface SiteFilterInput {
  keyword: string
  groupId: string
  onlyUnassigned?: boolean
  onlyRecentlyActive?: boolean
  now?: string
}

export function filterSites(sites: SiteSummary[], input: SiteFilterInput): SiteSummary[] {
  const keyword = input.keyword.trim().toLowerCase()
  const groupId = input.groupId.trim()
  const now = input.now ? new Date(input.now) : new Date()
  const activeThreshold = new Date(now.getTime() - 24 * 60 * 60 * 1000)

  return sites.filter((site) => {
    if (groupId && site.groupId !== groupId) {
      return false
    }

    if (input.onlyUnassigned && site.groupId) {
      return false
    }

    if (input.onlyRecentlyActive) {
      if (!site.lastSeenAt || new Date(site.lastSeenAt) < activeThreshold) {
        return false
      }
    }

    if (!keyword) {
      return true
    }

    const haystacks = [
      site.siteName,
      site.siteId,
      site.machineName,
      site.groupName ?? '',
      site.groupCode ?? '',
      site.clientVersion,
    ]

    return haystacks.some((item) => item.toLowerCase().includes(keyword))
  })
}

export function findSiteRowBySiteId(sites: SiteSummary[], siteId: string): SiteSummary | undefined {
  const normalizedSiteId = siteId.trim()

  if (!normalizedSiteId) {
    return undefined
  }

  return sites.find((site) => site.siteId === normalizedSiteId)
}

export function getSiteSearchSeed(site: Pick<SiteSummary, 'siteName' | 'siteId'>): string {
  const siteName = site.siteName.trim()

  return siteName || site.siteId
}

export function filterSitesForSelection(
  sites: SiteSummary[],
  input: {
    keyword: string
    selectedSiteRowId: string
  },
): SiteSummary[] {
  const filteredSites = filterSites(sites, {
    keyword: input.keyword,
    groupId: '',
    onlyUnassigned: false,
    onlyRecentlyActive: false,
  })

  if (!input.selectedSiteRowId || filteredSites.some((site) => site.id === input.selectedSiteRowId)) {
    return filteredSites
  }

  const selectedSite = sites.find((site) => site.id === input.selectedSiteRowId)

  if (!selectedSite) {
    return filteredSites
  }

  return [...filteredSites, selectedSite]
}

export function buildQuickAssignGroupPayload(
  sites: SiteSummary[],
  input: {
    siteId: string
    groupId: string
  },
): { siteRowId: string; groupId: string } | null {
  const normalizedGroupId = input.groupId.trim()

  if (!normalizedGroupId) {
    return null
  }

  const site = findSiteRowBySiteId(sites, input.siteId)

  if (!site) {
    return null
  }

  return {
    siteRowId: site.id,
    groupId: normalizedGroupId,
  }
}
