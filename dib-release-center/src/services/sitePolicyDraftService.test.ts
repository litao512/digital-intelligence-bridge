import { describe, expect, it } from 'vitest'
import { buildGroupPolicyDraftFromRecord, buildGroupPolicyUpsert, buildSiteOverrideUpsert } from '@/services/sitePolicyDraftService'

describe('sitePolicyDraftService', () => {
  it('should build upsert payload for group policy', () => {
    const payload = buildGroupPolicyUpsert({
      groupId: 'group-1',
      pluginPackageId: 'package-1',
      isEnabled: true,
      minClientVersion: '1.0.0',
      maxClientVersion: '1.9.99',
    })

    expect(payload).toEqual({
      group_id: 'group-1',
      package_id: 'package-1',
      is_enabled: true,
      min_client_version: '1.0.0',
      max_client_version: '1.9.99',
    })
  })

  it('should reject empty group id', () => {
    expect(() => buildGroupPolicyUpsert({
      groupId: ' ',
      pluginPackageId: 'package-1',
      isEnabled: true,
      minClientVersion: '1.0.0',
      maxClientVersion: '1.9.99',
    })).toThrow('站点分组不能为空')
  })

  it('should map policy record back to draft', () => {
    const draft = buildGroupPolicyDraftFromRecord({
      id: 'policy-1',
      groupId: 'group-1',
      pluginPackageId: 'package-1',
      pluginCode: 'patient-registration',
      pluginName: '就诊登记',
      isEnabled: false,
      minClientVersion: '1.1.0',
      maxClientVersion: '2.0.0',
      createdAt: '2026-04-06T12:00:00Z',
      updatedAt: '2026-04-06T12:00:00Z',
    })

    expect(draft).toEqual({
      groupId: 'group-1',
      pluginPackageId: 'package-1',
      isEnabled: false,
      minClientVersion: '1.1.0',
      maxClientVersion: '2.0.0',
    })
  })

  it('should build site override payload', () => {
    const payload = buildSiteOverrideUpsert({
      siteRowId: 'site-row-1',
      pluginPackageId: 'package-1',
      action: 'deny',
      reason: '站点临时禁用',
      isActive: true,
    })

    expect(payload).toEqual({
      site_id: 'site-row-1',
      package_id: 'package-1',
      action: 'deny',
      reason: '站点临时禁用',
      is_active: true,
    })
  })
})
