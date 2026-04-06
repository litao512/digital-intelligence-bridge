import { describe, expect, it } from 'vitest'
import { buildSiteGroupAssignmentUpdate } from '@/repositories/sitesRepository'

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
})
