import 'fake-indexeddb/auto'

import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'

import { toast } from 'sonner'

import { AppRoutes } from '@/AppRoutes'
import { AuthProvider } from '@/components/Auth/AuthProvider'
import { ThemeProvider } from '@/components/ThemeProvider'
import { Toaster } from '@/components/ui/sonner'
import { clearSession } from '@/lib/auth'
import { clearMockAuthCookie, setMockAuthSession } from '@/test/mocks/auth-cookie'

class ResizeObserverMock {
  observe() {
    return undefined
  }

  unobserve() {
    return undefined
  }

  disconnect() {
    return undefined
  }
}

function renderApp(initialPath: string, authEnabled = 'false') {
  vi.stubEnv('VITE_AUTH_ENABLED', authEnabled)

  return render(
    <ThemeProvider>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialPath]}>
          <AppRoutes />
          <Toaster />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>,
  )
}

describe('AppRoutes', () => {
  beforeEach(() => {
    vi.stubGlobal('ResizeObserver', ResizeObserverMock)
    Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
      configurable: true,
      value: 480,
    })
    clearSession()
    clearMockAuthCookie()
  })

  afterEach(() => {
    clearSession()
    clearMockAuthCookie()
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
  })

  it('redirects the root path to the query console', async () => {
    renderApp('/')

    expect(await screen.findByRole('heading', { level: 1, name: /query console/i })).toBeInTheDocument()
  })

  it.each([
    ['/query', /query console/i],
    ['/history', /query history/i],
    ['/settings', /settings/i],
    ['/unauthorized', /access denied/i],
  ])('renders %s', async (path, headingPattern) => {
    renderApp(path)

    expect(await screen.findByRole('heading', { level: 1, name: headingPattern })).toBeInTheDocument()
  })

  it('renders the signup route when auth is enabled', async () => {
    renderApp('/signup/', 'true')

    expect(await screen.findByRole('heading', { name: /create account/i })).toBeInTheDocument()
  })

  it('renders the admin dashboard after overview data loads', async () => {
    renderApp('/admin')

    expect(await screen.findByRole('heading', { level: 2, name: /cluster overview/i })).toBeInTheDocument()
  })

  it('renders cache management after cache stats load', async () => {
    renderApp('/admin/cache')

    expect(await screen.findByRole('heading', { level: 2, name: /cache utilization/i })).toBeInTheDocument()
  })

  it('renders the operations route after health checks load', async () => {
    renderApp('/operations')

    expect(await screen.findByRole('heading', { name: /health status/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: /operations/i })).toBeInTheDocument()
  })

  it('renders a shell 404 page for unknown authenticated routes', async () => {
    renderApp('/missing-route')

    expect(await screen.findByRole('region', { name: /page not found/i })).toBeInTheDocument()
    expect(screen.getByRole('complementary', { name: /application sidebar/i })).toBeInTheDocument()
  })

  it('hides the admin navigation item without query:admin scope when auth is enabled', async () => {
    setMockAuthSession(['query:read'])
    renderApp('/query', 'true')

    const primaryNavigation = await screen.findByRole('navigation', { name: /primary navigation/i })
    await waitFor(() => {
      expect(within(primaryNavigation).queryByRole('link', { name: /^admin$/i })).not.toBeInTheDocument()
    })
  })

  it('shows the admin navigation item for query:admin scope when auth is enabled', async () => {
    setMockAuthSession(['query:read', 'query:admin'])
    renderApp('/query', 'true')

    const primaryNavigation = await screen.findByRole('navigation', { name: /primary navigation/i })
    expect(within(primaryNavigation).getByRole('link', { name: /^admin$/i })).toHaveAttribute('href', '/admin')
  })

  it('redirects non-admin users away from admin routes when auth is enabled', async () => {
    setMockAuthSession(['query:read'])
    renderApp('/admin', 'true')

    expect(await screen.findByRole('heading', { level: 1, name: /query console/i })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { level: 1, name: /administration/i })).not.toBeInTheDocument()
  })

  it('renders accessible toast notifications from the global toaster', async () => {
    renderApp('/query')

    toast('Route wiring verified')

    expect(await screen.findByText('Route wiring verified')).toBeInTheDocument()
  })
})
