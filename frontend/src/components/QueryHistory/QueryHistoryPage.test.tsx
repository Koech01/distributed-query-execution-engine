import 'fake-indexeddb/auto'

import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { addLocalQueryHistoryEntry } from '@/hooks/use-local-query-history'

import { QueryHistoryPage } from './QueryHistoryPage'

async function deleteHistoryDb(): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const request = indexedDB.deleteDatabase('dqee-query-history')
    request.onsuccess = () => resolve()
    request.onerror = () => reject(request.error ?? new Error('Could not delete test database.'))
    request.onblocked = () => resolve()
  })
}

function LocationStateProbe() {
  const location = useLocation()
  const state = location.state as { sql?: string } | null

  return <p>Prefilled SQL: {state?.sql ?? 'none'}</p>
}

function renderHistoryPage() {
  return render(
    <MemoryRouter initialEntries={['/history']}>
      <Routes>
        <Route path="/history" element={<QueryHistoryPage />} />
        <Route path="/query" element={<LocationStateProbe />} />
        <Route path="/query/:queryId" element={<p>Result route</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('QueryHistoryPage', () => {
  afterEach(async () => {
    await deleteHistoryDb()
  })

  it('renders an empty state when no history exists', async () => {
    renderHistoryPage()

    expect(await screen.findByText('No query history yet')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /open query console/i })).toHaveAttribute('href', '/query')
  })

  it('renders history rows and navigates to a result by query id', async () => {
    await addLocalQueryHistoryEntry({
      result: {
        queryId: '22222222-2222-4222-8222-222222222222',
        rowCount: 5,
        executionMs: 42,
        degraded: true,
      },
      sql: 'SELECT * FROM Customers WHERE Region = @region',
      async: true,
      timestamp: new Date('2026-06-15T12:00:00.000Z'),
    })

    renderHistoryPage()

    expect(await screen.findByRole('table', { name: 'Local query history' })).toBeInTheDocument()
    expect(screen.getByText('22222222-2222-4222-8222-222222222222')).toBeInTheDocument()
    expect(screen.getByText('Degraded')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /re-run/i })).toBeDisabled()
    expect(screen.getByRole('link', { name: /view result/i })).toHaveAttribute(
      'href',
      '/query/22222222-2222-4222-8222-222222222222',
    )
  })

  it('re-runs entries that explicitly stored SQL and clears history with confirmation', async () => {
    const user = userEvent.setup()

    await addLocalQueryHistoryEntry({
      result: {
        queryId: '33333333-3333-4333-8333-333333333333',
        rowCount: 1,
        executionMs: 21,
        degraded: false,
      },
      sql: 'SELECT TOP 1 * FROM Orders',
      async: false,
      saveSql: true,
      timestamp: new Date('2026-06-15T13:00:00.000Z'),
    })

    const { unmount } = renderHistoryPage()

    await user.click(await screen.findByRole('button', { name: /re-run/i }))
    expect(screen.getByText('Prefilled SQL: SELECT TOP 1 * FROM Orders')).toBeInTheDocument()

    unmount()
    renderHistoryPage()
    await user.click(await screen.findByRole('button', { name: /clear history/i }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: /^clear history$/i }))

    await waitFor(() => expect(screen.getByText('No query history yet')).toBeInTheDocument())
  })
})
