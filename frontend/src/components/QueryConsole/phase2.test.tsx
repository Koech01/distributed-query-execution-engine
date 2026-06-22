import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { StreamingStatusBanner } from '@/components/QueryConsole/StreamingStatusBanner'

describe('StreamingStatusBanner', () => {
  it('renders nothing in the idle phase', () => {
    const { container } = render(<StreamingStatusBanner phase="idle" rowCount={0} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('shows streaming progress and a cancel action', () => {
    render(
      <StreamingStatusBanner
        phase="streaming"
        queryId="3fa85f64-5717-4562-b3fc-2c963f66afa6"
        streamMode="incremental"
        rowCount={12}
        totalShards={4}
        onCancel={() => undefined}
      />,
    )

    expect(screen.getByRole('status', { name: 'Streaming query status' })).toHaveTextContent('Streaming results')
    expect(screen.getByText('Incremental merge')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancel stream' })).toBeInTheDocument()
  })

  it('shows a cancelled message with rows received', () => {
    render(<StreamingStatusBanner phase="cancelled" rowCount={3} />)

    expect(screen.getByRole('alert')).toHaveTextContent('Stream cancelled')
    expect(screen.getByText(/3 row\(s\) were received/i)).toBeInTheDocument()
  })
})

describe('QueryPlanPanel', () => {
  it('renders plan metadata and sub-queries', async () => {
    const { QueryPlanPanel } = await import('@/components/QueryConsole/QueryPlanPanel')
    const { mockQueryPlan } = await import('@/test/mocks/handlers')

    render(
      <QueryPlanPanel
        plan={mockQueryPlan}
        isLoading={false}
        error={null}
        onInspect={() => undefined}
        onDismiss={() => undefined}
      />,
    )

    expect(screen.getByText('Broadcast')).toBeInTheDocument()
    expect(screen.getByRole('table', { name: 'Query plan sub-queries' })).toBeInTheDocument()
    expect(screen.getByText(/SELECT \* FROM Orders WHERE ShardId = 0/)).toBeInTheDocument()
  })

  it('calls inspect plan when the button is clicked', async () => {
    const user = userEvent.setup()
    const onInspect = vi.fn()
    const { QueryPlanPanel } = await import('@/components/QueryConsole/QueryPlanPanel')

    render(
      <QueryPlanPanel plan={null} isLoading={false} error={null} onInspect={onInspect} onDismiss={() => undefined} />,
    )

    await user.click(screen.getByRole('button', { name: 'Inspect plan' }))
    expect(onInspect).toHaveBeenCalledTimes(1)
  })
})
