import type { PostgrestError } from '@supabase/supabase-js'
import type { ResourceSummary, ResourceStatus } from '@/contracts/resource-types'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'

interface ResourceRow {
  id: string
  resource_code: string
  resource_name: string
  resource_type: string
  owner_organization_id: string | null
  visibility_scope: string
  capabilities: string[] | null
  business_tags: string[] | null
  status: ResourceStatus
  description: string
  created_at: string
  updated_at: string
}

export interface ResourceInput {
  resourceCode: string
  resourceName: string
  resourceType: string
  ownerOrganizationId: string | null
  visibilityScope: string
  capabilities: string[]
  businessTags: string[]
  status: ResourceStatus
  description: string
}

function throwIfError(error: PostgrestError | null, context: string): void {
  if (error) {
    throw new Error(`${context}失败：${error.message}`)
  }
}

export function toResourceSummary(row: ResourceRow): ResourceSummary {
  return {
    id: row.id,
    resourceCode: row.resource_code,
    resourceName: row.resource_name,
    resourceType: row.resource_type,
    ownerOrganizationId: row.owner_organization_id,
    visibilityScope: row.visibility_scope,
    capabilities: row.capabilities ?? [],
    businessTags: row.business_tags ?? [],
    status: row.status,
    description: row.description,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  }
}

export function buildResourcePayload(input: ResourceInput) {
  return {
    resource_code: input.resourceCode.trim(),
    resource_name: input.resourceName.trim(),
    resource_type: input.resourceType,
    owner_organization_id: input.ownerOrganizationId || null,
    visibility_scope: input.visibilityScope,
    capabilities: input.capabilities.map((item) => item.trim()).filter(Boolean),
    business_tags: input.businessTags.map((item) => item.trim()).filter(Boolean),
    status: input.status,
    description: input.description.trim(),
  }
}

export async function listResources(): Promise<ResourceSummary[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('resources')
    .select('id, resource_code, resource_name, resource_type, owner_organization_id, visibility_scope, capabilities, business_tags, status, description, created_at, updated_at')
    .order('resource_code', { ascending: true })

  throwIfError(error, '查询资源')
  return (data ?? []).map((row: unknown) => toResourceSummary(row as ResourceRow))
}

export async function createResource(input: ResourceInput): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('resources')
    .insert(buildResourcePayload(input))

  throwIfError(error, '新增资源')
}

export async function updateResource(id: string, input: ResourceInput): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('resources')
    .update(buildResourcePayload(input))
    .eq('id', id)

  throwIfError(error, '更新资源')
}
