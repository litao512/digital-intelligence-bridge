const SEMVER_PATTERN = /^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<pre>[0-9A-Za-z.-]+))?(?:\+(?<build>[0-9A-Za-z.-]+))?$/

interface ParsedSemVer {
  major: number
  minor: number
  patch: number
  preRelease: string | null
}

function parseVersion(version: string): ParsedSemVer {
  const match = SEMVER_PATTERN.exec(version)
  if (!match?.groups) {
    throw new Error(`无效版本号：${version}`)
  }

  return {
    major: Number(match.groups.major),
    minor: Number(match.groups.minor),
    patch: Number(match.groups.patch),
    preRelease: match.groups.pre ?? null,
  }
}

function compareIdentifiers(left: string, right: string): number {
  const leftNumeric = /^\d+$/.test(left)
  const rightNumeric = /^\d+$/.test(right)

  if (leftNumeric && rightNumeric) {
    return Number(left) - Number(right)
  }

  if (leftNumeric) {
    return -1
  }

  if (rightNumeric) {
    return 1
  }

  return left.localeCompare(right)
}

function comparePreRelease(left: string | null, right: string | null): number {
  if (left === right) {
    return 0
  }

  if (left === null) {
    return 1
  }

  if (right === null) {
    return -1
  }

  const leftParts = left.split('.')
  const rightParts = right.split('.')
  const length = Math.max(leftParts.length, rightParts.length)

  for (let index = 0; index < length; index += 1) {
    const leftPart = leftParts[index]
    const rightPart = rightParts[index]

    if (leftPart === undefined) {
      return -1
    }

    if (rightPart === undefined) {
      return 1
    }

    const result = compareIdentifiers(leftPart, rightPart)
    if (result !== 0) {
      return result
    }
  }

  return 0
}

export function compareVersions(left: string, right: string): number {
  const leftVersion = parseVersion(left)
  const rightVersion = parseVersion(right)

  if (leftVersion.major !== rightVersion.major) {
    return leftVersion.major - rightVersion.major
  }

  if (leftVersion.minor !== rightVersion.minor) {
    return leftVersion.minor - rightVersion.minor
  }

  if (leftVersion.patch !== rightVersion.patch) {
    return leftVersion.patch - rightVersion.patch
  }

  return comparePreRelease(leftVersion.preRelease, rightVersion.preRelease)
}

export function isVersionInRange(version: string, minVersion: string, maxVersion: string): boolean {
  return compareVersions(version, minVersion) >= 0 && compareVersions(version, maxVersion) <= 0
}
