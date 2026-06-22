import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { mockAccountProfile, resetMockAccountProfile } from '@/test/mocks/handlers'
import { setMockAuthSession } from '@/test/mocks/auth-cookie'
import { clearSession } from './auth'
import { accountApi } from './api'

describe('accountApi', () => {
  beforeEach(() => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    resetMockAccountProfile()
    setMockAuthSession(['query:read'])
  })

  afterEach(() => {
    clearSession()
    vi.unstubAllEnvs()
    resetMockAccountProfile()
  })

  it('loads the authenticated user profile', async () => {
    const profile = await accountApi.getProfile()

    expect(profile.userId).toBe(mockAccountProfile.userId)
    expect(profile.email).toBe('reader@example.com')
    expect(profile.displayName).toBe('Reader User')
    expect(profile.hasPasswordLogin).toBe(true)
    expect(profile.scopes).toContain('query:read')
  })

  it('updates profile fields without re-issuing a token when email is unchanged', async () => {
    const response = await accountApi.updateProfile({ displayName: 'Updated Reader' })

    expect(response.profile.displayName).toBe('Updated Reader')
    expect(response.profile.email).toBe('reader@example.com')
    expect(response.token).toBeNull()
  })

  it('re-issues a token when the email address changes', async () => {
    const response = await accountApi.updateProfile({ email: 'updated.reader@example.com' })

    expect(response.profile.email).toBe('updated.reader@example.com')
    expect(response.token?.accessToken).toBeTruthy()
    expect(response.token?.tokenType).toBe('Bearer')
  })

  it('changes the password and returns a refreshed access token response', async () => {
    const response = await accountApi.changePassword({
      currentPassword: 'correct-horse-battery-staple',
      newPassword: 'another-secure-password',
    })

    expect(response.accessToken).toBeTruthy()
    expect(response.tokenType).toBe('Bearer')
  })

  it('deletes the authenticated account with 204 No Content', async () => {
    await expect(accountApi.deleteAccount()).resolves.toBeUndefined()
  })
})
