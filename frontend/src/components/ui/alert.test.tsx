import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'

describe('Alert', () => {
  it('uses readable foreground text for warning alerts in dark mode', () => {
    render(
      <Alert variant="warning">
        <AlertTitle>Health attention needed</AlertTitle>
        <AlertDescription>One or more health endpoints are unavailable.</AlertDescription>
      </Alert>,
    )

    const alert = screen.getByRole('alert')
    expect(alert.className).toContain('dark:text-foreground')
    expect(alert.className).toContain('text-warning-foreground')
  })
})
