export interface SiteGroup {
  id: string
  groupCode: string
  groupName: string
  description: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface SiteSummary {
  id: string
  siteId: string
  siteName: string
  groupId: string | null
  groupCode: string | null
  groupName: string | null
  channelId: string | null
  channelCode: string | null
  channelName: string | null
  clientVersion: string
  machineName: string
  lastSeenAt: string | null
  lastUpdateCheckAt: string | null
  lastPluginDownloadAt: string | null
  lastClientDownloadAt: string | null
  installedPlugins: string[]
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface SiteGroupPluginPolicy {
  id: string
  groupId: string
  pluginPackageId: string
  pluginCode: string
  pluginName: string
  isEnabled: boolean
  minClientVersion: string
  maxClientVersion: string
  createdAt: string
  updatedAt: string
}

export type SitePluginOverrideAction = 'allow' | 'deny'

export interface SitePluginOverride {
  id: string
  siteRowId: string
  pluginPackageId: string
  pluginCode: string
  pluginName: string
  action: SitePluginOverrideAction
  reason: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ResolvedSitePluginPolicy {
  pluginPackageId: string
  pluginCode: string
  pluginName: string
  minClientVersion: string
  maxClientVersion: string
  effectiveIsEnabled: boolean
  source: 'group' | 'site-override'
  overrideAction: SitePluginOverrideAction | null
}

export interface SiteAnalyticsGroupBreakdown {
  groupCode: string
  groupName: string
  count: number
}

export interface SiteAnalyticsVersionBreakdown {
  version: string
  count: number
  activeCount24h: number
}

export interface SiteAuthorizationDriftItem {
  siteId: string
  siteName: string
  groupName: string
  clientVersion: string
  lastSeenAt: string | null
  authorizedNotInstalled: string[]
  installedNotAuthorized: string[]
}

export interface SiteAnalyticsOverview {
  totalSiteCount: number
  activeSiteCount24h: number
  unassignedSiteCount: number
  groupPolicyCount: number
  overrideCount: number
  driftSiteCount: number
  authorizedButNotInstalledSiteCount: number
  installedButNotAuthorizedSiteCount: number
}

export interface SiteAnalyticsGroupRow {
  groupCode: string
  groupName: string
  siteCount: number
  activeSiteCount24h: number
  policyCount: number
  driftSiteCount: number
}

export interface SiteAnalyticsSummary {
  overview: SiteAnalyticsOverview
  groupBreakdown: SiteAnalyticsGroupBreakdown[]
  groupRows: SiteAnalyticsGroupRow[]
  versionBreakdown: SiteAnalyticsVersionBreakdown[]
  authorizationDrift: SiteAuthorizationDriftItem[]
}
