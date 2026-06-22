import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { OAuthProviderButton } from '@/components/Auth/OAuthProviderButton'

describe('OAuthProviderButton', () => {
  it('renders an accessible provider button with loading state', async () => {
    const onClick = vi.fn()

    render(
      <OAuthProviderButton provider="google" label="Continue with Google" onClick={onClick} />,
    )

    const button = screen.getByRole('button', { name: 'Continue with Google' })
    expect(button).toBeEnabled()

    await userEvent.click(button)
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('disables interaction while redirecting', () => {
    render(
      <OAuthProviderButton
        provider="github"
        label="Continue with GitHub"
        isLoading
        onClick={() => undefined}
      />,
    )

    const button = screen.getByRole('button', { name: 'Redirecting…' })
    expect(button).toHaveAttribute('aria-busy', 'true')
    expect(button).toBeDisabled()
  })
})
