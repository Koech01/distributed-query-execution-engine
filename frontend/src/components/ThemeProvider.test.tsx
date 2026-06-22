import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { ThemeProvider } from './ThemeProvider'
import { ThemeToggle } from '@/components/ui/theme-toggle'

describe('ThemeProvider', () => {
  it('supports light, dark, and system preferences with local persistence', async () => {
    const user = userEvent.setup()

    render(
      <ThemeProvider>
        <MemoryRouter>
          <ThemeToggle />
        </MemoryRouter>
      </ThemeProvider>,
    )

    await user.click(screen.getByRole('button', { name: /change theme/i }))
    await user.click(screen.getByRole('menuitem', { name: /dark/i }))

    await waitFor(() => expect(window.localStorage.getItem('theme')).toBe('dark'))
    expect(document.documentElement).toHaveClass('dark')

    await user.click(screen.getByRole('button', { name: /change theme/i }))
    await user.click(screen.getByRole('menuitem', { name: /light/i }))

    await waitFor(() => expect(window.localStorage.getItem('theme')).toBe('light'))
    expect(document.documentElement).toHaveClass('light')

    await user.click(screen.getByRole('button', { name: /change theme/i }))
    await user.click(screen.getByRole('menuitem', { name: /system/i }))

    await waitFor(() => expect(window.localStorage.getItem('theme')).toBe('system'))
  })
})
