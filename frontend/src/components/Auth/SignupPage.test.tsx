import { HttpResponse, http } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { AuthProvider } from '@/components/Auth/AuthProvider'
import { SignupPage } from '@/components/Auth/SignupPage'
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

describe('SignupPage', () => {
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

  it('renders the registration form with accessible fields', () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/signup/']}>
          <SignupPage />
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(screen.getByRole('heading', { name: 'Create account' })).toBeInTheDocument()
    expect(screen.getByLabelText('Display name')).toBeInTheDocument()
    expect(screen.getByLabelText('Email address')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
    expect(screen.getByLabelText('Confirm password')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create account' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/login/')
  })

  it('shows client-side validation errors for invalid input', async () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/signup/']}>
          <SignupPage />
        </MemoryRouter>
      </AuthProvider>,
    )

    await userEvent.type(screen.getByLabelText('Display name'), 'A')
    await userEvent.type(screen.getByLabelText('Email address'), 'not-an-email')
    await userEvent.type(screen.getByLabelText('Password'), 'short')
    await userEvent.type(screen.getByLabelText('Confirm password'), 'different')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('Display name must be at least 2 characters.')).toBeInTheDocument()
    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(screen.getByText('Password must be at least 12 characters.')).toBeInTheDocument()
    expect(screen.getByText('Passwords do not match.')).toBeInTheDocument()
  })

  it('registers a new account and navigates to the return path', async () => {
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
        <MemoryRouter initialEntries={['/signup/?returnTo=%2Fhistory']}>
          <Routes>
            <Route path="/signup/" element={<SignupPage />} />
            <Route path="/history" element={<main>History page</main>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    )

    await userEvent.type(screen.getByLabelText('Display name'), 'New User')
    await userEvent.type(screen.getByLabelText('Email address'), 'new.user@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'correct-horse-battery-staple')
    await userEvent.type(screen.getByLabelText('Confirm password'), 'correct-horse-battery-staple')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('History page')).toBeInTheDocument()
    expect(getSessionUser()).toMatchObject({
      email: 'reader@example.com',
      scopes: ['query:read'],
    })
  })

  it('shows API errors from registration', async () => {
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
      http.post('*/auth/register', () =>
        HttpResponse.json(
          {
            error: 'authentication_failed',
            message: 'An account with this email address already exists.',
          },
          { status: 401 },
        ),
      ),
    )

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/signup/']}>
          <Routes>
            <Route path="/signup/" element={<SignupPage />} />
            <Route path="/query" element={<main>Query console</main>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(await screen.findByRole('heading', { name: 'Create account' })).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Display name'), 'Existing User')
    await userEvent.type(screen.getByLabelText('Email address'), 'existing@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'correct-horse-battery-staple')
    await userEvent.type(screen.getByLabelText('Confirm password'), 'correct-horse-battery-staple')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('An account with this email address already exists.')
    await waitFor(() => {
      expect(getSessionUser()).toBeNull()
    })
  })

  it('preserves returnTo in the sign-in link', () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/signup/?returnTo=%2Fsettings']}>
          <SignupPage />
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute(
      'href',
      '/login/?returnTo=%2Fsettings',
    )
  })

  it('renders when auth bypass is enabled so signup can be tested manually', () => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'false')

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/signup/']}>
          <SignupPage />
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(screen.getByRole('heading', { name: 'Create account' })).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent(/authentication bypass is active/i)
  })
})
