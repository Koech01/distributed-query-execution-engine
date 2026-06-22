import type { AuthUser, BackendOAuthProvider, JwtClaims, UserProfile } from '@/components/types'
import { resolveApiBaseUrl } from '@/lib/api-base-url'

const READ_SCOPE = 'query:read'
const ADMIN_SCOPE = 'query:admin'

let sessionUser: AuthUser | null = null

export function isAuthEnabled(): boolean {
  return import.meta.env.VITE_AUTH_ENABLED !== 'false'
}

export function getSessionUser(): AuthUser | null {
  if (!isAuthEnabled()) {
    return createDevBypassUser()
  }

  return sessionUser
}

export function setSessionUser(user: AuthUser | null): void {
  sessionUser = user
}

export function clearSession(): void {
  sessionUser = null
}

export function isSessionAuthenticated(): boolean {
  if (!isAuthEnabled()) {
    return true
  }

  return sessionUser !== null
}

export function profileToAuthUser(profile: UserProfile): AuthUser {
  return {
    subject: profile.userId,
    displayName: profile.displayName ?? undefined,
    email: profile.email,
    scopes: profile.scopes,
    roles: [],
  }
}

export function decodeClaims(token?: string | null): JwtClaims | null {
  if (!token) {
    return null
  }

  const parts = token.split('.')
  if (parts.length !== 3) {
    return null
  }

  try {
    const payload = base64UrlDecode(parts[1])
    const parsed: unknown = JSON.parse(payload)
    if (!parsed || typeof parsed !== 'object') {
      return null
    }

    return parsed as JwtClaims
  } catch {
    return null
  }
}

export function claimsToAuthUser(claims: JwtClaims): AuthUser {
  return {
    subject: claims.sub ?? 'unknown',
    displayName: claims.name,
    email: claims.email,
    scopes: extractScopes(claims),
    roles: extractRoles(claims),
  }
}

export function extractScopes(claims: JwtClaims): string[] {
  const values = [claims.scope, claims.scp].flatMap(normalizeClaimValues)
  return [...new Set(values)]
}

export function extractRoles(claims: JwtClaims): string[] {
  const values = [claims.roles, claims.role].flatMap(normalizeClaimValues)
  return [...new Set(values)]
}

export function hasScope(scope: string, claims?: JwtClaims | null): boolean {
  if (!claims) {
    return false
  }

  const scopes = extractScopes(claims)
  if (scopes.includes(scope)) {
    return true
  }

  const roles = extractRoles(claims)
  if (scope === READ_SCOPE) {
    return roles.some((role) => role === 'query-reader' || role === 'query-admin' || role === 'admin')
  }

  if (scope === ADMIN_SCOPE) {
    return roles.some((role) => role === 'query-admin' || role === 'admin')
  }

  return false
}

export function userHasScope(user: AuthUser | null, scope: string): boolean {
  if (!user) {
    return false
  }

  if (user.scopes.includes(scope)) {
    return true
  }

  if (scope === READ_SCOPE) {
    return user.roles.some((role) => role === 'query-reader' || role === 'query-admin' || role === 'admin')
  }

  if (scope === ADMIN_SCOPE) {
    return user.roles.some((role) => role === 'query-admin' || role === 'admin')
  }

  return false
}

export function hasValidSessionWithScope(scope: string): boolean {
  if (!isAuthEnabled()) {
    return true
  }

  return userHasScope(getSessionUser(), scope)
}

export function canReadQueries(): boolean {
  return hasValidSessionWithScope(READ_SCOPE)
}

export function canAdminister(): boolean {
  return hasValidSessionWithScope(ADMIN_SCOPE)
}

export function buildBackendOAuthLoginUrl(provider: BackendOAuthProvider, returnTo = '/query'): string {
  const path = `/auth/${provider}/login`
  const url = new URL(path, withTrailingSlash(getApiBaseUrlForAuth()))
  url.searchParams.set('returnTo', sanitizeReturnTo(returnTo))
  return url.toString()
}

export function initiateBackendOAuthLogin(provider: BackendOAuthProvider, returnTo = '/query'): void {
  window.location.assign(buildBackendOAuthLoginUrl(provider, returnTo))
}

export function isBackendOAuthProviderEnabled(provider: BackendOAuthProvider): boolean {
  if (provider === 'google') {
    return import.meta.env.VITE_OAUTH_GOOGLE_ENABLED !== 'false'
  }

  return import.meta.env.VITE_OAUTH_GITHUB_ENABLED !== 'false'
}

export function sanitizeReturnTo(returnTo: string): string {
  if (!returnTo.startsWith('/') || returnTo.startsWith('//')) {
    return '/query'
  }

  if (
    returnTo.startsWith('/login') ||
    returnTo.startsWith('/signup') ||
    returnTo.startsWith('/auth/callback') ||
    returnTo.startsWith('/unauthorized')
  ) {
    return '/query'
  }

  return returnTo
}

export function createDevBypassUser(): AuthUser {
  return {
    subject: 'dev-bypass',
    displayName: 'Development user',
    email: 'dev@local',
    scopes: [READ_SCOPE, ADMIN_SCOPE],
    roles: ['query-admin'],
  }
}

function normalizeClaimValues(value: string | string[] | undefined): string[] {
  if (!value) {
    return []
  }

  if (Array.isArray(value)) {
    return value.flatMap((entry) => entry.split(/\s+/).filter(Boolean))
  }

  return value.split(/\s+/).filter(Boolean)
}

function base64UrlDecode(value: string): string {
  const normalized = value.replace(/-/g, '+').replace(/_/g, '/')
  const padding = normalized.length % 4 === 0 ? '' : '='.repeat(4 - (normalized.length % 4))
  return atob(`${normalized}${padding}`)
}

function getApiBaseUrlForAuth(): string {
  return resolveApiBaseUrl()
}

function withTrailingSlash(value: string): string {
  return value.endsWith('/') ? value : `${value}/`
}

export function resetAuthSession(): void {
  clearSession()
}
