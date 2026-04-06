import type {
  SiteAnalyticsSummary,
  ResolvedSitePluginPolicy,
  SiteGroup,
  SiteGroupPluginPolicy,
  SitePluginOverride,
  SiteSummary,
} from '@/contracts/site-types'
import { isVersionInRange } from '@/utils/version'

interface ResolveSiteAuthorizedPluginsInput {
  site: SiteSummary
  groupPolicies: SiteGroupPluginPolicy[]
  siteOverrides: SitePluginOverride[]
}

interface AggregateSiteAnalyticsInput {
  sites: SiteSummary[]
  groups: SiteGroup[]
  groupPolicies: SiteGroupPluginPolicy[]
  siteOverrides: SitePluginOverride[]
  now?: string
}

export function resolveSiteAuthorizedPlugins(
  input: ResolveSiteAuthorizedPluginsInput,
): ResolvedSitePluginPolicy[] {
  const { site, groupPolicies, siteOverrides } = input
  const grouped = new Map<string, ResolvedSitePluginPolicy>()

  for (const policy of groupPolicies.filter((item) => item.groupId === site.groupId)) {
    grouped.set(policy.pluginPackageId, {
      pluginPackageId: policy.pluginPackageId,
      pluginCode: policy.pluginCode,
      pluginName: policy.pluginName,
      minClientVersion: policy.minClientVersion,
      maxClientVersion: policy.maxClientVersion,
      effectiveIsEnabled: policy.isEnabled,
      source: 'group',
      overrideAction: null,
    })
  }

  for (const override of siteOverrides.filter((item) => item.siteRowId === site.id && item.isActive)) {
    const existing = grouped.get(override.pluginPackageId)
    grouped.set(override.pluginPackageId, {
      pluginPackageId: override.pluginPackageId,
      pluginCode: override.pluginCode,
      pluginName: override.pluginName,
      minClientVersion: existing?.minClientVersion ?? '0.0.0',
      maxClientVersion: existing?.maxClientVersion ?? '9999.9999.9999',
      effectiveIsEnabled: override.action === 'allow',
      source: existing ? 'group' : 'site-override',
      overrideAction: override.action,
    })
  }

  return [...grouped.values()]
    .filter((item) => item.effectiveIsEnabled)
    .filter((item) => isVersionInRange(site.clientVersion, item.minClientVersion, item.maxClientVersion))
    .sort((left, right) => left.pluginCode.localeCompare(right.pluginCode))
}

export function aggregateSiteAnalytics(input: AggregateSiteAnalyticsInput): SiteAnalyticsSummary {
  const now = input.now ? new Date(input.now) : new Date()
  const activeThreshold = new Date(now.getTime() - 24 * 60 * 60 * 1000)

  const groupBreakdownMap = new Map<string, { groupCode: string; groupName: string; count: number }>()
  const versionBreakdownMap = new Map<string, number>()
  const authorizationDrift = input.sites.map((site) => {
    const authorizedPlugins = resolveSiteAuthorizedPlugins({
      site,
      groupPolicies: input.groupPolicies,
      siteOverrides: input.siteOverrides,
    }).map((item) => item.pluginCode)

    const installedSet = new Set(site.installedPlugins)
    const authorizedSet = new Set(authorizedPlugins)

    const groupCode = site.groupCode ?? 'unassigned'
    const groupName = site.groupName ?? '未分组'
    const existingGroup = groupBreakdownMap.get(groupCode)
    groupBreakdownMap.set(groupCode, {
      groupCode,
      groupName,
      count: (existingGroup?.count ?? 0) + 1,
    })

    versionBreakdownMap.set(site.clientVersion, (versionBreakdownMap.get(site.clientVersion) ?? 0) + 1)

    return {
      siteId: site.siteId,
      siteName: site.siteName,
      authorizedNotInstalled: authorizedPlugins.filter((pluginCode) => !installedSet.has(pluginCode)),
      installedNotAuthorized: site.installedPlugins.filter((pluginCode) => !authorizedSet.has(pluginCode)),
    }
  })

  return {
    totalSiteCount: input.sites.length,
    activeSiteCount24h: input.sites.filter((site) => site.lastSeenAt && new Date(site.lastSeenAt) >= activeThreshold).length,
    unassignedSiteCount: input.sites.filter((site) => !site.groupId).length,
    groupBreakdown: [...groupBreakdownMap.values()].sort((left, right) => left.groupCode.localeCompare(right.groupCode)),
    versionBreakdown: [...versionBreakdownMap.entries()]
      .map(([version, count]) => ({ version, count }))
      .sort((left, right) => left.version.localeCompare(right.version)),
    authorizationDrift,
  }
}
