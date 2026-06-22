import { AUTH_ACCESS_TOKEN_COOKIE } from '@/lib/auth-cookie'
import { mockAccountProfile } from '@/test/mocks/handlers'

export function setMockAuthCookie(token: string, scopes: string[] = ['query:read']): void {
  mockAccountProfile.scopes = scopes
  document.cookie = `${AUTH_ACCESS_TOKEN_COOKIE}=${token}; path=/`
}

export function clearMockAuthCookie(): void {
  document.cookie = `${AUTH_ACCESS_TOKEN_COOKIE}=; Max-Age=0; path=/`
}

export function createMockAuthCookieToken(scope = 'query:read'): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
  const body = btoa(JSON.stringify({ scope, exp: Math.floor(Date.now() / 1000) + 3600 }))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')

  return `${header}.${body}.signature`
}

export function setMockAuthSession(scopes: string[] = ['query:read']): void {
  setMockAuthCookie(createMockAuthCookieToken(scopes.join(' ')), scopes)
}
