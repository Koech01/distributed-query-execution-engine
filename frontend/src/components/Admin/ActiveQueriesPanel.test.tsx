import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { ActiveQueriesPanel } from '@/components/Admin/ActiveQueriesPanel'
import { Toaster } from '@/components/ui/sonner'
import { mockActiveQueryId } from '@/test/mocks/handlers'

describe('ActiveQueriesPanel', () => {
  it('lists active queries and supports cancel confirmation', async () => {
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <ActiveQueriesPanel />
        <Toaster />
      </MemoryRouter>,
    )

    expect(await screen.findByRole('table', { name: /active queries/i })).toHaveTextContent(mockActiveQueryId)

    await user.click(screen.getByRole('button', { name: /^cancel$/i }))

    expect(screen.getByRole('dialog')).toHaveTextContent(/cancel active query/i)
    await user.click(screen.getByRole('button', { name: /confirm cancel/i }))

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
  })
})
