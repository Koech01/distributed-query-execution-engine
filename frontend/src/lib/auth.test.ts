import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { UserProfile } from '@/components/types'
import {
  AUTH_ACCESS_TOKEN_COOKIE,
  buildAuthCookieHeader,
  parseAuthCookieHeader,
  requestHasAuthCookie,
} from './auth-cookie'
import {
  buildBackendOAuthLoginUrl,
  clearSession,
  decodeClaims,
  extractScopes,
  getSessionUser,
  hasScope,
  hasValidSessionWithScope,
  isBackendOAuthProviderEnabled,
  profileToAuthUser,
  sanitizeReturnTo,
  setSessionUser,
  userHasScope,
} from './auth'

function createTestJwt(payload: Record<string, unknown>): string {
  const header = base64UrlEncode(JSON.stringify({ alg: 'none', typ: 'JWT' }))
  const body = base64UrlEncode(JSON.stringify(payload))
  return `${header}.${body}.signature`
}

function base64UrlEncode(value: string): string {
  return btoa(value).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

const mockProfile: UserProfile = {
  userId: '11111111-1111-4111-8111-111111111111',
  email: 'reader@example.com',
  displayName: 'Reader User',
  hasPasswordLogin: true,
  linkedProviders: [],
  scopes: ['query:read'],
  createdAt: '2026-06-20T10:00:00.000Z',
  updatedAt: '2026-06-20T10:00:00.000Z',
}

describe('auth session manager', () => {
  beforeEach(() => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    clearSession()
  })

  afterEach(() => {
    clearSession()
    vi.unstubAllEnvs()
  })

  it('sets, gets, and clears the in-memory session user', () => {
    const user = profileToAuthUser(mockProfile)

    expect(getSessionUser()).toBeNull()

    setSessionUser(user)
    expect(getSessionUser()).toEqual(user)

    clearSession()
    expect(getSessionUser()).toBeNull()
  })

  it('maps account profiles into auth users without storing JWTs', () => {
    expect(profileToAuthUser(mockProfile)).toMatchObject({
      subject: mockProfile.userId,
      email: mockProfile.email,
      scopes: ['query:read'],
    })
  })

  it('decodes JWT claims when a token string is available for diagnostics only', () => {
    const token = createTestJwt({
      sub: 'user-123',
      name: 'Query Reader',
      email: 'reader@example.com',
      scope: 'openid query:read',
      exp: Math.floor(Date.now() / 1000) + 3600,
    })

    expect(decodeClaims(token)).toMatchObject({
      sub: 'user-123',
      name: 'Query Reader',
      email: 'reader@example.com',
    })
    expect(extractScopes(decodeClaims(token)!)).toEqual(['openid', 'query:read'])
  })
})

describe('auth scope checking', () => {
  it('accepts query:read and query:admin scopes from scope claims', () => {
    const readClaims = decodeClaims(createTestJwt({ scope: 'openid query:read' }))
    const adminClaims = decodeClaims(createTestJwt({ scope: 'query:read query:admin' }))

    expect(hasScope('query:read', readClaims)).toBe(true)
    expect(hasScope('query:admin', readClaims)).toBe(false)
    expect(hasScope('query:admin', adminClaims)).toBe(true)
  })

  it('accepts role equivalents for read and admin access', () => {
    const readerClaims = decodeClaims(createTestJwt({ role: 'query-reader' }))
    const adminClaims = decodeClaims(createTestJwt({ roles: ['query-admin'] }))

    expect(hasScope('query:read', readerClaims)).toBe(true)
    expect(hasScope('query:admin', readerClaims)).toBe(false)
    expect(hasScope('query:admin', adminClaims)).toBe(true)
  })

  it('validates route access from the in-memory session user only', () => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    clearSession()

    expect(hasValidSessionWithScope('query:read')).toBe(false)

    setSessionUser(profileToAuthUser(mockProfile))
    expect(hasValidSessionWithScope('query:read')).toBe(true)
    expect(hasValidSessionWithScope('query:admin')).toBe(false)
    expect(userHasScope(getSessionUser(), 'query:read')).toBe(true)
  })

  it('sanitizes unsafe return URLs', () => {
    expect(sanitizeReturnTo('/query?tab=results')).toBe('/query?tab=results')
    expect(sanitizeReturnTo('https://evil.example')).toBe('/query')
    expect(sanitizeReturnTo('/login/')).toBe('/query')
    expect(sanitizeReturnTo('/signup/')).toBe('/query')
    expect(sanitizeReturnTo('/auth/callback')).toBe('/query')
  })
})

describe('auth cookie helpers', () => {
  it('builds and parses the access token cookie header', () => {
    const header = buildAuthCookieHeader('signed-token-value', 7200)

    expect(header).toContain(`${AUTH_ACCESS_TOKEN_COOKIE}=signed-token-value`)
    expect(header).toContain('HttpOnly')
    expect(parseAuthCookieHeader(header)).toBe('signed-token-value')
    expect(parseAuthCookieHeader('theme=dark; dqee_access_token=abc123; Path=/')).toBe('abc123')
  })

  it('detects auth cookies on requests', () => {
    const request = new Request('https://example.com/auth/account', {
      headers: {
        cookie: `${AUTH_ACCESS_TOKEN_COOKIE}=token-value`,
      },
    })

    expect(requestHasAuthCookie(request)).toBe(true)
    expect(requestHasAuthCookie(new Request('https://example.com/auth/account'))).toBe(false)
  })
})

describe('backend OAuth helpers', () => {
  beforeEach(() => {
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    vi.stubEnv('VITE_DEV_USE_PROXY', 'false')
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('builds backend OAuth login URLs with a sanitized return path', () => {
    expect(buildBackendOAuthLoginUrl('google', '/query')).toBe(
      'http://localhost:5281/auth/google/login?returnTo=%2Fquery',
    )
    expect(buildBackendOAuthLoginUrl('github', '/history')).toBe(
      'http://localhost:5281/auth/github/login?returnTo=%2Fhistory',
    )
  })

  it('respects provider enablement flags from the environment', () => {
    vi.stubEnv('VITE_OAUTH_GOOGLE_ENABLED', 'false')
    vi.stubEnv('VITE_OAUTH_GITHUB_ENABLED', 'true')

    expect(isBackendOAuthProviderEnabled('google')).toBe(false)
    expect(isBackendOAuthProviderEnabled('github')).toBe(true)
  })
})

describe('auth development bypass', () => {
  it('treats auth as optional when VITE_AUTH_ENABLED=false', () => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'false')
    clearSession()

    expect(hasValidSessionWithScope('query:read')).toBe(true)
    expect(hasValidSessionWithScope('query:admin')).toBe(true)
  })
})
