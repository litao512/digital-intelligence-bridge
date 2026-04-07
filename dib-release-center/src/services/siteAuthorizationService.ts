import type {
  SiteAnalyticsSummary,
  SiteAnalyticsGroupRow,
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
  const versionBreakdownMap = new Map<string, { version: string; count: number; activeCount24h: number }>()
  const groupRowMap = new Map<string, SiteAnalyticsGroupRow>()
  const issueRows = input.sites.map((site) => {
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

    const isActive = Boolean(site.lastSeenAt && new Date(site.lastSeenAt) >= activeThreshold)
    const existingVersion = versionBreakdownMap.get(site.clientVersion)
    versionBreakdownMap.set(site.clientVersion, {
      version: site.clientVersion,
      count: (existingVersion?.count ?? 0) + 1,
      activeCount24h: (existingVersion?.activeCount24h ?? 0) + (isActive ? 1 : 0),
    })

    const existingGroupRow = groupRowMap.get(groupCode)
    const authorizedNotInstalled = authorizedPlugins.filter((pluginCode) => !installedSet.has(pluginCode))
    const installedNotAuthorized = site.installedPlugins.filter((pluginCode) => !authorizedSet.has(pluginCode))
    const hasAuthorizationDrift = authorizedNotInstalled.length > 0 || installedNotAuthorized.length > 0

    groupRowMap.set(groupCode, {
      groupCode,
      groupName,
      siteCount: (existingGroupRow?.siteCount ?? 0) + 1,
      activeSiteCount24h: (existingGroupRow?.activeSiteCount24h ?? 0) + (isActive ? 1 : 0),
      policyCount: existingGroupRow?.policyCount ?? input.groupPolicies.filter((item) => item.groupId === site.groupId).length,
      driftSiteCount: (existingGroupRow?.driftSiteCount ?? 0) + (hasAuthorizationDrift ? 1 : 0),
    })

    return {
      siteId: site.siteId,
      siteName: site.siteName,
      groupName,
      clientVersion: site.clientVersion,
      lastSeenAt: site.lastSeenAt,
      authorizedNotInstalled,
      installedNotAuthorized,
      isUnassigned: !site.groupId,
      hasAuthorizationDrift,
    }
  })

  const authorizationDrift = issueRows.filter((item) => item.hasAuthorizationDrift)
  const driftSiteCount = authorizationDrift.length
  const authorizedButNotInstalledSiteCount = authorizationDrift.filter((item) => item.authorizedNotInstalled.length > 0).length
  const installedButNotAuthorizedSiteCount = authorizationDrift.filter((item) => item.installedNotAuthorized.length > 0).length

  return {
    overview: {
      totalSiteCount: input.sites.length,
      activeSiteCount24h: input.sites.filter((site) => site.lastSeenAt && new Date(site.lastSeenAt) >= activeThreshold).length,
      unassignedSiteCount: input.sites.filter((site) => !site.groupId).length,
      groupPolicyCount: input.groupPolicies.length,
      overrideCount: input.siteOverrides.filter((item) => item.isActive).length,
      driftSiteCount,
      authorizedButNotInstalledSiteCount,
      installedButNotAuthorizedSiteCount,
    },
    groupBreakdown: [...groupBreakdownMap.values()].sort((left, right) => left.groupCode.localeCompare(right.groupCode)),
    groupRows: [...groupRowMap.values()].sort((left, right) => left.groupCode.localeCompare(right.groupCode)),
    versionBreakdown: [...versionBreakdownMap.values()]
      .sort((left, right) => left.version.localeCompare(right.version)),
    authorizationDrift: authorizationDrift
      .sort((left, right) => left.siteName.localeCompare(right.siteName)),
    issueRows: issueRows
      .sort((left, right) => left.siteName.localeCompare(right.siteName)),
  }
}
