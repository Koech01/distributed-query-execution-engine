import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'

import { QueryResultTable } from './QueryResultTable'
import { mockQueryResult } from '@/test/mocks/handlers'

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

describe('QueryResultTable', () => {
  it('renders sortable column headers and row values', () => {
    vi.stubGlobal('ResizeObserver', ResizeObserverMock)

    render(<QueryResultTable result={mockQueryResult} />)

    expect(screen.getByRole('columnheader', { name: 'id' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sort by id' })).toBeInTheDocument()
    expect(screen.getByText('Ada')).toBeInTheDocument()
  })

  it('enables virtualized scrolling for large result sets', () => {
    vi.stubGlobal('ResizeObserver', ResizeObserverMock)

    const largeResult = {
      ...mockQueryResult,
      rowCount: 150,
      rows: Array.from({ length: 150 }, (_, index) => [String(index + 1), `value-${index + 1}`]),
    }

    render(<QueryResultTable result={largeResult} />)

    expect(screen.getByText(/Virtualized scrolling is enabled for performance/i)).toBeInTheDocument()
  })
})
