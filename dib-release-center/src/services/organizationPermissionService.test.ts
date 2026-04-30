import { describe, expect, it } from 'vitest'
import type {
  OrganizationPluginPermission,
  OrganizationResourcePermission,
} from '@/contracts/organization-types'
import {
  getActivePluginCodes,
  getActiveResourceIds,
  isPermissionActive,
} from '@/services/organizationPermissionService'

function createPluginPermission(overrides: Partial<OrganizationPluginPermission> = {}): OrganizationPluginPermission {
  return {
    id: 'permission-1',
    organizationId: 'org-1',
    pluginCode: 'patient-registration',
    status: 'Active',
    grantedBy: 'admin',
    grantedAt: '2026-04-30T01:00:00Z',
    expiresAt: null,
    ...overrides,
  }
}

function createResourcePermission(overrides: Partial<OrganizationResourcePermission> = {}): OrganizationResourcePermission {
  return {
    id: 'permission-1',
    organizationId: 'org-1',
    resourceId: 'resource-1',
    status: 'Active',
    grantedBy: 'admin',
    grantedAt: '2026-04-30T01:00:00Z',
    expiresAt: null,
    ...overrides,
  }
}

describe('organizationPermissionService', () => {
  it('should treat only active and unexpired permissions as active', () => {
    expect(isPermissionActive(createPluginPermission(), '2026-04-30T02:00:00Z')).toBe(true)
    expect(isPermissionActive(createPluginPermission({ status: 'Inactive' }), '2026-04-30T02:00:00Z')).toBe(false)
    expect(isPermissionActive(createPluginPermission({ expiresAt: '2026-04-30T03:00:00Z' }), '2026-04-30T02:00:00Z')).toBe(true)
    expect(isPermissionActive(createPluginPermission({ expiresAt: '2026-04-30T01:00:00Z' }), '2026-04-30T02:00:00Z')).toBe(false)
  })

  it('should return unique active plugin codes', () => {
    const permissions = [
      createPluginPermission({ id: 'permission-1', pluginCode: 'patient-registration' }),
      createPluginPermission({ id: 'permission-2', pluginCode: 'patient-registration' }),
      createPluginPermission({ id: 'permission-3', pluginCode: 'insurance-audit' }),
      createPluginPermission({ id: 'permission-4', pluginCode: 'disabled-plugin', status: 'Inactive' }),
      createPluginPermission({ id: 'permission-5', pluginCode: 'expired-plugin', expiresAt: '2026-04-29T00:00:00Z' }),
    ]

    expect(getActivePluginCodes(permissions, '2026-04-30T02:00:00Z')).toEqual(['patient-registration', 'insurance-audit'])
  })

  it('should return unique active resource ids', () => {
    const permissions = [
      createResourcePermission({ id: 'permission-1', resourceId: 'resource-1' }),
      createResourcePermission({ id: 'permission-2', resourceId: 'resource-1' }),
      createResourcePermission({ id: 'permission-3', resourceId: 'resource-2' }),
      createResourcePermission({ id: 'permission-4', resourceId: 'resource-3', status: 'Inactive' }),
      createResourcePermission({ id: 'permission-5', resourceId: 'resource-4', expiresAt: '2026-04-29T00:00:00Z' }),
    ]

    expect(getActiveResourceIds(permissions, '2026-04-30T02:00:00Z')).toEqual(['resource-1', 'resource-2'])
  })
})
