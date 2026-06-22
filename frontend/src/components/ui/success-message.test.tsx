import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'

import { SUCCESS_MESSAGE_AUTO_HIDE_MS, SuccessMessage } from './success-message'

describe('SuccessMessage', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('renders an accessible success status with standard success styling', () => {
    render(<SuccessMessage id="save-status">Profile updated successfully.</SuccessMessage>)

    const message = screen.getByText('Profile updated successfully.')
    const status = message.closest('[role="status"]')
    expect(status).toHaveAttribute('id', 'save-status')
    expect(status).toHaveAttribute('aria-live', 'polite')
    expect(status).toHaveClass('text-success-foreground')
    expect(status).toHaveClass('border-success/40')
  })

  it('calls onDismiss after the default auto-hide delay', () => {
    const onDismiss = vi.fn()
    render(<SuccessMessage onDismiss={onDismiss}>Profile updated successfully.</SuccessMessage>)

    expect(onDismiss).not.toHaveBeenCalled()
    vi.advanceTimersByTime(SUCCESS_MESSAGE_AUTO_HIDE_MS)
    expect(onDismiss).toHaveBeenCalledTimes(1)
  })

  it('resets the auto-hide timer when the message changes', () => {
    const onDismiss = vi.fn()
    const { rerender } = render(<SuccessMessage onDismiss={onDismiss}>First message.</SuccessMessage>)

    vi.advanceTimersByTime(3000)
    rerender(<SuccessMessage onDismiss={onDismiss}>Second message.</SuccessMessage>)
    vi.advanceTimersByTime(3000)

    expect(onDismiss).not.toHaveBeenCalled()

    vi.advanceTimersByTime(2000)
    expect(onDismiss).toHaveBeenCalledTimes(1)
  })
})
