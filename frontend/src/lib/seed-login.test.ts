import { afterEach, describe, expect, it, vi } from 'vitest'

import { getDefaultSeedLoginPrefill, getSeedLoginAccounts, isSeedLoginPrefillEnabled } from '@/lib/seed-login'

describe('seed-login', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('is disabled by default', () => {
    vi.stubEnv('VITE_SEED_LOGIN_PREFILL_ENABLED', undefined)

    expect(isSeedLoginPrefillEnabled()).toBe(false)
    expect(getSeedLoginAccounts()).toEqual([])
    expect(getDefaultSeedLoginPrefill()).toBeNull()
  })

  it('returns configured accounts when enabled', () => {
    vi.stubEnv('VITE_SEED_LOGIN_PREFILL_ENABLED', 'true')
    vi.stubEnv('VITE_SEED_LOGIN_ADMIN_EMAIL', 'admin@example.com')
    vi.stubEnv('VITE_SEED_LOGIN_ADMIN_PASSWORD', 'ChangeMe-Admin-12')
    vi.stubEnv('VITE_SEED_LOGIN_USER_EMAIL', 'user@example.com')
    vi.stubEnv('VITE_SEED_LOGIN_USER_PASSWORD', 'ChangeMe-User-12')

    expect(getSeedLoginAccounts()).toEqual([
      {
        label: 'Admin',
        email: 'admin@example.com',
        password: 'ChangeMe-Admin-12',
      },
      {
        label: 'Standard user',
        email: 'user@example.com',
        password: 'ChangeMe-User-12',
      },
    ])
    expect(getDefaultSeedLoginPrefill()).toEqual({
      email: 'admin@example.com',
      password: 'ChangeMe-Admin-12',
    })
  })

  it('ignores incomplete credential pairs', () => {
    vi.stubEnv('VITE_SEED_LOGIN_PREFILL_ENABLED', 'true')
    vi.stubEnv('VITE_SEED_LOGIN_ADMIN_EMAIL', 'admin@example.com')
    vi.stubEnv('VITE_SEED_LOGIN_ADMIN_PASSWORD', '')

    expect(getSeedLoginAccounts()).toEqual([])
  })
})