import { HttpResponse, http } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { AuthProvider } from '@/components/Auth/AuthProvider'
import { LoginPage } from '@/components/Auth/LoginPage'
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

describe('LoginPage', () => {
  beforeEach(() => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    vi.stubEnv('VITE_DEV_USE_PROXY', 'false')
    vi.stubEnv('VITE_OAUTH_GOOGLE_ENABLED', 'true')
    vi.stubEnv('VITE_OAUTH_GITHUB_ENABLED', 'true')
    vi.stubEnv('VITE_SEED_LOGIN_PREFILL_ENABLED', 'false')
    clearSession()
    clearMockAuthCookie()
  })

  afterEach(() => {
    clearSession()
    clearMockAuthCookie()
    vi.unstubAllEnvs()
  })

  it('renders Google, GitHub, and email sign-in options', () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/login/']}>
          <LoginPage />
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(screen.getByRole('button', { name: 'Continue with Google' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Continue with GitHub' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign in with email' })).toBeInTheDocument()
    expect(screen.getByLabelText('Email address')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Create account' })).toHaveAttribute('href', '/signup/')
  })

  it('redirects to the backend Google OAuth login endpoint', async () => {
    const assignMock = vi.fn()
    const originalLocation = window.location

    Object.defineProperty(window, 'location', {
      configurable: true,
      value: {
        ...originalLocation,
        assign: assignMock,
      },
    })

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/login/?returnTo=%2Fhistory']}>
          <LoginPage />
        </MemoryRouter>
      </AuthProvider>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Continue with Google' }))

    expect(assignMock).toHaveBeenCalledWith('http://localhost:5281/auth/google/login?returnTo=%2Fhistory')

    Object.defineProperty(window, 'location', {
      configurable: true,
      value: originalLocation,
    })
  })

  it('signs in with email and password and navigates to the return path', async () => {
    const accessToken = createTestJwt()

    server.use(
      http.post('*/auth/login', async ({ request }) => {
        const body = (await request.json()) as { email: string; password: string }
        expect(body.email).toBe('reader@example.com')
        expect(body.password).toBe('correct-horse-battery-staple')

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

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/login/?returnTo=%2Fhistory']}>
          <Routes>
            <Route path="/login/" element={<LoginPage />} />
            <Route path="/history" element={<main>History page</main>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    )

    await userEvent.type(screen.getByLabelText('Email address'), 'reader@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'correct-horse-battery-staple')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in with email' }))

    expect(await screen.findByText('History page')).toBeInTheDocument()
    expect(getSessionUser()).toMatchObject({
      email: 'reader@example.com',
      scopes: ['query:read'],
    })
  })

  it('shows API errors from email sign-in', async () => {
    server.use(
      http.get('*/auth/account', () =>
        HttpResponse.json(
          {
            error: 'unauthorized',
            message: 'Authentication required.',
          },
          { status: 401 },
        ),
      ),
      http.post('*/auth/login', () =>
        HttpResponse.json(
          {
            error: 'authentication_failed',
            message: 'Invalid email address or password.',
          },
          { status: 401 },
        ),
      ),
    )

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/login/']}>
          <Routes>
            <Route path="/login/" element={<LoginPage />} />
            <Route path="/query" element={<main>Query console</main>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Email address'), 'reader@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'wrong-password')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in with email' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email address or password.')
    await waitFor(() => {
      expect(getSessionUser()).toBeNull()
    })
  })

  it('prefills seeded admin credentials when seed login prefill is enabled', () => {
    vi.stubEnv('VITE_SEED_LOGIN_PREFILL_ENABLED', 'true')
    vi.stubEnv('VITE_SEED_LOGIN_ADMIN_EMAIL', 'admin@example.com')
    vi.stubEnv('VITE_SEED_LOGIN_ADMIN_PASSWORD', 'ChangeMe-Admin-12')

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/login/']}>
          <LoginPage />
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(screen.getByLabelText('Email address')).toHaveValue('admin@example.com')
    expect(screen.getByLabelText('Password')).toHaveValue('ChangeMe-Admin-12')
  })

  it('shows callback errors from the query string', () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/login/?error=access_denied']}>
          <LoginPage />
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('access_denied')
  })

  it('renders when auth bypass is enabled so login can be tested manually', async () => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'false')

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/login/']}>
          <Routes>
            <Route path="/login/" element={<LoginPage />} />
            <Route path="/query" element={<main>Query console</main>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(await screen.findByText('Query console')).toBeInTheDocument()
  })
})