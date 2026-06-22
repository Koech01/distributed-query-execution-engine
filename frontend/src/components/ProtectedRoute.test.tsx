import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'

import { ProtectedRoute } from '@/components/ProtectedRoute'
import { AuthProvider } from '@/components/Auth/AuthProvider'
import { clearSession } from '@/lib/auth'
import { clearMockAuthCookie, setMockAuthSession } from '@/test/mocks/auth-cookie'

function renderProtectedRoute(initialPath = '/query', authEnabled = 'true') {
  vi.stubEnv('VITE_AUTH_ENABLED', authEnabled)

  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route path="/login/" element={<main>Login page</main>} />
          <Route
            path="/query"
            element={
              <ProtectedRoute>
                <main>Protected content</main>
              </ProtectedRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    clearSession()
    clearMockAuthCookie()
  })

  afterEach(() => {
    clearSession()
    clearMockAuthCookie()
    vi.unstubAllEnvs()
  })

  it('renders protected content when auth is disabled for development', async () => {
    renderProtectedRoute('/query', 'false')

    expect(await screen.findByText('Protected content')).toBeInTheDocument()
  })

  it('redirects unauthenticated users to login with a return URL', async () => {
    renderProtectedRoute('/query', 'true')

    expect(await screen.findByText('Login page')).toBeInTheDocument()
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument()
  })

  it('allows access when a valid HttpOnly session cookie is present', async () => {
    setMockAuthSession(['query:read'])
    renderProtectedRoute('/query', 'true')

    expect(await screen.findByText('Protected content')).toBeInTheDocument()
  })

  it('redirects to login when no session cookie is present', async () => {
    renderProtectedRoute('/query', 'true')

    expect(await screen.findByText('Login page')).toBeInTheDocument()
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument()
  })
})
