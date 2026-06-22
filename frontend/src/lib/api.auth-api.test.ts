import { HttpResponse, http } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/test/mocks/server'
import { AuthError } from './errors'
import { authApi } from './api'

function createTestJwt(): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
  const body = btoa(JSON.stringify({ scope: 'query:read', exp: Math.floor(Date.now() / 1000) + 3600 }))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')

  return `${header}.${body}.signature`
}

describe('authApi', () => {
  beforeEach(() => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('logs in with email and password', async () => {
    const accessToken = createTestJwt()

    server.use(
      http.post('*/auth/login', async ({ request }) => {
        const body = (await request.json()) as { email: string; password: string }
        expect(body.email).toBe('reader@example.com')
        expect(body.password).toBe('correct-horse-battery-staple')

        return HttpResponse.json({
          accessToken,
          expiresIn: 3600,
          tokenType: 'Bearer',
        })
      }),
    )

    const response = await authApi.login({
      email: 'reader@example.com',
      password: 'correct-horse-battery-staple',
    })

    expect(response.accessToken).toBe(accessToken)
    expect(response.tokenType).toBe('Bearer')
  })

  it('registers a new email account', async () => {
    const accessToken = createTestJwt()

    server.use(
      http.post('*/auth/register', async ({ request }) => {
        const body = (await request.json()) as {
          email: string
          password: string
          displayName: string
        }
        expect(body.email).toBe('new.user@example.com')
        expect(body.password).toBe('correct-horse-battery-staple')
        expect(body.displayName).toBe('New User')

        return HttpResponse.json({
          accessToken,
          expiresIn: 3600,
          tokenType: 'Bearer',
        })
      }),
    )

    const response = await authApi.register({
      email: 'new.user@example.com',
      password: 'correct-horse-battery-staple',
      displayName: 'New User',
    })

    expect(response.accessToken).toBe(accessToken)
    expect(response.tokenType).toBe('Bearer')
  })

  it('exchanges an OAuth callback code for an access token', async () => {
    const accessToken = createTestJwt()

    server.use(
      http.post('*/auth/token/exchange', async ({ request }) => {
        const body = (await request.json()) as { exchangeCode: string }
        expect(body.exchangeCode).toBe('exchange-code-123')

        return HttpResponse.json({
          accessToken,
          expiresIn: 3600,
          tokenType: 'Bearer',
        })
      }),
    )

    const response = await authApi.exchangeToken({ exchangeCode: 'exchange-code-123' })

    expect(response.accessToken).toBe(accessToken)
  })

  it('maps exchange failures to AuthError without redirecting to login', async () => {
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
      http.post('*/auth/token/exchange', () =>
        HttpResponse.json(
          {
            error: 'authentication_failed',
            message: 'Exchange code is invalid or expired.',
          },
          { status: 401 },
        ),
      ),
    )

    await expect(authApi.exchangeToken({ exchangeCode: 'expired-code' })).rejects.toBeInstanceOf(AuthError)
    expect(assignMock).not.toHaveBeenCalled()

    Object.defineProperty(window, 'location', {
      configurable: true,
      value: originalLocation,
    })
  })
})
