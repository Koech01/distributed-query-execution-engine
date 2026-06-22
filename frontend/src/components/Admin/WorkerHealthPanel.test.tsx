import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'

import { WorkerHealthPanel } from '@/components/Admin/WorkerHealthPanel'

describe('WorkerHealthPanel', () => {
  it('renders worker probe statuses with accessible text labels', async () => {
    render(<WorkerHealthPanel />)

    expect(await screen.findByRole('table', { name: /worker health/i })).toHaveTextContent('worker-1')
    expect(screen.getByText(/Live: Healthy/i)).toBeInTheDocument()
    expect(screen.getAllByText(/Ready: Healthy/i).length).toBeGreaterThan(0)
    expect(screen.getByText(/gRPC: Healthy/i)).toBeInTheDocument()
    expect(screen.getByText(/Live: Unhealthy/i)).toBeInTheDocument()
    expect(screen.getByText(/gRPC: Unreachable/i)).toBeInTheDocument()
    expect(screen.getByText(/Not registered/i)).toBeInTheDocument()
    expect(screen.getByRole('table', { name: /worker health/i })).toHaveTextContent('worker-2')
  })
})
