import { describe, expect, it } from 'vitest'

import { formatActiveQueryKind, formatElapsedSince, formatWorkerProbeStatus } from './admin-display'

describe('admin-display helpers', () => {
  it('maps active query kinds to readable labels', () => {
    expect(formatActiveQueryKind(0)).toBe('Sync')
    expect(formatActiveQueryKind(1)).toBe('Stream')
    expect(formatActiveQueryKind(2)).toBe('Async')
  })

  it('maps worker probe statuses to readable labels', () => {
    expect(formatWorkerProbeStatus(0)).toBe('Healthy')
    expect(formatWorkerProbeStatus(1)).toBe('Unhealthy')
    expect(formatWorkerProbeStatus(2)).toBe('Unreachable')
  })

  it('formats elapsed durations from startedAt timestamps', () => {
    const now = new Date('2026-06-20T12:02:15.000Z')
    expect(formatElapsedSince('2026-06-20T12:00:00.000Z', now)).toBe('2m 15s')
    expect(formatElapsedSince('2026-06-20T12:02:00.000Z', now)).toBe('15s')
  })
})
