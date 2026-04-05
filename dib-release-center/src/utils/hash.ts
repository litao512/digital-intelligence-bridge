const SHA256_PATTERN = /^[a-f0-9]{64}$/i

export function isSha256Hex(value: string): boolean {
  return SHA256_PATTERN.test(value)
}

export function assertSha256Hex(value: string, context: string): string {
  if (!isSha256Hex(value)) {
    throw new Error(`${context} 的 sha256 无效：${value}`)
  }

  return value.toLowerCase()
}
