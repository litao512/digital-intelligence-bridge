import { describe, expect, it } from 'vitest'
import { buildResourceBindingPayload, toResourceBindingSummary } from '@/repositories/resourceBindingsRepository'

describe('resourceBindingsRepository', () => {
  it('toResourceBindingSummary should map database row', () => {
    expect(toResourceBindingSummary({
      id: 'binding-1',
      site_row_id: 'site-1',
      plugin_code: 'patient-registration',
      resource_id: 'resource-1',
      status: 'Active',
      usage_key: 'registration-db',
      config_version: 1,
      created_at: '2026-04-30T01:00:00Z',
      updated_at: '2026-04-30T01:00:00Z',
    })).toMatchObject({
      siteRowId: 'site-1',
      pluginCode: 'patient-registration',
      usageKey: 'registration-db',
    })
  })

  it('buildResourceBindingPayload should normalize binding payload', () => {
    expect(buildResourceBindingPayload({
      siteRowId: 'site-1',
      pluginCode: ' patient-registration ',
      resourceId: 'resource-1',
      usageKey: ' registration-db ',
    })).toEqual({
      site_row_id: 'site-1',
      plugin_code: 'patient-registration',
      resource_id: 'resource-1',
      usage_key: 'registration-db',
      binding_scope: 'PluginAtSite',
      status: 'Active',
    })
  })
})
