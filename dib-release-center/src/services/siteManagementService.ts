import type { SiteSummary } from '@/contracts/site-types'

export interface SiteFilterInput {
  keyword: string
  groupId: string
}

export function filterSites(sites: SiteSummary[], input: SiteFilterInput): SiteSummary[] {
  const keyword = input.keyword.trim().toLowerCase()
  const groupId = input.groupId.trim()

  return sites.filter((site) => {
    if (groupId && site.groupId !== groupId) {
      return false
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
