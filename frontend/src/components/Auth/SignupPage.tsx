import { UserPlus } from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'

import { AuthPageLayout } from '@/components/ui/auth-page-layout'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/hooks/use-auth'
import { sanitizeReturnTo } from '@/lib/auth'
import { getErrorMessage } from '@/lib/errors'
import { registerRequestSchema } from '@/lib/schemas'

type FieldName = 'displayName' | 'email' | 'password' | 'confirmPassword'

export function SignupPage() {
  const navigate = useNavigate()
  const formId = useId()
  const displayNameFieldId = `${formId}-display-name`
  const emailFieldId = `${formId}-email`
  const passwordFieldId = `${formId}-password`
  const confirmPasswordFieldId = `${formId}-confirm-password`
  const passwordHintId = `${formId}-password-hint`
  const displayNameErrorId = `${formId}-display-name-error`
  const emailErrorId = `${formId}-email-error`
  const passwordErrorId = `${formId}-password-error`
  const confirmPasswordErrorId = `${formId}-confirm-password-error`

  const { isAuthenticated, isAuthEnabled, registerWithEmail } = useAuth()
  const [searchParams] = useSearchParams()
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldName, string>>>({})
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const returnTo = sanitizeReturnTo(searchParams.get('returnTo') ?? '/query')
  const loginHref = returnTo === '/query' ? '/login/' : `/login/?returnTo=${encodeURIComponent(returnTo)}`

  if (isAuthEnabled && isAuthenticated) {
    return <Navigate to={returnTo} replace />
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setErrorMessage(null)
    setFieldErrors({})

    const nextErrors: Partial<Record<FieldName, string>> = {}
    const validation = registerRequestSchema.safeParse({ email, password, displayName })

    if (!validation.success) {
      for (const issue of validation.error.issues) {
        const field = issue.path[0]
        if (field === 'displayName' || field === 'email' || field === 'password') {
          nextErrors[field] = issue.message
        }
      }
    }

    if (confirmPassword.length === 0) {
      nextErrors.confirmPassword = 'Confirm your password.'
    } else if (password !== confirmPassword) {
      nextErrors.confirmPassword = 'Passwords do not match.'
    }

    if (Object.keys(nextErrors).length > 0) {
      setFieldErrors(nextErrors)
      return
    }

    setIsSubmitting(true)

    try {
      await registerWithEmail(validation.data!.email, validation.data!.password, validation.data!.displayName)
      navigate(returnTo, { replace: true })
    } catch (error) {
      setIsSubmitting(false)
      setErrorMessage(getErrorMessage(error))
    }
  }

  return (
    <AuthPageLayout showBrandPanel={false}>
      <Card className="border-border/60 shadow-lg">
        <CardHeader className="space-y-3">
          <CardTitle className="text-2xl tracking-tight">Create account</CardTitle>
          <CardDescription className="text-base leading-relaxed">
            Register with email to run queries, inspect results, and manage your workspace preferences.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          {!isAuthEnabled ? (
            <p
              className="rounded-lg border border-amber-500/30 bg-amber-500/5 px-3 py-2 text-sm text-foreground"
              role="status"
            >
              Authentication bypass is active. You can still register against the backend API here. Set{' '}
              <code className="rounded bg-muted px-1 py-0.5 text-xs">VITE_AUTH_ENABLED=true</code> in{' '}
              <code className="rounded bg-muted px-1 py-0.5 text-xs">.env</code> and restart the dev server to
              enforce sign-in on protected routes.
            </p>
          ) : null}

          {errorMessage ? (
            <p
              className="rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive"
              role="alert"
              aria-live="polite"
            >
              {errorMessage}
            </p>
          ) : null}

          <form className="space-y-4" onSubmit={(event) => void handleSubmit(event)} noValidate>
            <div className="space-y-2">
              <Label htmlFor={displayNameFieldId}>Display name</Label>
              <Input
                id={displayNameFieldId}
                type="text"
                autoComplete="name"
                value={displayName}
                onChange={(event) => setDisplayName(event.target.value)}
                aria-required="true"
                aria-invalid={fieldErrors.displayName ? 'true' : undefined}
                aria-describedby={fieldErrors.displayName ? displayNameErrorId : undefined}
                disabled={isSubmitting}
                required
              />
              {fieldErrors.displayName ? (
                <p id={displayNameErrorId} className="text-sm text-destructive" role="alert" aria-live="polite">
                  {fieldErrors.displayName}
                </p>
              ) : null}
            </div>

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
                aria-invalid={fieldErrors.email ? 'true' : undefined}
                aria-describedby={fieldErrors.email ? emailErrorId : undefined}
                disabled={isSubmitting}
                required
              />
              {fieldErrors.email ? (
                <p id={emailErrorId} className="text-sm text-destructive" role="alert" aria-live="polite">
                  {fieldErrors.email}
                </p>
              ) : null}
            </div>

            <div className="space-y-2">
              <Label htmlFor={passwordFieldId}>Password</Label>
              <Input
                id={passwordFieldId}
                type="password"
                autoComplete="new-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                aria-required="true"
                aria-invalid={fieldErrors.password ? 'true' : undefined}
                aria-describedby={fieldErrors.password ? passwordErrorId : passwordHintId}
                disabled={isSubmitting}
                required
              />
              {fieldErrors.password ? (
                <p id={passwordErrorId} className="text-sm text-destructive" role="alert" aria-live="polite">
                  {fieldErrors.password}
                </p>
              ) : (
                <p id={passwordHintId} className="text-sm text-muted-foreground">
                  Use at least 12 characters. Longer passphrases are recommended.
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor={confirmPasswordFieldId}>Confirm password</Label>
              <Input
                id={confirmPasswordFieldId}
                type="password"
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                aria-required="true"
                aria-invalid={fieldErrors.confirmPassword ? 'true' : undefined}
                aria-describedby={fieldErrors.confirmPassword ? confirmPasswordErrorId : undefined}
                disabled={isSubmitting}
                required
              />
              {fieldErrors.confirmPassword ? (
                <p id={confirmPasswordErrorId} className="text-sm text-destructive" role="alert" aria-live="polite">
                  {fieldErrors.confirmPassword}
                </p>
              ) : null}
            </div>

            <Button
              type="submit"
              className="h-11 w-full text-base"
              aria-busy={isSubmitting}
              disabled={isSubmitting}
            >
              <UserPlus className="size-4" aria-hidden="true" />
              {isSubmitting ? 'Creating account…' : 'Create account'}
            </Button>
          </form>
        </CardContent>
        <CardFooter className="justify-center border-t border-border/60 pt-6">
          <p className="text-sm text-muted-foreground">
            Already have an account?{' '}
            <Link
              to={loginHref}
              className="font-medium text-foreground underline-offset-4 transition-colors hover:text-primary hover:underline"
            >
              Sign in
            </Link>
          </p>
        </CardFooter>
      </Card>
    </AuthPageLayout>
  )
}
