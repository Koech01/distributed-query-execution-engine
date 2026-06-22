const DEFAULT_API_BASE_URL = 'http://localhost:5281'

function getSameOriginOrDefault(): string {
  if (typeof window !== 'undefined' && window.location?.origin) {
    return window.location.origin
  }

  return DEFAULT_API_BASE_URL
}

export function resolveApiBaseUrl(): string {
  const configured = import.meta.env.VITE_API_BASE_URL?.trim()

  if (configured === 'same-origin' || configured === '.') {
    return getSameOriginOrDefault()
  }

  if (import.meta.env.DEV && import.meta.env.VITE_DEV_USE_PROXY === 'true') {
    return getSameOriginOrDefault()
  }

  return configured || DEFAULT_API_BASE_URL
}
