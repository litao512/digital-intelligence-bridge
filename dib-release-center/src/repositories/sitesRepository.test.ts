import { describe, expect, it } from 'vitest'
import {
  buildSiteGroupAssignmentUpdate,
  buildSiteOrganizationAssignmentUpdate,
  toSiteSummary,
} from '@/repositories/sitesRepository'

describe('sitesRepository', () => {
  it('buildSiteGroupAssignmentUpdate should map selected group into database payload', () => {
    expect(buildSiteGroupAssignmentUpdate('group-outpatient')).toEqual({
      group_id: 'group-outpatient',
    })
  })

  it('buildSiteGroupAssignmentUpdate should allow clearing the group', () => {
    expect(buildSiteGroupAssignmentUpdate(null)).toEqual({
      group_id: null,
    })
  })

  it('buildSiteOrganizationAssignmentUpdate should map selected organization into database payload', () => {
    expect(buildSiteOrganizationAssignmentUpdate('org-1')).toEqual({
      organization_id: 'org-1',
    })
    expect(buildSiteOrganizationAssignmentUpdate(null)).toEqual({
      organization_id: null,
    })
  })

  it('toSiteSummary should map organization fields and business tags', () => {
    expect(toSiteSummary({
      id: 'site-row-1',
      site_id: '11111111-1111-1111-1111-111111111111',
      site_name: '门诊登记台 1',
      group_id: 'group-a',
      group_code: 'outpatient-basic',
      group_name: '门诊基础版',
      organization_id: 'org-1',
      organization_code: 'hospital-a',
      organization_name: 'A 医院',
      channel_id: 'channel-stable',
      channel_code: 'stable',
      channel_name: '稳定版',
      client_version: '1.0.0',
      machine_name: 'WIN-CLIENT-01',
      last_seen_at: null,
      last_update_check_at: null,
      last_plugin_download_at: null,
      last_client_download_at: null,
      installed_plugins_json: ['patient-registration'],
      business_tags: ['门诊', '随访'],
      is_active: true,
      created_at: '2026-04-06T12:00:00Z',
      updated_at: '2026-04-06T12:00:00Z',
    })).toMatchObject({
      organizationId: 'org-1',
      organizationCode: 'hospital-a',
      organizationName: 'A 医院',
      businessTags: ['门诊', '随访'],
    })
  })
})
