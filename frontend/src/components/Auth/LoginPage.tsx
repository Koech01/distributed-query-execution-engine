import { LogIn } from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'

import { OAuthProviderButton } from '@/components/Auth/OAuthProviderButton'
import type { BackendOAuthProvider } from '@/components/types'
import { AuthPageLayout } from '@/components/ui/auth-page-layout'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/hooks/use-auth'
import { isBackendOAuthProviderEnabled, sanitizeReturnTo } from '@/lib/auth'
import { getErrorMessage } from '@/lib/errors'
import { loginRequestSchema } from '@/lib/schemas'

type ActiveSignIn = 'email' | BackendOAuthProvider

export function LoginPage() {
  const navigate = useNavigate()
  const formId = useId()
  const emailFieldId = `${formId}-email`
  const passwordFieldId = `${formId}-password`
  const emailErrorId = `${formId}-email-error`
  const passwordErrorId = `${formId}-password-error`

  const { isAuthenticated, isAuthEnabled, loginWithEmail, loginWithOAuth } = useAuth()
  const [searchParams] = useSearchParams()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [emailError, setEmailError] = useState<string | null>(null)
  const [passwordError, setPasswordError] = useState<string | null>(null)
  const [activeSignIn, setActiveSignIn] = useState<ActiveSignIn | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const returnTo = sanitizeReturnTo(searchParams.get('returnTo') ?? '/query')
  const callbackError = searchParams.get('error')
  const displayError = errorMessage ?? callbackError
  const signupHref = returnTo === '/query' ? '/signup/' : `/signup/?returnTo=${encodeURIComponent(returnTo)}`

  const showGoogle = isBackendOAuthProviderEnabled('google')
  const showGitHub = isBackendOAuthProviderEnabled('github')
  const showOAuthProviders = showGoogle || showGitHub
  const isBusy = activeSignIn !== null

  if (!isAuthEnabled) {
    return <Navigate to={returnTo} replace />
  }

  if (isAuthenticated) {
    return <Navigate to={returnTo} replace />
  }

  const handleEmailSignIn = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setErrorMessage(null)
    setEmailError(null)
    setPasswordError(null)

    const validation = loginRequestSchema.safeParse({ email, password })
    if (!validation.success) {
      for (const issue of validation.error.issues) {
        if (issue.path[0] === 'email') {
          setEmailError(issue.message)
        }
        if (issue.path[0] === 'password') {
          setPasswordError(issue.message)
        }
      }
      return
    }

    setActiveSignIn('email')

    try {
      await loginWithEmail(validation.data.email, validation.data.password)
      navigate(returnTo, { replace: true })
    } catch (error) {
      setActiveSignIn(null)
      setErrorMessage(getErrorMessage(error))
    }
  }

  const handleOAuthSignIn = (provider: BackendOAuthProvider) => {
    setErrorMessage(null)
    setActiveSignIn(provider)
    loginWithOAuth(provider, returnTo)
  }

  return (
    <AuthPageLayout showBrandPanel={false}>
      <Card className="border-border/60 shadow-lg">
        <CardHeader className="space-y-3">
          <CardTitle className="text-2xl tracking-tight">Sign in</CardTitle>
          <CardDescription className="text-base leading-relaxed">
            Access the Distributed Query Execution Engine with email and password or a connected provider.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          {displayError ? (
            <p
              className="rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive"
              role="alert"
              aria-live="polite"
            >
              {displayError}
            </p>
          ) : null}

          {showOAuthProviders ? (
            <div className="space-y-3" aria-label="Social sign-in options">
              {showGoogle ? (
                <OAuthProviderButton
                  provider="google"
                  label="Continue with Google"
                  isLoading={activeSignIn === 'google'}
                  disabled={isBusy && activeSignIn !== 'google'}
                  onClick={() => handleOAuthSignIn('google')}
                />
              ) : null}
              {showGitHub ? (
                <OAuthProviderButton
                  provider="github"
                  label="Continue with GitHub"
                  isLoading={activeSignIn === 'github'}
                  disabled={isBusy && activeSignIn !== 'github'}
                  onClick={() => handleOAuthSignIn('github')}
                />
              ) : null}
            </div>
          ) : null}

          {showOAuthProviders ? (
            <div className="relative py-1">
              <div className="absolute inset-0 flex items-center" aria-hidden="true">
                <span className="w-full border-t border-border/70" />
              </div>
              <div className="relative flex justify-center">
                <span className="bg-card px-3 text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
                  Or sign in with email
                </span>
              </div>
            </div>
          ) : null}

          <form className="space-y-4" onSubmit={(event) => void handleEmailSignIn(event)} noValidate>
            <div className="space-y-2">
              <Label htmlFor={emailFieldId}>Email address</Label>
              <Input
                id={emailFieldId}
                type="email"
                autoComplete="email"
                inputMode="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                aria-required="true"
                aria-invalid={emailError ? 'true' : undefined}
                aria-describedby={emailError ? emailErrorId : undefined}
                disabled={isBusy}
                required
              />
              {emailError ? (
                <p id={emailErrorId} className="text-sm text-destructive" role="alert" aria-live="polite">
                  {emailError}
                </p>
              ) : null}
            </div>

            <div className="space-y-2">
              <Label htmlFor={passwordFieldId}>Password</Label>
              <Input
                id={passwordFieldId}
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                aria-required="true"
                aria-invalid={passwordError ? 'true' : undefined}
                aria-describedby={passwordError ? passwordErrorId : undefined}
                disabled={isBusy}
                required
              />
              {passwordError ? (
                <p id={passwordErrorId} className="text-sm text-destructive" role="alert" aria-live="polite">
                  {passwordError}
                </p>
              ) : null}
            </div>

            <Button
              type="submit"
              className="h-11 w-full text-base"
              aria-busy={activeSignIn === 'email'}
              disabled={isBusy && activeSignIn !== 'email'}
            >
              <LogIn className="size-4" aria-hidden="true" />
              {activeSignIn === 'email' ? 'Signing in…' : 'Sign in with email'}
            </Button>
          </form>
        </CardContent>
        <CardFooter className="justify-center border-t border-border/60 pt-6">
          <p className="text-sm text-muted-foreground">
            Need an account?{' '}
            <Link
              to={signupHref}
              className="font-medium text-foreground underline-offset-4 transition-colors hover:text-primary hover:underline"
            >
              Create account
            </Link>
          </p>
        </CardFooter>
      </Card>
    </AuthPageLayout>
  )
}
