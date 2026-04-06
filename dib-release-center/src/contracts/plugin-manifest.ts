export interface PluginManifestItem {
  pluginId: string
  name: string
  version: string | null
  mandatory: boolean
  dibMinVersion: string | null
  dibMaxVersion: string | null
  packageUrl: string | null
  sha256: string | null
}

export interface PluginManifest {
  channel: string
  siteId?: string | null
  publishedAt: string | null
  plugins: PluginManifestItem[]
}
