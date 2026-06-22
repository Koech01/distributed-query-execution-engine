import { HttpResponse, http } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { CacheManagementPage } from '@/components/Admin/CacheManagementPage'
import { Toaster } from '@/components/ui/sonner'
import { mockPlanHash } from '@/test/mocks/handlers'
import { server } from '@/test/mocks/server'

function renderCacheManagementPage() {
  return render(
    <MemoryRouter>
      <CacheManagementPage />
      <Toaster />
    </MemoryRouter>,
  )
}

describe('CacheManagementPage', () => {
  it('renders cache stats and flushes all plans after confirmation', async () => {
    const user = userEvent.setup()

    renderCacheManagementPage()

    expect(await screen.findByRole('heading', { level: 2, name: /cache utilization/i })).toBeInTheDocument()
    expect(screen.getByText('18')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /flush all plans/i }))
    await user.click(screen.getByRole('button', { name: /confirm flush all/i }))

    await waitFor(() => {
      expect(screen.getByText(/plan cache flushed/i)).toBeInTheDocument()
    })
  })

  it('validates plan hash input before flush-by-hash submission', async () => {
    const user = userEvent.setup()

    renderCacheManagementPage()

    await screen.findByRole('heading', { level: 2, name: /cache utilization/i })

    const input = screen.getByLabelText(/plan hash/i)
    await user.type(input, 'invalid-hash')
    await user.click(screen.getByRole('button', { name: /flush plan hash/i }))

    expect(await screen.findByText(/64-character hexadecimal/i)).toBeInTheDocument()
  })

  it('flushes a valid plan hash and shows success feedback', async () => {
    const user = userEvent.setup()

    renderCacheManagementPage()

    await screen.findByRole('heading', { level: 2, name: /cache utilization/i })

    await user.type(screen.getByLabelText(/plan hash/i), mockPlanHash)
    await user.click(screen.getByRole('button', { name: /flush plan hash/i }))

    await waitFor(() => {
      expect(screen.getByText(/plan hash flushed/i)).toBeInTheDocument()
    })
  })

  it('shows an error toast when flush-by-hash fails on the server', async () => {
    server.use(
      http.post('*/admin/cache/flush', () =>
        HttpResponse.json(
          {
            type: 'invalid_request',
            message: 'Plan hash must be a 64-character hexadecimal SHA256 value.',
          },
          { status: 400 },
        ),
      ),
    )

    const user = userEvent.setup()

    renderCacheManagementPage()

    await screen.findByRole('heading', { level: 2, name: /cache utilization/i })
    await user.type(screen.getByLabelText(/plan hash/i), mockPlanHash)
    await user.click(screen.getByRole('button', { name: /flush plan hash/i }))

    await waitFor(() => {
      expect(screen.getByText(/flush failed/i)).toBeInTheDocument()
    })
  })
})
