import { createClient, type SupabaseClient } from '@supabase/supabase-js'

export const RELEASE_SCHEMA = 'dib_release'

let supabaseClient: SupabaseClient | null = null

function getRequiredEnv(name: 'VITE_SUPABASE_URL' | 'VITE_SUPABASE_ANON_KEY'): string {
  const value = import.meta.env[name]
  if (!value) {
    throw new Error(`${name} 未配置，无法初始化 Supabase 客户端。`)
  }

  return value
}

export function isSupabaseConfigured(): boolean {
  return Boolean(import.meta.env.VITE_SUPABASE_URL && import.meta.env.VITE_SUPABASE_ANON_KEY)
}

export function getSupabaseClient(): SupabaseClient {
  if (!supabaseClient) {
    supabaseClient = createClient(
      getRequiredEnv('VITE_SUPABASE_URL'),
      getRequiredEnv('VITE_SUPABASE_ANON_KEY'),
    )
  }

  return supabaseClient
}
