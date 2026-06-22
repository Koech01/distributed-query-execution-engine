import 'fake-indexeddb/auto'

import { HttpResponse, http } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { AuthProvider } from '@/components/Auth/AuthProvider'
import { QueryConsolePage } from '@/components/QueryConsole/QueryConsolePage'
import { mockQueryResult } from '@/test/mocks/handlers'
import { server } from '@/test/mocks/server'
import {
  USER_PREFERENCES_STORAGE_KEY,
  readStoredUserPreferences,
  writeStoredUserPreferences,
} from '@/hooks/use-preferences'
import { listLocalQueryHistory } from '@/hooks/use-local-query-history'

import { SettingsPage } from './SettingsPage'

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

async function deleteHistoryDb(): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const request = indexedDB.deleteDatabase('dqee-query-history')
    request.onsuccess = () => resolve()
    request.onerror = () => reject(request.error ?? new Error('Could not delete test database.'))
    request.onblocked = () => resolve()
  })
}

function renderSettingsPage() {
  vi.stubEnv('VITE_AUTH_ENABLED', 'false')

  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={['/settings']}>
        <Routes>
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

function renderQueryConsole() {
  vi.stubEnv('VITE_AUTH_ENABLED', 'false')

  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={['/query']}>
        <Routes>
          <Route path="/query" element={<QueryConsolePage />} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

describe('SettingsPage preferences', () => {
  beforeEach(() => {
    vi.stubGlobal('ResizeObserver', ResizeObserverMock)
    Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
      configurable: true,
      value: 480,
    })
  })

  afterEach(async () => {
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
    await deleteHistoryDb()
  })

  it('persists preference changes to localStorage', async () => {
    const user = userEvent.setup()
    renderSettingsPage()

    expect(await screen.findByRole('heading', { name: 'Query defaults' })).toBeInTheDocument()

    const timeoutInput = screen.getByLabelText('Default timeout (seconds)')
    await user.clear(timeoutInput)
    await user.type(timeoutInput, '45')

    await user.click(screen.getByLabelText('Default async execution'))
    await user.click(screen.getByLabelText('Save SQL in local history'))
    await user.click(screen.getByRole('button', { name: 'Save preferences' }))

    await waitFor(() =>
      expect(readStoredUserPreferences()).toMatchObject({
        defaultTimeoutSeconds: 45,
        defaultAsync: true,
        saveSqlInHistory: true,
      }),
    )

    expect(
      screen.getByText('Preferences saved. Query Console defaults will apply the next time you open it.').closest('[role="status"]'),
    ).toHaveClass('text-success-foreground')

    expect(window.localStorage.getItem(USER_PREFERENCES_STORAGE_KEY)).not.toContain('token')
    expect(window.localStorage.getItem(USER_PREFERENCES_STORAGE_KEY)).not.toContain('Bearer')
  })

  it('applies saved defaults on Query Console mount', async () => {
    writeStoredUserPreferences({
      defaultTimeoutSeconds: 75,
      defaultFailurePolicy: 'StrictAll',
      defaultAsync: true,
      saveSqlInHistory: false,
    })

    renderQueryConsole()

    expect(await screen.findByLabelText('Timeout (seconds)')).toHaveValue(75)
    expect(screen.getByLabelText('Run asynchronously')).toBeChecked()
    expect(screen.getByLabelText('Failure policy')).toHaveTextContent('Strict all')
  })

  it('uses save SQL preference when recording query history', async () => {
    writeStoredUserPreferences({
      defaultTimeoutSeconds: 30,
      defaultFailurePolicy: 'BestEffort',
      defaultAsync: false,
      saveSqlInHistory: true,
    })

    const user = userEvent.setup()
    renderQueryConsole()

    await waitFor(() => expect(screen.getByLabelText('Timeout (seconds)')).toHaveValue(30))
    await user.click(screen.getByRole('button', { name: 'Execute' }))
    expect(await screen.findByRole('columnheader', { name: 'id' })).toBeInTheDocument()

    const entries = await listLocalQueryHistory()
    expect(entries).toHaveLength(1)
    expect(entries[0]?.sql).toBe('SELECT TOP 10 * FROM Orders')
  })

  it('keeps history metadata-only when save SQL is disabled', async () => {
    writeStoredUserPreferences({
      defaultTimeoutSeconds: 30,
      defaultFailurePolicy: 'BestEffort',
      defaultAsync: false,
      saveSqlInHistory: false,
    })

    server.use(http.post('*/queries', () => HttpResponse.json(mockQueryResult)))

    const user = userEvent.setup()
    renderQueryConsole()

    await waitFor(() => expect(screen.getByLabelText('Timeout (seconds)')).toHaveValue(30))
    await user.click(screen.getByRole('button', { name: 'Execute' }))
    expect(await screen.findByRole('columnheader', { name: 'id' })).toBeInTheDocument()

    await waitFor(async () => {
      const entries = await listLocalQueryHistory()
      expect(entries).toHaveLength(1)
    })

    const entries = await listLocalQueryHistory()
    expect(entries[0]?.sql).toBeUndefined()
  })
})
