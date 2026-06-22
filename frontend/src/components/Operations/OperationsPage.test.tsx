import { HttpResponse, http } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { server } from '@/test/mocks/server'

import { OperationsPage } from './OperationsPage'

function renderOperationsPage() {
  return render(<OperationsPage />)
}

describe('OperationsPage', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllEnvs()
  })

  it('shows live and ready health status from the API client', async () => {
    renderOperationsPage()

    expect(await screen.findByRole('heading', { name: 'Health status' })).toBeInTheDocument()
    expect(screen.getByText('API live')).toBeInTheDocument()
    expect(screen.getByText('API ready')).toBeInTheDocument()
    expect(screen.getByText('Backend status: live')).toBeInTheDocument()
    expect(screen.getByText('Backend status: ready')).toBeInTheDocument()
    expect(screen.getAllByText('Available')).toHaveLength(2)
    expect(screen.getByText('Auto-refreshes every 30 seconds while this page is open.')).toBeInTheDocument()
  })

  it('shows 503 health responses as unavailable with non-color-only warning text', async () => {
    server.use(
      http.get('*/health/ready', () =>
        HttpResponse.json(
          { error: 'service_unavailable', message: 'Readiness probe failed.' },
          { status: 503 },
        ),
      ),
    )

    renderOperationsPage()

    expect(await screen.findByText('Health attention needed')).toBeInTheDocument()
    expect(screen.getByText('Unavailable')).toBeInTheDocument()
    expect(screen.getByText('Readiness probe failed.')).toBeInTheDocument()
  })

  it('renders observability links only when environment URLs are configured', async () => {
    vi.stubEnv('VITE_GRAFANA_URL', 'https://grafana.example.test/d/dqee')
    vi.stubEnv('VITE_JAEGER_URL', 'https://jaeger.example.test/search')

    renderOperationsPage()

    expect(await screen.findByRole('heading', { name: 'Observability links' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /open grafana/i })).toHaveAttribute(
      'href',
      'https://grafana.example.test/d/dqee',
    )
    expect(screen.getByRole('link', { name: /open jaeger/i })).toHaveAttribute(
      'href',
      'https://jaeger.example.test/search',
    )
  })

  it('shows a designed empty state when observability URLs are not configured', async () => {
    renderOperationsPage()

    expect(await screen.findByText('No external tools configured')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /open grafana/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /open jaeger/i })).not.toBeInTheDocument()
  })

  it('refreshes health checks manually and every 30 seconds', async () => {
    const user = userEvent.setup()
    let liveChecks = 0
    let intervalCallback: TimerHandler | undefined
    const realSetInterval = window.setInterval.bind(window)

    vi.spyOn(window, 'setInterval').mockImplementation((handler: TimerHandler, timeout?: number, ...args: unknown[]) => {
      if (timeout === REFRESH_DELAY_FOR_TEST) {
        intervalCallback = handler
        return 1
      }

      return realSetInterval(handler, timeout, ...args)
    })

    server.use(
      http.get('*/health/live', () => {
        liveChecks += 1
        return HttpResponse.json({ status: `live-${liveChecks}` })
      }),
    )

    renderOperationsPage()

    expect(await screen.findByText('Backend status: live-1')).toBeInTheDocument()

    await act(async () => {
      if (typeof intervalCallback === 'function') {
        intervalCallback()
      }
    })

    await waitFor(() => expect(screen.getByText('Backend status: live-2')).toBeInTheDocument())

    await user.click(screen.getByRole('button', { name: /refresh now/i }))
    await waitFor(() => expect(screen.getByText('Backend status: live-3')).toBeInTheDocument())
  })
})

const REFRESH_DELAY_FOR_TEST = 30_000
