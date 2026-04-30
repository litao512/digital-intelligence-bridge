import type { OrganizationSummary } from '@/contracts/organization-types'

export interface OrganizationFilterInput {
  keyword: string
  organizationType: string
  includeInactive?: boolean
}

export function createDefaultOrganizationFilterInput(): OrganizationFilterInput {
  return {
    keyword: '',
    organizationType: '',
    includeInactive: false,
  }
}

export function normalizeBusinessTags(value: string | string[]): string[] {
  const tags = Array.isArray(value) ? value : value.split(/[,\n，、]/)

  return Array.from(new Set(tags.map((tag) => tag.trim()).filter(Boolean)))
}

export function filterOrganizations(
  organizations: OrganizationSummary[],
  input: OrganizationFilterInput,
): OrganizationSummary[] {
  const keyword = input.keyword.trim().toLowerCase()
  const organizationType = input.organizationType.trim()

  return organizations.filter((organization) => {
    if (!input.includeInactive && organization.status !== 'Active') {
      return false
    }

    if (organizationType && organization.organizationType !== organizationType) {
      return false
    }

    if (!keyword) {
      return true
    }

    const haystacks = [
      organization.code,
      organization.name,
      organization.organizationType,
      ...organization.businessTags,
    ]

    return haystacks.some((item) => item.toLowerCase().includes(keyword))
  })
}
