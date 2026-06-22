import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { AppSidebar } from '@/components/ui/app-sidebar'
import { SidebarProvider, SidebarTrigger } from '@/components/ui/sidebar'

function mockViewport(isMobile: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: isMobile,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  })
}

function getSidebarElement(): HTMLElement {
  const sidebar = document.getElementById('app-sidebar')
  if (!sidebar) {
    throw new Error('Expected #app-sidebar to be rendered.')
  }

  return sidebar
}

function renderShell(isMobile: boolean) {
  mockViewport(isMobile)

  return render(
    <MemoryRouter initialEntries={['/query']}>
      <SidebarProvider>
        <AppSidebar />
        <SidebarTrigger />
      </SidebarProvider>
    </MemoryRouter>,
  )
}

describe('Sidebar shell', () => {
  beforeEach(() => {
    document.body.style.overflow = ''
  })

  afterEach(() => {
    vi.restoreAllMocks()
    document.body.style.overflow = ''
  })

  it('collapses desktop navigation to icon-only labels when toggled', async () => {
    renderShell(false)

    const sidebar = getSidebarElement()
    expect(sidebar).toHaveAttribute('data-state', 'expanded')

    const primaryNavigation = screen.getByRole('navigation', { name: /primary navigation/i })
    expect(within(primaryNavigation).getByRole('link', { name: /^query$/i })).toBeVisible()

    await userEvent.click(screen.getByRole('button', { name: /toggle sidebar/i }))

    expect(sidebar).toHaveAttribute('data-state', 'collapsed')
    expect(screen.getByRole('button', { name: /toggle sidebar/i })).toHaveAttribute('aria-expanded', 'false')
    expect(within(primaryNavigation).getByRole('link', { name: /^query$/i })).toBeInTheDocument()
    expect(within(primaryNavigation).getByRole('link', { name: /^query$/i }).querySelector('span:not(.sr-only)')).toBeNull()
    expect(screen.queryByText(/distributed query engine/i)).not.toBeInTheDocument()
  })

  it('expands desktop navigation labels when toggled again', async () => {
    renderShell(false)

    const toggle = screen.getByRole('button', { name: /toggle sidebar/i })
    await userEvent.click(toggle)
    await userEvent.click(toggle)

    const primaryNavigation = screen.getByRole('navigation', { name: /primary navigation/i })
    expect(within(primaryNavigation).getByText(/^query$/i)).toBeVisible()
    expect(screen.getByText(/distributed query engine/i)).toBeVisible()
  })

  it('opens and closes the mobile drawer from the menu button', async () => {
    renderShell(true)

    const toggle = screen.getByRole('button', { name: /toggle sidebar/i })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')

    await userEvent.click(toggle)

    await waitFor(() => {
      expect(getSidebarElement()).toHaveAttribute('data-state', 'open')
    })
    expect(toggle).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('button', { name: /close navigation menu/i })).toBeInTheDocument()

    await userEvent.click(toggle)
    await waitFor(() => {
      expect(getSidebarElement()).toHaveAttribute('data-state', 'closed')
    })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')
  })

  it('closes the mobile drawer when tapping the backdrop', async () => {
    renderShell(true)

    await userEvent.click(screen.getByRole('button', { name: /toggle sidebar/i }))
    await waitFor(() => {
      expect(getSidebarElement()).toHaveAttribute('data-state', 'open')
    })

    await userEvent.click(screen.getByRole('button', { name: /close navigation menu/i }))

    await waitFor(() => {
      expect(getSidebarElement()).toHaveAttribute('data-state', 'closed')
    })
    expect(screen.getByRole('button', { name: /toggle sidebar/i })).toHaveAttribute('aria-expanded', 'false')
  })

  it('closes the mobile drawer after selecting a navigation item', async () => {
    renderShell(true)

    await userEvent.click(screen.getByRole('button', { name: /toggle sidebar/i }))
    await waitFor(() => {
      expect(getSidebarElement()).toHaveAttribute('data-state', 'open')
    })

    const primaryNavigation = screen.getByRole('navigation', { name: /primary navigation/i })
    await userEvent.click(within(primaryNavigation).getByRole('link', { name: /^history$/i }))

    await waitFor(() => {
      expect(getSidebarElement()).toHaveAttribute('data-state', 'closed')
    })
    expect(screen.getByRole('button', { name: /toggle sidebar/i })).toHaveAttribute('aria-expanded', 'false')
  })

  it('closes the mobile drawer when Escape is pressed', async () => {
    renderShell(true)

    await userEvent.click(screen.getByRole('button', { name: /toggle sidebar/i }))
    await waitFor(() => {
      expect(getSidebarElement()).toHaveAttribute('data-state', 'open')
    })

    await userEvent.keyboard('{Escape}')

    await waitFor(() => {
      expect(getSidebarElement()).toHaveAttribute('data-state', 'closed')
    })
  })
})
