export interface ClientManifest {
  channel: string
  latestVersion: string | null
  mandatory: boolean
  minUpgradeVersion: string | null
  packageUrl: string | null
  sha256: string | null
  fileName: string | null
  releaseNotes: string
  publishedAt: string | null
}
