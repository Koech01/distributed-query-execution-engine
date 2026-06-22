import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { NotFoundPage } from './index'

describe('NotFoundPage', () => {
  it('renders an accessible empty state with recovery actions', () => {
    render(
      <MemoryRouter>
        <NotFoundPage standalone />
      </MemoryRouter>,
    )

    expect(screen.getByRole('main', { name: /page not found/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: /page not found/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /query console/i })).toHaveAttribute('href', '/query')
    expect(screen.getByRole('button', { name: /go back/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /view system health/i })).toHaveAttribute('href', '/operations')
  })

  it('uses a section landmark when rendered inside the application shell', () => {
    render(
      <MemoryRouter>
        <NotFoundPage />
      </MemoryRouter>,
    )

    expect(screen.getByRole('region', { name: /page not found/i })).toBeInTheDocument()
    expect(screen.queryByRole('main', { name: /page not found/i })).not.toBeInTheDocument()
  })

  it('lets keyboard users activate the query console recovery action', async () => {
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <NotFoundPage standalone />
      </MemoryRouter>,
    )

    await user.tab()

    const queryConsoleLink = screen.getByRole('link', { name: /query console/i })
    expect(queryConsoleLink).toHaveFocus()

    await user.keyboard('{Enter}')

    expect(queryConsoleLink).toHaveAttribute('href', '/query')
  })
})
