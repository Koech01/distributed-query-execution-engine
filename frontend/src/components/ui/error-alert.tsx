import { AlertCircle } from 'lucide-react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { AppError, getErrorMessage } from '@/lib/errors'

interface ErrorAlertProps {
  error: unknown
  title?: string
}

export function ErrorAlert({ error, title = 'Request failed' }: ErrorAlertProps) {
  const message = getErrorMessage(error)
  const details = error instanceof AppError ? error.details : []

  return (
    <Alert variant="destructive">
      <AlertCircle className="size-4" aria-hidden="true" />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>
        <p>{message}</p>
        {details.length > 0 ? (
          <ul className="mt-2 list-disc space-y-1 pl-5" aria-label="Error details">
            {details.map((detail) => (
              <li key={detail}>{detail}</li>
            ))}
          </ul>
        ) : null}
      </AlertDescription>
    </Alert>
  )
}
