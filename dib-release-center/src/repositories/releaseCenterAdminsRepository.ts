import type { PostgrestError } from '@supabase/supabase-js'
import { getSupabaseClient, RELEASE_SCHEMA } from '@/services/supabase'

export interface ReleaseCenterAdmin {
  id: string
  userId: string | null
  email: string
  displayName: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

interface ReleaseCenterAdminRow {
  id: string
  user_id: string | null
  email: string
  display_name: string
  is_active: boolean
  created_at: string
  updated_at: string
}

function toReleaseCenterAdmin(row: ReleaseCenterAdminRow): ReleaseCenterAdmin {
  return {
    id: row.id,
    userId: row.user_id,
    email: row.email,
    displayName: row.display_name,
    isActive: row.is_active,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  }
}

function throwIfError(error: PostgrestError | null, context: string): void {
  if (error) {
    throw new Error(`${context}失败：${error.message}`)
  }
}

export async function getCurrentAdminProfile(): Promise<ReleaseCenterAdmin | null> {
  const client = getSupabaseClient()
  const { data: userData, error: userError } = await client.auth.getUser()
  if (userError) {
    throw new Error(`读取当前用户失败：${userError.message}`)
  }

  const user = userData.user
  if (!user) {
    return null
  }

  const email = (user.email ?? '').toLowerCase()
  const { data, error } = await client
    .schema(RELEASE_SCHEMA)
    .from('release_center_admins')
    .select('id, user_id, email, display_name, is_active, created_at, updated_at')
    .or(`user_id.eq.${user.id},email.eq.${email}`)
    .eq('is_active', true)
    .limit(1)
    .maybeSingle()

  throwIfError(error, '查询发布中心管理员')
  return data ? toReleaseCenterAdmin(data as ReleaseCenterAdminRow) : null
}

export async function linkCurrentAdminUser(): Promise<void> {
  const { error } = await getSupabaseClient()
    .schema(RELEASE_SCHEMA)
    .rpc('link_current_admin_user')

  if (error) {
    throw new Error(`绑定管理员账号失败：${error.message}`)
  }
}
