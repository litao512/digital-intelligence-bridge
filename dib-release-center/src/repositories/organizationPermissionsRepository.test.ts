import { beforeEach, describe, expect, it, vi } from 'vitest'

const orderMock = vi.fn()
const eqSecondMock = vi.fn()
const eqFirstMock = vi.fn()
const selectMock = vi.fn()
const upsertMock = vi.fn()
const updateMock = vi.fn()
const fromMock = vi.fn()
const schemaMock = vi.fn()

vi.mock('@/services/supabase', () => ({
  RELEASE_SCHEMA: 'dib_release',
  getSupabaseClient: () => ({
    schema: schemaMock,
  }),
}))

import {
  buildOrganizationPluginPermissionPayload,
  buildOrganizationResourcePermissionPayload,
  deactivateOrganizationPluginPermission,
  deactivateOrganizationResourcePermission,
  listOrganizationPluginPermissions,
  listOrganizationResourcePermissions,
  toOrganizationPluginPermission,
  toOrganizationResourcePermission,
  upsertOrganizationPluginPermission,
  upsertOrganizationResourcePermission,
} from '@/repositories/organizationPermissionsRepository'

describe('organizationPermissionsRepository', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    orderMock.mockResolvedValue({ data: [], error: null })
    eqSecondMock.mockResolvedValue({ error: null })
    eqFirstMock.mockReturnValue({ order: orderMock, eq: eqSecondMock })
    selectMock.mockReturnValue({ eq: eqFirstMock })
    upsertMock.mockResolvedValue({ error: null })
    updateMock.mockReturnValue({ eq: eqFirstMock })
    fromMock.mockReturnValue({ select: selectMock, upsert: upsertMock, update: updateMock })
    schemaMock.mockReturnValue({ from: fromMock })
  })

  it('should map plugin and resource permission rows', () => {
    expect(toOrganizationPluginPermission({
      id: 'permission-1',
      organization_id: 'org-1',
      plugin_code: 'patient-registration',
      status: 'Active',
      granted_by: 'admin',
      granted_at: '2026-04-30T01:00:00Z',
      expires_at: null,
    })).toMatchObject({
      organizationId: 'org-1',
      pluginCode: 'patient-registration',
      status: 'Active',
    })

    expect(toOrganizationResourcePermission({
      id: 'permission-2',
      organization_id: 'org-1',
      resource_id: 'resource-1',
      status: 'Inactive',
      granted_by: 'admin',
      granted_at: '2026-04-30T01:00:00Z',
      expires_at: '2026-05-01T00:00:00Z',
    })).toMatchObject({
      organizationId: 'org-1',
      resourceId: 'resource-1',
      status: 'Inactive',
      expiresAt: '2026-05-01T00:00:00Z',
    })
  })

  it('should build normalized upsert payloads', () => {
    expect(buildOrganizationPluginPermissionPayload({
      organizationId: 'org-1',
      pluginCode: ' patient-registration ',
      grantedBy: ' admin ',
    })).toEqual({
      organization_id: 'org-1',
      plugin_code: 'patient-registration',
      status: 'Active',
      granted_by: 'admin',
      expires_at: null,
    })

    expect(buildOrganizationResourcePermissionPayload({
      organizationId: 'org-1',
      resourceId: 'resource-1',
      status: 'Inactive',
      expiresAt: '2026-05-01T00:00:00Z',
    })).toEqual({
      organization_id: 'org-1',
      resource_id: 'resource-1',
      status: 'Inactive',
      granted_by: '',
      expires_at: '2026-05-01T00:00:00Z',
    })
  })

  it('should list plugin permissions for organization', async () => {
    await listOrganizationPluginPermissions('org-1')

    expect(fromMock).toHaveBeenCalledWith('organization_plugin_permissions')
    expect(eqFirstMock).toHaveBeenCalledWith('organization_id', 'org-1')
    expect(orderMock).toHaveBeenCalledWith('plugin_code', { ascending: true })
  })

  it('should upsert plugin permission by organization and plugin', async () => {
    await upsertOrganizationPluginPermission({ organizationId: 'org-1', pluginCode: 'patient-registration' })

    expect(upsertMock).toHaveBeenCalledWith({
      organization_id: 'org-1',
      plugin_code: 'patient-registration',
      status: 'Active',
      granted_by: '',
      expires_at: null,
    }, { onConflict: 'organization_id,plugin_code' })
  })

  it('should deactivate plugin permission by organization and plugin', async () => {
    await deactivateOrganizationPluginPermission('org-1', 'patient-registration')

    expect(updateMock).toHaveBeenCalledWith({ status: 'Inactive' })
    expect(eqFirstMock).toHaveBeenCalledWith('organization_id', 'org-1')
    expect(eqSecondMock).toHaveBeenCalledWith('plugin_code', 'patient-registration')
  })

  it('should list resource permissions for organization', async () => {
    await listOrganizationResourcePermissions('org-1')

    expect(fromMock).toHaveBeenCalledWith('organization_resource_permissions')
    expect(eqFirstMock).toHaveBeenCalledWith('organization_id', 'org-1')
    expect(orderMock).toHaveBeenCalledWith('resource_id', { ascending: true })
  })

  it('should upsert resource permission by organization and resource', async () => {
    await upsertOrganizationResourcePermission({ organizationId: 'org-1', resourceId: 'resource-1' })

    expect(upsertMock).toHaveBeenCalledWith({
      organization_id: 'org-1',
      resource_id: 'resource-1',
      status: 'Active',
      granted_by: '',
      expires_at: null,
    }, { onConflict: 'organization_id,resource_id' })
  })

  it('should deactivate resource permission by organization and resource', async () => {
    await deactivateOrganizationResourcePermission('org-1', 'resource-1')

    expect(updateMock).toHaveBeenCalledWith({ status: 'Inactive' })
    expect(eqFirstMock).toHaveBeenCalledWith('organization_id', 'org-1')
    expect(eqSecondMock).toHaveBeenCalledWith('resource_id', 'resource-1')
  })
})
