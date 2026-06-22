import { describe, expect, it } from 'vitest'

import { adminCacheFlushRequestSchema, PLAN_HASH_REGEX } from './schemas'

describe('admin cache schemas', () => {
  it('accepts an empty flush request for flush-all operations', () => {
    expect(adminCacheFlushRequestSchema.parse({})).toEqual({})
  })

  it('accepts a valid 64-character hexadecimal plan hash', () => {
    const planHash = 'a'.repeat(64)
    expect(PLAN_HASH_REGEX.test(planHash)).toBe(true)
    expect(adminCacheFlushRequestSchema.parse({ planHash })).toEqual({ planHash })
  })

  it('rejects plan hashes that are not 64-character hexadecimal values', () => {
    expect(() => adminCacheFlushRequestSchema.parse({ planHash: 'abc123' })).toThrow(
      /64-character hexadecimal/i,
    )
    expect(() => adminCacheFlushRequestSchema.parse({ planHash: 'g'.repeat(64) })).toThrow(
      /64-character hexadecimal/i,
    )
  })
})
