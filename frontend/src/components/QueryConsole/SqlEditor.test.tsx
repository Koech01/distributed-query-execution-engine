import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'

import { ThemeProvider } from '@/components/ThemeProvider'
import { SqlEditor } from '@/components/QueryConsole/SqlEditor'

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

function renderSqlEditor(props: Partial<React.ComponentProps<typeof SqlEditor>> = {}) {
  const onChange = props.onChange ?? (() => undefined)

  return render(
    <ThemeProvider>
      <SqlEditor value="SELECT * FROM Orders" onChange={onChange} {...props} />
    </ThemeProvider>,
  )
}

describe('SqlEditor', () => {
  beforeAll(() => {
    vi.stubGlobal('ResizeObserver', ResizeObserverMock)
    Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
      configurable: true,
      value: 480,
    })
  })

  afterAll(() => {
    vi.unstubAllGlobals()
  })

  it('associates the editor with the SQL query label', () => {
    renderSqlEditor()

    expect(screen.getByRole('textbox', { name: 'SQL query' })).toBeInTheDocument()
    expect(screen.getByText('SELECT query')).toBeInTheDocument()
  })

  it('shows the character count and validation error messaging', () => {
    renderSqlEditor({ value: '', error: 'SQL is required.' })

    expect(screen.getByText(/0 \/ 10,000 characters/i)).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('SQL is required.')
  })

  it('shows blocked keyword warnings for non-select statements', () => {
    renderSqlEditor({ value: 'DELETE FROM Orders' })

    expect(screen.getByText(/Review statement type/i)).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('"DELETE" is not permitted in this query engine.')
  })

  it('respects aria-labelledby when provided', () => {
    render(
      <ThemeProvider>
        <h2 id="sql-heading">SQL query</h2>
        <SqlEditor labelledBy="sql-heading" value="SELECT 1" onChange={() => undefined} />
      </ThemeProvider>,
    )

    expect(screen.getByRole('textbox', { name: 'SQL query' })).toHaveAttribute('aria-labelledby', 'sql-heading')
  })
})
