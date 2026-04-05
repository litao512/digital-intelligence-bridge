import type { Session, SupabaseClient, User } from '@supabase/supabase-js'
import { getSupabaseClient } from '@/services/supabase'

export interface ReleaseAuthState {
  session: Session | null
  user: User | null
}

export function normalizeAdminEmail(value: string): string {
  return value.trim().toLowerCase()
}

export function validatePasswordSignInInput(email: string, password: string): void {
  if (!normalizeAdminEmail(email)) {
    throw new Error('登录邮箱不能为空')
  }

  if (!password.trim()) {
    throw new Error('登录密码不能为空')
  }
}

function getClient(): SupabaseClient {
  return getSupabaseClient()
}

export async function signInWithPassword(email: string, password: string): Promise<void> {
  validatePasswordSignInInput(email, password)

  const { error } = await getClient().auth.signInWithPassword({
    email: normalizeAdminEmail(email),
    password,
  })

  if (error) {
    throw new Error(`登录失败：${error.message}`)
  }
}

export async function signOutReleaseCenter(): Promise<void> {
  const { error } = await getClient().auth.signOut()
  if (error) {
    throw new Error(`退出登录失败：${error.message}`)
  }
}

export async function getCurrentAuthState(): Promise<ReleaseAuthState> {
  const { data, error } = await getClient().auth.getSession()
  if (error) {
    throw new Error(`读取登录状态失败：${error.message}`)
  }

  return {
    session: data.session,
    user: data.session?.user ?? null,
  }
}

export function onReleaseAuthStateChange(
  callback: (state: ReleaseAuthState) => void,
): () => void {
  const { data } = getClient().auth.onAuthStateChange((_event, session) => {
    callback({
      session,
      user: session?.user ?? null,
    })
  })

  return () => {
    data.subscription.unsubscribe()
  }
}
