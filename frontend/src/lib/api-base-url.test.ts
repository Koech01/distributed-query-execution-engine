import { afterEach, describe, expect, it, vi } from 'vitest'

import { resolveApiBaseUrl } from './api-base-url'

describe('resolveApiBaseUrl', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('uses same-origin when configured for the Vite proxy', () => {
    vi.stubEnv('VITE_API_BASE_URL', 'same-origin')
    vi.stubEnv('VITE_DEV_USE_PROXY', 'false')

    expect(resolveApiBaseUrl()).toBe(window.location.origin)
  })

  it('uses same-origin when dev proxy mode is enabled', () => {
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    vi.stubEnv('VITE_DEV_USE_PROXY', 'true')

    expect(resolveApiBaseUrl()).toBe(window.location.origin)
  })

  it('falls back to the configured backend URL outside proxy mode', () => {
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    vi.stubEnv('VITE_DEV_USE_PROXY', 'false')

    expect(resolveApiBaseUrl()).toBe('http://localhost:5281')
  })
})
