import type { PostgrestError } from '@supabase/supabase-js'
import type { OrganizationSummary } from '@/contracts/organization-types'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'

interface OrganizationRow {
  id: string
  code: string
  name: string
  organization_type: string
  business_tags: string[] | null
  status: 'Active' | 'Inactive'
  created_at: string
  updated_at: string
}

export interface OrganizationInput {
  code: string
  name: string
  organizationType: string
  businessTags: string[]
  status: 'Active' | 'Inactive'
}

interface OrganizationPayload {
  code: string
  name: string
  organization_type: string
  business_tags: string[]
  status: 'Active' | 'Inactive'
}

function throwIfError(error: PostgrestError | null, context: string): void {
  if (error) {
    throw new Error(`${context}失败：${error.message}`)
  }
}

export function toOrganizationSummary(row: OrganizationRow): OrganizationSummary {
  return {
    id: row.id,
    code: row.code,
    name: row.name,
    organizationType: row.organization_type,
    businessTags: row.business_tags ?? [],
    status: row.status,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  }
}

export function buildOrganizationPayload(input: OrganizationInput): OrganizationPayload {
  return {
    code: input.code.trim(),
    name: input.name.trim(),
    organization_type: input.organizationType.trim() || 'Unknown',
    business_tags: input.businessTags.map((tag) => tag.trim()).filter(Boolean),
    status: input.status,
  }
}

export async function listOrganizations(): Promise<OrganizationSummary[]> {
  const { data, error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('organizations')
    .select('id, code, name, organization_type, business_tags, status, created_at, updated_at')
    .order('code', { ascending: true })

  throwIfError(error, '查询单位')
  return (data ?? []).map((row: unknown) => toOrganizationSummary(row as OrganizationRow))
}

export async function createOrganization(input: OrganizationInput): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('organizations')
    .insert(buildOrganizationPayload(input))

  throwIfError(error, '新增单位')
}

export async function updateOrganization(id: string, input: OrganizationInput): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .from('organizations')
    .update(buildOrganizationPayload(input))
    .eq('id', id)

  throwIfError(error, '更新单位')
}
