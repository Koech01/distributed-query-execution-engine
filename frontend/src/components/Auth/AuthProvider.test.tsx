import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'

import { AuthProvider } from '@/components/Auth/AuthProvider'
import { useAuth } from '@/hooks/use-auth'
import { clearSession } from '@/lib/auth'
import { clearMockAuthCookie, setMockAuthSession } from '@/test/mocks/auth-cookie'

function AuthStateProbe() {
  const { isAuthenticated, isLoading, user } = useAuth()

  return (
    <div>
      <p>{isLoading ? 'loading' : 'ready'}</p>
      <p>{isAuthenticated ? 'authenticated' : 'anonymous'}</p>
      <p>{user?.email ?? 'no-user'}</p>
    </div>
  )
}

describe('AuthProvider', () => {
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

  it('shows a loading state while restoring a cookie-backed session', () => {
    render(
      <AuthProvider>
        <AuthStateProbe />
      </AuthProvider>,
    )

    expect(screen.getByText('loading')).toBeInTheDocument()
  })

  it('restores an authenticated session from the HttpOnly cookie via /auth/account', async () => {
    setMockAuthSession(['query:read'])

    render(
      <AuthProvider>
        <AuthStateProbe />
      </AuthProvider>,
    )

    await waitFor(() => {
      expect(screen.getByText('ready')).toBeInTheDocument()
    })
    expect(screen.getByText('authenticated')).toBeInTheDocument()
    expect(screen.getByText('reader@example.com')).toBeInTheDocument()
  })

  it('remains anonymous when no auth cookie is present', async () => {
    render(
      <MemoryRouter>
        <AuthProvider>
          <AuthStateProbe />
        </AuthProvider>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText('ready')).toBeInTheDocument()
    })
    expect(screen.getByText('anonymous')).toBeInTheDocument()
    expect(screen.getByText('no-user')).toBeInTheDocument()
  })
})
