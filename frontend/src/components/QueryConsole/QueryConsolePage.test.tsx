import { HttpResponse, http } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { AuthProvider } from '@/components/Auth/AuthProvider'
import { QueryConsolePage } from '@/components/QueryConsole/QueryConsolePage'
import * as scroll from '@/lib/scroll'
import { mockQueryId, mockQueryResult } from '@/test/mocks/handlers'
import { server } from '@/test/mocks/server'

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

describe('QueryConsolePage', () => {
  beforeEach(() => {
    vi.spyOn(scroll, 'scrollElementIntoView').mockImplementation(() => undefined)
    vi.stubGlobal('ResizeObserver', ResizeObserverMock)
    Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
      configurable: true,
      value: 480,
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
  })

  it('shows client-side validation when SQL is empty', async () => {
    const user = userEvent.setup()
    renderQueryConsole()

    await user.click(screen.getByRole('button', { name: 'Clear' }))
    await user.click(screen.getByRole('button', { name: 'Execute' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('SQL is required.')
    expect(screen.queryByRole('columnheader', { name: /id/i })).not.toBeInTheDocument()
  })

  it('shows parameter name validation errors', async () => {
    const user = userEvent.setup()
    renderQueryConsole()

    await user.click(screen.getByRole('button', { name: 'Add parameter' }))

    const nameInput = screen.getByLabelText('Name')
    await user.clear(nameInput)
    await user.type(nameInput, '@1invalid')
    await user.click(screen.getByRole('button', { name: 'Execute' }))

    expect(await screen.findByText(/Parameter names must start with @/i)).toBeInTheDocument()
  })

  it('renders query results in an accessible table', async () => {
    const user = userEvent.setup()
    renderQueryConsole()

    await user.click(screen.getByRole('button', { name: 'Execute' }))

    expect(await screen.findByRole('columnheader', { name: 'id' })).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'name' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sort by id' })).toBeInTheDocument()
    expect(screen.getByText('Ada')).toBeInTheDocument()
    expect(screen.getByText('Grace')).toBeInTheDocument()
    expect(screen.getByText(mockQueryId)).toBeInTheDocument()
  })

  it('shows a degradation banner for 206 partial content responses', async () => {
    server.use(
      http.post('*/queries', () =>
        HttpResponse.json(
          {
            ...mockQueryResult,
            successfulShards: 3,
            failedShards: [2],
            degraded: true,
            degradationReason: 'Shard 2 timed out.',
          },
          { status: 206 },
        ),
      ),
    )

    const user = userEvent.setup()
    renderQueryConsole()

    await user.click(screen.getByRole('button', { name: 'Execute' }))

    const banner = await screen.findByRole('alert')
    expect(banner).toHaveTextContent('Partial results returned')
    expect(banner).toHaveTextContent('Failed shard indices: 2.')
    expect(banner).toHaveTextContent('Shard 2 timed out.')

    const resultsSection = screen.getByLabelText('Query results table')
    expect(within(resultsSection).getByText('Ada')).toBeInTheDocument()
  })

  it('shows sync execution errors beside the action bar', async () => {
    server.use(
      http.post('*/queries', () =>
        HttpResponse.json(
          {
            error: 'invalid_json',
            message: 'The request body was not valid JSON.',
          },
          { status: 400 },
        ),
      ),
    )

    const user = userEvent.setup()
    renderQueryConsole()

    await user.click(screen.getByRole('button', { name: 'Execute' }))

    const feedback = await screen.findByLabelText('Query execution feedback')
    const alert = within(feedback).getByRole('alert')

    expect(alert).toHaveTextContent('Query execution failed')
    expect(alert).toHaveTextContent('The request body was not valid JSON.')
    expect(feedback).toContainElement(screen.getByRole('button', { name: 'Execute' }))
  })

  it('shows streaming failures once without a duplicate execution alert', async () => {
    server.use(
      http.post('*/queries/stream', () =>
        HttpResponse.json(
          {
            error: 'invalid_json',
            message: 'The request body was not valid JSON.',
          },
          { status: 400 },
        ),
      ),
    )

    const user = userEvent.setup()
    renderQueryConsole()

    await user.click(screen.getByLabelText('Stream results'))
    await user.click(screen.getByRole('button', { name: 'Execute' }))

    const feedback = await screen.findByLabelText('Query execution feedback')
    const alerts = within(feedback).getAllByRole('alert')

    expect(alerts).toHaveLength(1)
    expect(alerts[0]).toHaveTextContent('Streaming failed')
    expect(alerts[0]).toHaveTextContent('The request body was not valid JSON.')
    expect(screen.queryByText('Query execution failed')).not.toBeInTheDocument()
  })

  it('submits async queries, polls status, and renders the fetched result', async () => {
    let submittedAsync: boolean | undefined
    server.use(
      http.post('*/queries', async ({ request }) => {
        const body = (await request.json()) as { async?: boolean }
        submittedAsync = body.async

        return HttpResponse.json(
          {
            queryId: mockQueryId,
            statusUrl: `/queries/${mockQueryId}/status`,
          },
          { status: 202 },
        )
      }),
      http.get('*/queries/:queryId/status', ({ params }) =>
        HttpResponse.json({
          queryId: params.queryId,
          status: 'completed',
          message: 'Result is ready.',
        }),
      ),
      http.get('*/queries/:queryId/result', () => HttpResponse.json(mockQueryResult)),
    )

    const user = userEvent.setup()
    renderQueryConsole()

    await user.click(screen.getByLabelText('Run asynchronously'))
    await user.click(screen.getByRole('button', { name: 'Execute' }))

    expect(await screen.findByRole('status', { name: 'Async query status' })).toHaveTextContent('Async query completed')
    expect(await screen.findByRole('columnheader', { name: 'id' })).toBeInTheDocument()
    expect(screen.getByText('Ada')).toBeInTheDocument()
    expect(submittedAsync).toBe(true)
  })

  it('scrolls to the results section after a successful sync execution', async () => {
    const user = userEvent.setup()
    renderQueryConsole()

    await user.click(screen.getByRole('button', { name: 'Execute' }))

    expect(await screen.findByRole('columnheader', { name: 'id' })).toBeInTheDocument()

    await vi.waitFor(() => {
      expect(scroll.scrollElementIntoView).toHaveBeenCalled()
    })

    const resultsSection = screen.getByRole('heading', { name: 'Results' }).closest('section')
    expect(scroll.scrollElementIntoView).toHaveBeenCalledWith(resultsSection, { focus: true })
  })

  it('scrolls to execution feedback when validation fails', async () => {
    const user = userEvent.setup()
    renderQueryConsole()

    await user.click(screen.getByRole('button', { name: 'Clear' }))
    await user.click(screen.getByRole('button', { name: 'Execute' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('SQL is required.')

    await vi.waitFor(() => {
      expect(scroll.scrollElementIntoView).toHaveBeenCalled()
    })

    expect(scroll.scrollElementIntoView).toHaveBeenCalledWith(
      screen.getByLabelText('Query execution feedback'),
      { focus: true },
    )
  })
})
