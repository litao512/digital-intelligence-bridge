const SHA256_PATTERN = /^[a-f0-9]{64}$/i

function encodeText(value: string): Uint8Array {
  return new TextEncoder().encode(value)
}

function toHex(buffer: ArrayBuffer): string {
  return [...new Uint8Array(buffer)]
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('')
}

function toArrayBuffer(content: Uint8Array): ArrayBuffer {
  return content.buffer.slice(content.byteOffset, content.byteOffset + content.byteLength) as ArrayBuffer
}

export function isSha256Hex(value: string): boolean {
  return SHA256_PATTERN.test(value)
}

export function assertSha256Hex(value: string, context: string): string {
  if (!isSha256Hex(value)) {
    throw new Error(`${context} 的 sha256 无效：${value}`)
  }

  return value.toLowerCase()
}

export async function computeSha256Hex(content: string | Uint8Array): Promise<string> {
  const source = typeof content === 'string' ? encodeText(content) : content
  const digest = await globalThis.crypto.subtle.digest('SHA-256', toArrayBuffer(source))
  return toHex(digest)
}
