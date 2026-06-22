import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'

import { RequireAdmin } from '@/components/Admin/RequireAdmin'
import { AuthProvider } from '@/components/Auth/AuthProvider'
import { clearSession } from '@/lib/auth'
import { clearMockAuthCookie, setMockAuthSession } from '@/test/mocks/auth-cookie'

function renderRequireAdmin(initialPath = '/admin', authEnabled = 'true') {
  vi.stubEnv('VITE_AUTH_ENABLED', authEnabled)

  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route path="/query" element={<main>Query console</main>} />
          <Route path="/admin" element={<RequireAdmin />}>
            <Route index element={<main>Admin content</main>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

describe('RequireAdmin', () => {
  beforeEach(() => {
    clearSession()
    clearMockAuthCookie()
  })

  afterEach(() => {
    clearSession()
    clearMockAuthCookie()
    vi.unstubAllEnvs()
  })

  it('allows admin routes when auth is disabled for development', async () => {
    renderRequireAdmin('/admin', 'false')

    expect(await screen.findByText('Admin content')).toBeInTheDocument()
  })

  it('redirects non-admin users to the query console', async () => {
    setMockAuthSession(['query:read'])
    renderRequireAdmin('/admin', 'true')

    expect(await screen.findByText('Query console')).toBeInTheDocument()
    expect(screen.queryByText('Admin content')).not.toBeInTheDocument()
  })

  it('allows admin routes when the session includes query:admin', async () => {
    setMockAuthSession(['query:read', 'query:admin'])
    renderRequireAdmin('/admin', 'true')

    expect(await screen.findByText('Admin content')).toBeInTheDocument()
  })
})
