import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { render, screen, within } from '@testing-library/react'

import { AppSidebar } from '@/components/ui/app-sidebar'
import { SidebarProvider } from '@/components/ui/sidebar'

function renderSidebar(showAdmin: boolean, initialPath = '/query') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <SidebarProvider>
        <AppSidebar showAdmin={showAdmin} />
      </SidebarProvider>
    </MemoryRouter>,
  )
}

describe('AppSidebar', () => {
  it('renders standard navigation items for all authenticated users', () => {
    renderSidebar(false)

    const primaryNavigation = screen.getByRole('navigation', { name: /primary navigation/i })

    expect(within(primaryNavigation).getByRole('link', { name: /^query$/i })).toHaveAttribute('href', '/query')
    expect(within(primaryNavigation).getByRole('link', { name: /^history$/i })).toHaveAttribute('href', '/history')
    expect(within(primaryNavigation).getByRole('link', { name: /^operations$/i })).toHaveAttribute('href', '/operations')
    expect(within(primaryNavigation).getByRole('link', { name: /^settings$/i })).toHaveAttribute('href', '/settings')
  })

  it('includes the admin navigation item only when showAdmin is true', () => {
    renderSidebar(false)
    expect(screen.queryByRole('link', { name: /^admin$/i })).not.toBeInTheDocument()

    renderSidebar(true)
    const primaryNavigation = screen.getAllByRole('navigation', { name: /primary navigation/i }).at(-1)!
    expect(within(primaryNavigation).getByRole('link', { name: /^admin$/i })).toHaveAttribute('href', '/admin')
  })

  it('marks the active route for assistive technologies', () => {
    renderSidebar(false, '/operations')

    const primaryNavigation = screen.getByRole('navigation', { name: /primary navigation/i })
    expect(within(primaryNavigation).getByRole('link', { name: /^operations$/i })).toHaveAttribute('aria-current', 'page')
  })
})
