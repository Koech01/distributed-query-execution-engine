import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { createAmbiguousQueryResultError } from '@/hooks/use-query-poll'
import { AsyncStatusBanner } from './AsyncStatusBanner'

const queryId = '3fa85f64-5717-4562-b3fc-2c963f66afa6'

describe('AsyncStatusBanner', () => {
  it('renders running status with query metadata and cancel action', async () => {
    const user = userEvent.setup()
    const onCancel = vi.fn()

    render(
      <AsyncStatusBanner
        queryId={queryId}
        phase="running"
        status="running"
        elapsedMs={1_250}
        onCancel={onCancel}
      />,
    )

    const status = screen.getByRole('status', { name: 'Async query status' })
    expect(status).toHaveTextContent('Query is running asynchronously')
    expect(status).toHaveTextContent('Backend status: running')
    expect(status).toHaveTextContent(queryId)

    await user.click(screen.getByRole('button', { name: 'Cancel polling' }))
    expect(onCancel).toHaveBeenCalledTimes(1)
  })

  it('renders paused state without relying on color alone', () => {
    render(<AsyncStatusBanner queryId={queryId} phase="paused" status="running" elapsedMs={2_000} />)

    const status = screen.getByRole('status', { name: 'Async query status' })
    expect(status).toHaveTextContent('Paused')
    expect(status).toHaveTextContent('Polling paused')
    expect(status).toHaveTextContent('This tab is hidden')
  })

  it('renders completed state as a terminal success without cancel action', () => {
    render(<AsyncStatusBanner queryId={queryId} phase="completed" status="completed" elapsedMs={3_000} />)

    const status = screen.getByRole('status', { name: 'Async query status' })
    expect(status).toHaveTextContent('Completed')
    expect(status).toHaveTextContent('terminal result has been loaded')
    expect(screen.queryByRole('button', { name: 'Cancel polling' })).not.toBeInTheDocument()
  })

  it('renders ambiguous result 404 as an alert with recovery context', () => {
    render(
      <AsyncStatusBanner
        queryId={queryId}
        phase="error"
        status="completed"
        elapsedMs={4_000}
        error={createAmbiguousQueryResultError()}
      />,
    )

    const alert = screen.getByRole('alert', { name: 'Async query status' })
    expect(alert).toHaveTextContent('Needs attention')
    expect(alert).toHaveTextContent('still be processing')
    expect(alert).toHaveTextContent('expired')
    expect(alert).toHaveTextContent('unknown query')
  })
})
