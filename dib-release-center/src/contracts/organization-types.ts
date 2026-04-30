export type OrganizationStatus = 'Active' | 'Inactive'

export interface OrganizationSummary {
  id: string
  code: string
  name: string
  organizationType: string
  businessTags: string[]
  status: OrganizationStatus
  createdAt: string
  updatedAt: string
}

export interface OrganizationPluginPermission {
  id: string
  organizationId: string
  pluginCode: string
  status: OrganizationStatus
  grantedBy: string
  grantedAt: string
  expiresAt: string | null
}

export interface OrganizationResourcePermission {
  id: string
  organizationId: string
  resourceId: string
  status: OrganizationStatus
  grantedBy: string
  grantedAt: string
  expiresAt: string | null
}
