import type {
  OrganizationPluginPermission,
  OrganizationResourcePermission,
} from '@/contracts/organization-types'

type Permission = Pick<OrganizationPluginPermission, 'status' | 'expiresAt'>

export function isPermissionActive(permission: Permission, nowInput: string | Date = new Date()): boolean {
  if (permission.status !== 'Active') {
    return false
  }

  if (!permission.expiresAt) {
    return true
  }

  const now = typeof nowInput === 'string' ? new Date(nowInput) : nowInput
  return new Date(permission.expiresAt) > now
}

export function getActivePluginCodes(
  permissions: OrganizationPluginPermission[],
  nowInput: string | Date = new Date(),
): string[] {
  return Array.from(new Set(
    permissions
      .filter((permission) => isPermissionActive(permission, nowInput))
      .map((permission) => permission.pluginCode),
  ))
}

export function getActiveResourceIds(
  permissions: OrganizationResourcePermission[],
  nowInput: string | Date = new Date(),
): string[] {
  return Array.from(new Set(
    permissions
      .filter((permission) => isPermissionActive(permission, nowInput))
      .map((permission) => permission.resourceId),
  ))
}
