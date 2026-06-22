import { ShieldAlert } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { AuthPageLayout } from '@/components/ui/auth-page-layout'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/hooks/use-auth'
import { authApi } from '@/lib/api'
import { sanitizeReturnTo } from '@/lib/auth'
import { getErrorMessage } from '@/lib/errors'

function getInitialErrorMessage(searchParams: URLSearchParams): string | null {
  const providerError = searchParams.get('error_description') ?? searchParams.get('error')
  if (providerError) {
    return providerError
  }

  if (!searchParams.get('exchangeCode')) {
    return 'The sign-in callback is missing required parameters. Start again from the login page.'
  }

  return null
}

export function AuthCallbackPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { completeSignIn, isAuthEnabled } = useAuth()
  const [exchangeError, setExchangeError] = useState<string | null>(null)

  const exchangeCode = searchParams.get('exchangeCode')
  const returnToParam = searchParams.get('returnTo')
  const initialError = getInitialErrorMessage(searchParams)
  const errorMessage = initialError ?? exchangeError

  useEffect(() => {
    if (!isAuthEnabled) {
      navigate('/query', { replace: true })
      return
    }

    if (initialError || !exchangeCode) {
      return
    }

    let cancelled = false

    void authApi
      .exchangeToken({ exchangeCode })
      .then(async () => {
        if (cancelled) {
          return
        }

        await completeSignIn()
        navigate(sanitizeReturnTo(returnToParam ?? '/query'), { replace: true })
      })
      .catch((error: unknown) => {
        if (cancelled) {
          return
        }

        setExchangeError(getErrorMessage(error))
      })

    return () => {
      cancelled = true
    }
  }, [completeSignIn, exchangeCode, initialError, isAuthEnabled, navigate, returnToParam])

  if (errorMessage) {
    return (
      <AuthPageLayout>
        <Card className="border-border/60 shadow-lg">
          <CardHeader className="space-y-3">
            <CardTitle className="text-2xl tracking-tight">Sign-in failed</CardTitle>
            <CardDescription className="text-base leading-relaxed">
              We could not complete authentication. Review the message below and try again.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <Alert variant="destructive" aria-live="polite">
              <ShieldAlert aria-hidden="true" />
              <AlertTitle>Authentication error</AlertTitle>
              <AlertDescription>{errorMessage}</AlertDescription>
            </Alert>
            <Button asChild className="w-full">
              <Link to="/login/">Return to login</Link>
            </Button>
          </CardContent>
        </Card>
      </AuthPageLayout>
    )
  }

  return (
    <AuthPageLayout>
      <Card className="border-border/60 shadow-lg" aria-busy="true" aria-live="polite">
        <CardHeader className="space-y-3">
          <CardTitle className="text-2xl tracking-tight">Completing sign-in</CardTitle>
          <CardDescription className="text-base leading-relaxed">
            Exchanging your secure sign-in code and establishing your session.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-5/6" />
          <Skeleton className="h-4 w-2/3" />
        </CardContent>
      </Card>
    </AuthPageLayout>
  )
}
