import { HttpResponse, http } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { AuthProvider } from '@/components/Auth/AuthProvider'
import { mockAccountProfile, resetMockAccountProfile } from '@/test/mocks/handlers'
import { server } from '@/test/mocks/server'
import { clearSession, getSessionUser } from '@/lib/auth'
import { clearMockAuthCookie, setMockAuthSession } from '@/test/mocks/auth-cookie'
import { requestHasAuthCookie } from '@/lib/auth-cookie'

import { AccountSettingsSection } from './AccountSettingsSection'

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


function renderAccountSection() {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={['/settings']}>
        <Routes>
          <Route path="/settings" element={<AccountSettingsSection />} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

describe('AccountSettingsSection', () => {
  beforeEach(() => {
    vi.stubGlobal('ResizeObserver', ResizeObserverMock)
    Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
      configurable: true,
      value: 480,
    })
    resetMockAccountProfile()
  })

  afterEach(() => {
    clearSession()
    clearMockAuthCookie()
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
    resetMockAccountProfile()
  })

  it('shows an empty state when auth is disabled', async () => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'false')

    renderAccountSection()

    expect(await screen.findByText('Sign in to manage your account')).toBeInTheDocument()
  })

  it('loads and displays the authenticated profile', async () => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    setMockAuthSession(['query:read'])

    renderAccountSection()

    expect(await screen.findByText(mockAccountProfile.userId)).toBeInTheDocument()
    expect(screen.getByLabelText('Display name')).toHaveValue('Reader User')
    expect(screen.getByLabelText('Email address')).toHaveValue('reader@example.com')
    expect(screen.getByRole('button', { name: 'Change password' })).toBeInTheDocument()
  })

  it('updates the display name through the profile form', async () => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    setMockAuthSession(['query:read'])

    const user = userEvent.setup()
    renderAccountSection()

    const displayNameInput = await screen.findByLabelText('Display name')
    await user.clear(displayNameInput)
    await user.type(displayNameInput, 'Updated Reader')
    await user.click(screen.getByRole('button', { name: 'Save profile changes' }))

    expect(await screen.findByText('Profile updated successfully.')).toBeInTheDocument()
    expect(screen.getByText('Profile updated successfully.').closest('[role="status"]')).toHaveClass('text-success-foreground')
    await waitFor(() => expect(screen.getByLabelText('Display name')).toHaveValue('Updated Reader'))
  })

  it('hides password management for OAuth-only accounts', async () => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    setMockAuthSession(['query:read'])

    server.use(
      http.get('*/auth/account', ({ request }) => {
        if (!requestHasAuthCookie(request)) {
          return HttpResponse.json({ error: 'unauthorized', message: 'Authentication required.' }, { status: 401 })
        }

        return HttpResponse.json({
          ...mockAccountProfile,
          hasPasswordLogin: false,
          linkedProviders: ['google'],
        })
      }),
    )

    renderAccountSection()

    expect(await screen.findByText('google')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Change password' })).not.toBeInTheDocument()
  })

  it('changes the password and refreshes the HttpOnly session cookie', async () => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    setMockAuthSession(['query:read'])

    const user = userEvent.setup()
    renderAccountSection()

    await screen.findByLabelText('Current password')
    await user.type(screen.getByLabelText('Current password'), 'correct-horse-battery-staple')
    await user.type(screen.getByLabelText('New password'), 'another-secure-password')
    await user.type(screen.getByLabelText('Confirm new password'), 'another-secure-password')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(await screen.findByText(/Password changed successfully/i)).toBeInTheDocument()
    expect(screen.getByText(/Password changed successfully/i).closest('[role="status"]')).toHaveClass('text-success-foreground')
    await waitFor(() =>
      expect(getSessionUser()).toMatchObject({
        email: mockAccountProfile.email,
        scopes: ['query:read'],
      }),
    )
  })

  it('deletes the account after email confirmation and signs out', async () => {
    vi.stubEnv('VITE_AUTH_ENABLED', 'true')
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    setMockAuthSession(['query:read'])

    const user = userEvent.setup()
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/settings']}>
          <Routes>
            <Route path="/settings" element={<AccountSettingsSection />} />
            <Route path="/login/" element={<div>Login page</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    )

    await screen.findByRole('button', { name: 'Delete account' })
    await user.click(screen.getByRole('button', { name: 'Delete account' }))
    await user.type(screen.getByLabelText('Confirm email address'), mockAccountProfile.email)
    await user.click(screen.getByRole('button', { name: 'Confirm delete' }))

    expect(await screen.findByText('Login page')).toBeInTheDocument()
    expect(getSessionUser()).toBeNull()
  })
})
