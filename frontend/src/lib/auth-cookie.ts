/** HttpOnly cookie name issued by the backend on auth responses. */
export const AUTH_ACCESS_TOKEN_COOKIE = 'dqee_access_token'

export function buildAuthCookieHeader(token: string, maxAgeSeconds = 3600): string {
  return `${AUTH_ACCESS_TOKEN_COOKIE}=${token}; Path=/; HttpOnly; SameSite=Lax; Max-Age=${maxAgeSeconds}`
}

export function parseAuthCookieHeader(cookieHeader: string | null): string | null {
  if (!cookieHeader) {
    return null
  }

  const prefix = `${AUTH_ACCESS_TOKEN_COOKIE}=`
  const segments = cookieHeader.split(';')

  for (const segment of segments) {
    const trimmed = segment.trim()
    if (trimmed.startsWith(prefix)) {
      return trimmed.slice(prefix.length)
    }
  }

  return null
}

export function requestHasAuthCookie(request: Request): boolean {
  return parseAuthCookieHeader(request.headers.get('cookie')) !== null
}
