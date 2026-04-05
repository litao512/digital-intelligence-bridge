export interface ReleaseChannel {
  id: string
  channelCode: string
  channelName: string
  description: string
  sortOrder: number
  isDefault: boolean
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface PluginPackage {
  id: string
  pluginCode: string
  pluginName: string
  entryType: string
  author: string
  description: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface PluginVersion {
  id: string
  packageId: string
  pluginCode: string
  pluginName: string
  channelId: string
  channelCode: string
  channelName: string
  version: string
  dibMinVersion: string
  dibMaxVersion: string
  releaseNotes: string
  manifestJson: Record<string, unknown>
  isPublished: boolean
  isMandatory: boolean
  publishedAt: string | null
  createdAt: string
  updatedAt: string
  assetId: string
  bucketName: string
  storagePath: string
  fileName: string
  assetKind: 'plugin_package' | 'client_package' | 'manifest'
  sha256: string
  sizeBytes: number
  mimeType: string
  packageUrl: string
}

export interface ClientVersion {
  id: string
  channelId: string
  channelCode: string
  channelName: string
  version: string
  minUpgradeVersion: string
  isPublished: boolean
  isMandatory: boolean
  releaseNotes: string
  publishedAt: string | null
  createdAt: string
  updatedAt: string
  assetId: string
  bucketName: string
  storagePath: string
  fileName: string
  assetKind: 'plugin_package' | 'client_package' | 'manifest'
  sha256: string
  sizeBytes: number
  mimeType: string
  packageUrl: string
}
