import { describe, expect, it } from 'vitest'
import { normalizeAdminEmail, validatePasswordSignInInput } from '@/services/releaseAuthService'

describe('releaseAuthService', () => {
  it('normalizeAdminEmail should trim and lowercase admin email', () => {
    expect(normalizeAdminEmail('  Admin@Example.COM  ')).toBe('admin@example.com')
  })

  it('validatePasswordSignInInput should reject empty password', () => {
    expect(() => validatePasswordSignInInput('admin@example.com', '   ')).toThrow('登录密码不能为空')
  })
})
