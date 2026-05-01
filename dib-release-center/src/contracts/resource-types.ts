export type ResourceStatus = 'Draft' | 'PendingApproval' | 'Active' | 'Disabled' | 'Archived'

export interface ResourceSummary {
  id: string
  resourceCode: string
  resourceName: string
  resourceType: string
  ownerOrganizationId: string | null
  visibilityScope: string
  capabilities: string[]
  businessTags: string[]
  status: ResourceStatus
  description: string
  createdAt: string
  updatedAt: string
}

export interface ResourceBindingSummary {
  id: string
  siteRowId: string
  pluginCode: string
  resourceId: string
  status: 'PendingActivation' | 'Active' | 'Suspended' | 'Revoked'
  usageKey: string
  configVersion: number
  createdAt: string
  updatedAt: string
}
