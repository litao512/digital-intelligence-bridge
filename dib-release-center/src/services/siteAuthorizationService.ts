import type {
  ResolvedSitePluginPolicy,
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
