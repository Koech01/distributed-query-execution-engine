import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession } from './auth'
import { queryApi } from './api'
import { AuthError } from './errors'
import { server } from '@/test/mocks/server'
import { setMockAuthSession } from '@/test/mocks/auth-cookie'
import { HttpResponse, http } from 'msw'

describe('queryApi auth handling', () => {
  beforeEach(() => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    setMockAuthSession(['query:read'])
  })

  afterEach(() => {
    clearSession()
    vi.unstubAllEnvs()
  })

  it('clears the session and redirects to login on 401', async () => {
    const assignMock = vi.fn()
    const originalLocation = window.location

    Object.defineProperty(window, 'location', {
      configurable: true,
      value: {
        ...originalLocation,
        assign: assignMock,
      },
    })

    server.use(
      http.post('*/queries', () =>
        HttpResponse.json(
          {
            error: 'unauthorized',
            message: 'Authentication required.',
          },
          { status: 401 },
        ),
      ),
    )

    await expect(
      queryApi.submit({
        sql: 'SELECT 1',
        parameters: [],
      }),
    ).rejects.toBeInstanceOf(AuthError)

    expect(assignMock).toHaveBeenCalledWith(expect.stringContaining('/login/?returnTo='))

    Object.defineProperty(window, 'location', {
      configurable: true,
      value: originalLocation,
    })
  })
})
