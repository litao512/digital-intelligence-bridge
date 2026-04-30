import { describe, expect, it } from 'vitest'
import { buildResourcePayload, toResourceSummary } from '@/repositories/resourcesRepository'

describe('resourcesRepository', () => {
  it('toResourceSummary should map owner organization and tags', () => {
    expect(toResourceSummary({
      id: 'resource-1',
      resource_code: 'his-db',
      resource_name: 'HIS 数据库',
      resource_type: 'PostgreSQL',
      owner_organization_id: 'org-1',
      owner_organization_name: 'A 医院',
      visibility_scope: 'Private',
      capabilities: ['registration'],
      business_tags: ['门诊'],
      status: 'Active',
      description: '业务库',
      created_at: '2026-04-30T01:00:00Z',
      updated_at: '2026-04-30T01:00:00Z',
    })).toMatchObject({
      resourceCode: 'his-db',
      ownerOrganizationId: 'org-1',
      businessTags: ['门诊'],
      status: 'Active',
    })
  })

  it('buildResourcePayload should normalize arrays and text fields', () => {
    expect(buildResourcePayload({
      resourceCode: ' his-db ',
      resourceName: ' HIS 数据库 ',
      resourceType: 'PostgreSQL',
      ownerOrganizationId: '',
      ownerOrganizationName: ' A 医院 ',
      visibilityScope: 'Private',
      capabilities: [' registration ', ''],
      businessTags: [' 门诊 '],
      status: 'Active',
      description: ' 业务库 ',
    })).toEqual({
      resource_code: 'his-db',
      resource_name: 'HIS 数据库',
      resource_type: 'PostgreSQL',
      owner_organization_id: null,
      owner_organization_name: 'A 医院',
      visibility_scope: 'Private',
      capabilities: ['registration'],
      business_tags: ['门诊'],
      status: 'Active',
      description: '业务库',
    })
  })
})
