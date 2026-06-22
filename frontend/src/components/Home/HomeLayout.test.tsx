import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { HomeLayout } from './page'
import { ThemeProvider } from '@/components/ThemeProvider'
import { AuthProvider } from '@/components/Auth/AuthProvider'

function renderShell(initialPath = '/query') {
  vi.stubEnv('VITE_AUTH_ENABLED', 'false')

  return render(
    <ThemeProvider>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route element={<HomeLayout showAdmin />}>
            <Route path="query" element={<main><h1>Query Console</h1></main>} />
            <Route path="history" element={<main><h1>Query History</h1></main>} />
            <Route path="operations" element={<main><h1>Operations</h1></main>} />
            <Route path="settings" element={<main><h1>Settings</h1></main>} />
            <Route path="admin" element={<main><h1>Admin</h1></main>} />
          </Route>
        </Routes>
      </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>,
  )
}

describe('HomeLayout', () => {
  it('renders sidebar navigation, header breadcrumbs, and outlet content', () => {
    renderShell('/operations')
    const primaryNavigation = screen.getByRole('navigation', { name: /primary navigation/i })

    expect(screen.getByRole('complementary', { name: /application sidebar/i })).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: /breadcrumb/i })).toBeInTheDocument()
    expect(within(primaryNavigation).getByRole('link', { name: /operations/i })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('heading', { level: 1, name: /operations/i })).toBeInTheDocument()
  })

  it('keeps sidebar navigation and shell actions reachable from the keyboard', async () => {
    const user = userEvent.setup()
    renderShell('/query')

    await user.tab()
    const skipLink = screen.getByRole('link', { name: /skip to main content/i })
    expect(skipLink).toHaveFocus()

    await user.tab()
    expect(within(screen.getByRole('navigation', { name: /primary navigation/i })).getByRole('link', { name: /query/i })).toHaveFocus()

    await user.keyboard('{Enter}')
    expect(screen.getByRole('heading', { level: 1, name: /query console/i })).toBeInTheDocument()

    skipLink.focus()
    await user.keyboard('{Enter}')
    expect(screen.getByRole('main', { name: /application content/i })).toHaveFocus()

    await user.tab()
    expect(screen.getByRole('button', { name: /toggle sidebar/i })).toHaveFocus()

    await user.tab()
    expect(screen.getByRole('link', { name: /home/i })).toHaveFocus()

    await user.tab()
    expect(screen.getByRole('button', { name: /change theme/i })).toHaveFocus()

    await user.tab()
    expect(screen.getByRole('button', { name: /open user menu/i })).toHaveFocus()
  })

  it('provides a logout action without calling backend auth endpoints', async () => {
    const user = userEvent.setup()
    const fetchSpy = vi.spyOn(window, 'fetch')
    const logoutListener = vi.fn()
    window.addEventListener('dqee:logout', logoutListener)

    renderShell('/query')

    await user.click(screen.getByRole('button', { name: /open user menu/i }))
    await user.click(screen.getByRole('menuitem', { name: /log out/i }))

    expect(logoutListener).toHaveBeenCalledTimes(1)
    expect(fetchSpy).not.toHaveBeenCalled()

    window.removeEventListener('dqee:logout', logoutListener)
    fetchSpy.mockRestore()
  })
})
