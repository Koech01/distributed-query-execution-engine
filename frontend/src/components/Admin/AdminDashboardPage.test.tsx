import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'

import { AdminDashboardPage } from '@/components/Admin/AdminDashboardPage'
import { mockActiveQueryId, mockPlanHash } from '@/test/mocks/handlers'

describe('AdminDashboardPage', () => {
  it('renders cluster overview stats, active queries, and worker health', async () => {
    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    expect(await screen.findByRole('heading', { level: 2, name: /cluster overview/i })).toBeInTheDocument()
    expect(await screen.findByText(/1 active query/i)).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: /active queries/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: /worker health/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /open cache management/i })).toHaveAttribute('href', '/admin/cache')
    expect(screen.getByRole('table', { name: /active queries/i })).toHaveTextContent(mockActiveQueryId)
    expect(screen.getByRole('table', { name: /worker health/i })).toHaveTextContent('worker-1')
    expect(screen.getByTitle(mockPlanHash)).toBeInTheDocument()
  })
})
