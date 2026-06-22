import { HttpResponse, http } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'

import { AuthCallbackPage } from '@/components/Auth/AuthCallbackPage'
import { AuthProvider } from '@/components/Auth/AuthProvider'
import { buildAuthCookieHeader } from '@/lib/auth-cookie'
import { clearSession, getSessionUser } from '@/lib/auth'
import { clearMockAuthCookie, setMockAuthCookie } from '@/test/mocks/auth-cookie'
import { server } from '@/test/mocks/server'

function createTestJwt(scope = 'query:read'): string {
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

function renderCallback(initialEntry: string) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/auth/callback" element={<AuthCallbackPage />} />
          <Route path="/query" element={<main>Query console</main>} />
          <Route path="/history" element={<main>History page</main>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

describe('AuthCallbackPage', () => {
  beforeEach(() => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    clearSession()
    clearMockAuthCookie()
  })

  afterEach(() => {
    clearSession()
    clearMockAuthCookie()
    vi.unstubAllEnvs()
  })

  it('exchanges backend OAuth callback codes and redirects to the return path', async () => {
    const accessToken = createTestJwt()

    server.use(
      http.post('*/auth/token/exchange', async ({ request }) => {
        const body = (await request.json()) as { exchangeCode: string }
        expect(body.exchangeCode).toBe('backend-exchange-code')
        setMockAuthCookie(accessToken)

        return HttpResponse.json(
          {
            accessToken,
            expiresIn: 3600,
            tokenType: 'Bearer',
          },
          {
            headers: {
              'Set-Cookie': buildAuthCookieHeader(accessToken),
            },
          },
        )
      }),
    )

    renderCallback('/auth/callback?exchangeCode=backend-exchange-code&returnTo=%2Fhistory')

    expect(await screen.findByText('History page')).toBeInTheDocument()
    expect(getSessionUser()).toMatchObject({
      email: 'reader@example.com',
      scopes: ['query:read'],
    })
  })

  it('shows an error when the backend exchange code is invalid', async () => {
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

    renderCallback('/auth/callback?exchangeCode=expired-code&returnTo=%2Fquery')

    expect(await screen.findByRole('alert')).toHaveTextContent('Exchange code is invalid or expired.')
    expect(screen.getByRole('link', { name: 'Return to login' })).toHaveAttribute('href', '/login/')
  })

  it('shows provider errors returned by the backend OAuth redirect', async () => {
    renderCallback('/auth/callback?error=access_denied')

    expect(await screen.findByRole('alert')).toHaveTextContent('access_denied')
  })
})
