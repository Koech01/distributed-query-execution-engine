import { Clock, BadgeCheck, KeyRound, LoaderCircle, ShieldCheck, Trash2, UserRound } from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'

import type { UserProfile } from '@/components/types'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { ErrorAlert } from '@/components/ui/error-alert'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { PageSection } from '@/components/ui/page-section'
import { Skeleton } from '@/components/ui/skeleton'
import { StatTile } from '@/components/ui/stat-tile'
import { SuccessMessage } from '@/components/ui/success-message'
import { useAccountProfile } from '@/hooks/use-account-profile'
import { formatTimestamp } from '@/lib/date'
import { getErrorMessage } from '@/lib/errors'
import { changePasswordRequestSchema, updateProfileRequestSchema } from '@/lib/schemas'

function AccountProfileSkeleton() {
  return (
    <div aria-busy="true" aria-label="Loading account profile" className="space-y-5">
      <div className="grid gap-4 md:grid-cols-2">
        <Skeleton className="h-28 rounded-xl" />
        <Skeleton className="h-28 rounded-xl" />
      </div>
      <Skeleton className="h-40 rounded-xl" />
    </div>
  )
}

function ProfileMetadata({ profile }: { profile: UserProfile }) {
  return (
    <div className="grid gap-4 md:grid-cols-2">
      <StatTile label="Account ID" icon={BadgeCheck} hint="Backend user identifier">
        <p className="break-all font-mono text-sm">{profile.userId}</p>
      </StatTile>
      <StatTile label="Sign-in methods" icon={KeyRound} hint="Linked OAuth providers and password login">
        <div className="flex flex-wrap gap-2">
          {profile.hasPasswordLogin ? <Badge variant="secondary">Email password</Badge> : null}
          {profile.linkedProviders.length > 0 ? (
            profile.linkedProviders.map((provider) => (
              <Badge key={provider} variant="outline">
                {provider}
              </Badge>
            ))
          ) : (
            <span className="text-sm text-muted-foreground">No linked OAuth providers</span>
          )}
        </div>
      </StatTile>
      <StatTile label="Scopes" icon={ShieldCheck} hint="JWT scopes issued for this account">
        <p className="text-sm leading-relaxed">{profile.scopes.join(', ') || 'None assigned'}</p>
      </StatTile>
      <StatTile label="Profile timestamps" icon={Clock} hint="Created and last updated in UTC">
        <div className="space-y-1 text-sm">
          <p>
            <span className="text-muted-foreground">Created:</span> {formatTimestamp(profile.createdAt)}
          </p>
          <p>
            <span className="text-muted-foreground">Updated:</span> {formatTimestamp(profile.updatedAt)}
          </p>
        </div>
      </StatTile>
    </div>
  )
}

function ProfileUpdateForm({
  profile,
  onSave,
  disabled,
}: {
  profile: UserProfile
  onSave: (request: { displayName?: string; email?: string }) => Promise<void>
  disabled?: boolean
}) {
  const formId = useId()
  const displayNameId = `${formId}-display-name`
  const emailId = `${formId}-email`
  const displayNameErrorId = `${formId}-display-name-error`
  const emailErrorId = `${formId}-email-error`
  const statusId = `${formId}-status`

  const [displayName, setDisplayName] = useState(profile.displayName ?? '')
  const [email, setEmail] = useState(profile.email)
  const [displayNameError, setDisplayNameError] = useState<string | null>(null)
  const [emailError, setEmailError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setDisplayNameError(null)
    setEmailError(null)
    setFormError(null)
    setStatusMessage(null)

    const unchanged =
      displayName.trim() === (profile.displayName ?? '').trim() && email.trim() === profile.email.trim()
    if (unchanged) {
      setFormError('Update at least one profile field before saving.')
      return
    }

    const payload: { displayName?: string; email?: string } = {}
    if (displayName.trim() !== (profile.displayName ?? '').trim()) {
      payload.displayName = displayName.trim()
    }
    if (email.trim() !== profile.email.trim()) {
      payload.email = email.trim()
    }

    const validation = updateProfileRequestSchema.safeParse(payload)
    if (!validation.success) {
      for (const issue of validation.error.issues) {
        if (issue.path[0] === 'displayName') {
          setDisplayNameError(issue.message)
        }
        if (issue.path[0] === 'email') {
          setEmailError(issue.message)
        }
        if (issue.path.length === 0) {
          setFormError(issue.message)
        }
      }
      return
    }

    setIsSaving(true)

    try {
      await onSave(validation.data)
      setDisplayName(validation.data.displayName ?? displayName.trim())
      if (validation.data.email) {
        setEmail(validation.data.email)
      }
      setStatusMessage('Profile updated successfully.')
    } catch (error) {
      setFormError(getErrorMessage(error))
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <form className="space-y-4" onSubmit={(event) => void handleSubmit(event)} noValidate>
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor={displayNameId}>Display name</Label>
          <Input
            id={displayNameId}
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
            autoComplete="name"
            aria-required="true"
            aria-invalid={displayNameError ? 'true' : undefined}
            aria-describedby={displayNameError ? displayNameErrorId : undefined}
            disabled={disabled || isSaving}
            required
          />
          {displayNameError ? (
            <p id={displayNameErrorId} className="text-sm text-destructive" role="alert" aria-live="polite">
              {displayNameError}
            </p>
          ) : null}
        </div>

        <div className="space-y-2">
          <Label htmlFor={emailId}>Email address</Label>
          <Input
            id={emailId}
            type="email"
            inputMode="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            autoComplete="email"
            aria-required="true"
            aria-invalid={emailError ? 'true' : undefined}
            aria-describedby={emailError ? emailErrorId : undefined}
            disabled={disabled || isSaving}
            required
          />
          {emailError ? (
            <p id={emailErrorId} className="text-sm text-destructive" role="alert" aria-live="polite">
              {emailError}
            </p>
          ) : null}
        </div>
      </div>

      {formError ? (
        <p className="text-sm text-destructive" role="alert" aria-live="polite">
          {formError}
        </p>
      ) : null}

      {statusMessage ? (
        <SuccessMessage id={statusId} onDismiss={() => setStatusMessage(null)}>
          {statusMessage}
        </SuccessMessage>
      ) : null}

      <Button type="submit" aria-busy={isSaving} disabled={disabled || isSaving}>
        {isSaving ? (
          <>
            <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
            Saving profile...
          </>
        ) : (
          'Save profile changes'
        )}
      </Button>
    </form>
  )
}

function PasswordChangeForm({
  onChangePassword,
  disabled,
}: {
  onChangePassword: (currentPassword: string, newPassword: string) => Promise<void>
  disabled?: boolean
}) {
  const formId = useId()
  const currentPasswordId = `${formId}-current-password`
  const newPasswordId = `${formId}-new-password`
  const confirmPasswordId = `${formId}-confirm-password`
  const passwordHintId = `${formId}-password-hint`

  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setFieldErrors({})
    setFormError(null)
    setStatusMessage(null)

    const nextErrors: Record<string, string> = {}
    const validation = changePasswordRequestSchema.safeParse({ currentPassword, newPassword })
    if (!validation.success) {
      for (const issue of validation.error.issues) {
        const field = String(issue.path[0] ?? 'form')
        nextErrors[field] = issue.message
      }
    }

    if (confirmPassword.length === 0) {
      nextErrors.confirmPassword = 'Confirm your new password.'
    } else if (newPassword !== confirmPassword) {
      nextErrors.confirmPassword = 'New passwords do not match.'
    }

    if (Object.keys(nextErrors).length > 0) {
      setFieldErrors(nextErrors)
      return
    }

    setIsSaving(true)

    try {
      await onChangePassword(currentPassword, newPassword)
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
      setStatusMessage('Password changed successfully. Your session cookie was refreshed securely.')
    } catch (error) {
      setFormError(getErrorMessage(error))
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <form className="space-y-4" onSubmit={(event) => void handleSubmit(event)} noValidate>
      <div className="space-y-2">
        <Label htmlFor={currentPasswordId}>Current password</Label>
        <Input
          id={currentPasswordId}
          type="password"
          autoComplete="current-password"
          value={currentPassword}
          onChange={(event) => setCurrentPassword(event.target.value)}
          aria-required="true"
          aria-invalid={fieldErrors.currentPassword ? 'true' : undefined}
          disabled={disabled || isSaving}
          required
        />
        {fieldErrors.currentPassword ? (
          <p className="text-sm text-destructive" role="alert" aria-live="polite">
            {fieldErrors.currentPassword}
          </p>
        ) : null}
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor={newPasswordId}>New password</Label>
          <Input
            id={newPasswordId}
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            aria-required="true"
            aria-invalid={fieldErrors.newPassword ? 'true' : undefined}
            aria-describedby={fieldErrors.newPassword ? undefined : passwordHintId}
            disabled={disabled || isSaving}
            required
          />
          {fieldErrors.newPassword ? (
            <p className="text-sm text-destructive" role="alert" aria-live="polite">
              {fieldErrors.newPassword}
            </p>
          ) : (
            <p id={passwordHintId} className="text-sm text-muted-foreground">
              Use at least 12 characters. Choose a password different from your current one.
            </p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor={confirmPasswordId}>Confirm new password</Label>
          <Input
            id={confirmPasswordId}
            type="password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            aria-required="true"
            aria-invalid={fieldErrors.confirmPassword ? 'true' : undefined}
            disabled={disabled || isSaving}
            required
          />
          {fieldErrors.confirmPassword ? (
            <p className="text-sm text-destructive" role="alert" aria-live="polite">
              {fieldErrors.confirmPassword}
            </p>
          ) : null}
        </div>
      </div>

      {formError ? (
        <p className="text-sm text-destructive" role="alert" aria-live="polite">
          {formError}
        </p>
      ) : null}

      {statusMessage ? (
        <SuccessMessage onDismiss={() => setStatusMessage(null)}>{statusMessage}</SuccessMessage>
      ) : null}

      <Button type="submit" aria-busy={isSaving} disabled={disabled || isSaving}>
        {isSaving ? (
          <>
            <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
            Updating password...
          </>
        ) : (
          'Change password'
        )}
      </Button>
    </form>
  )
}

function DeleteAccountSection({
  email,
  onDelete,
  disabled,
}: {
  email: string
  onDelete: () => Promise<void>
  disabled?: boolean
}) {
  const navigate = useNavigate()
  const [isOpen, setIsOpen] = useState(false)
  const [confirmation, setConfirmation] = useState('')
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  const canDelete = confirmation.trim().toLowerCase() === email.trim().toLowerCase()

  const handleDelete = async () => {
    setErrorMessage(null)
    setIsDeleting(true)

    try {
      await onDelete()
      setIsOpen(false)
      navigate('/login/', { replace: true })
    } catch (error) {
      setErrorMessage(getErrorMessage(error))
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <div className="space-y-4 rounded-xl border border-destructive/30 bg-destructive/5 p-5">
      <div className="space-y-1">
        <h3 className="text-base font-semibold text-foreground">Delete account</h3>
        <p className="text-sm text-muted-foreground">
          Permanently soft-delete your account. You will be signed out and cannot authenticate again with this account.
        </p>
      </div>

      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogTrigger asChild>
          <Button type="button" variant="destructive" disabled={disabled}>
            <Trash2 className="size-4" aria-hidden="true" />
            Delete account
          </Button>
        </DialogTrigger>
        <DialogContent aria-describedby="delete-account-description">
          <DialogHeader>
            <DialogTitle>Delete your account?</DialogTitle>
            <DialogDescription id="delete-account-description">
              This action soft-deletes your account on the backend. Type your email address to confirm.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label htmlFor="delete-account-confirmation">Confirm email address</Label>
            <Input
              id="delete-account-confirmation"
              type="email"
              value={confirmation}
              onChange={(event) => setConfirmation(event.target.value)}
              autoComplete="off"
              aria-required="true"
              disabled={isDeleting}
            />
          </div>

          {errorMessage ? (
            <p className="text-sm text-destructive" role="alert" aria-live="polite">
              {errorMessage}
            </p>
          ) : null}

          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" disabled={isDeleting}>
                Cancel
              </Button>
            </DialogClose>
            <Button type="button" variant="destructive" aria-busy={isDeleting} disabled={!canDelete || isDeleting} onClick={() => void handleDelete()}>
              Confirm delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

export function AccountSettingsSection() {
  const {
    profile,
    isLoading,
    isRefreshing,
    error,
    isAvailable,
    refreshProfile,
    updateProfile,
    changePassword,
    deleteAccount,
  } = useAccountProfile()

  if (!isAvailable) {
    return (
      <PageSection
        title="Account"
        titleId="settings-account-heading"
        icon={UserRound}
        description="Manage your profile, password, and account lifecycle through the backend account API."
      >
        <Empty aria-label="Account management unavailable">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <UserRound className="size-5" aria-hidden="true" />
            </EmptyMedia>
            <EmptyTitle>Sign in to manage your account</EmptyTitle>
            <EmptyDescription>
              Account profile management requires an authenticated session. Enable auth and sign in to view or update
              your profile.
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      </PageSection>
    )
  }

  return (
    <PageSection
      title="Account"
      titleId="settings-account-heading"
      icon={UserRound}
      description="View and update your profile, change your password for email accounts, or delete your account."
    >
      <div className="flex justify-end">
        <Button type="button" variant="outline" size="sm" onClick={() => void refreshProfile()} aria-busy={isRefreshing} disabled={isLoading || isRefreshing}>
          <LoaderCircle className={isRefreshing ? 'size-4 animate-spin' : 'size-4'} aria-hidden="true" />
          {isRefreshing ? 'Refreshing...' : 'Refresh profile'}
        </Button>
      </div>

      {isLoading ? <AccountProfileSkeleton /> : null}

      {!isLoading && error ? (
        <div className="space-y-4">
          <ErrorAlert error={error} title="Could not load account profile" />
          <Button type="button" variant="outline" onClick={() => void refreshProfile()}>
            Retry
          </Button>
        </div>
      ) : null}

      {!isLoading && !error && profile ? (
        <div className="space-y-8">
          <ProfileMetadata profile={profile} />
          <ProfileUpdateForm
            key={profile.userId}
            profile={profile}
            disabled={isRefreshing}
            onSave={async (request) => {
              await updateProfile(request)
            }}
          />

          {profile.hasPasswordLogin ? (
            <div className="space-y-4 border-t border-border/60 pt-8">
              <div className="space-y-1">
                <h3 className="flex items-center gap-2 text-base font-semibold">
                  <KeyRound className="size-4" aria-hidden="true" />
                  Password
                </h3>
                <p className="text-sm text-muted-foreground">
                  Change your password for email sign-in. OAuth-only accounts do not use a local password.
                </p>
              </div>
              <PasswordChangeForm
                disabled={isRefreshing}
                onChangePassword={async (currentPassword, newPassword) => {
                  await changePassword({ currentPassword, newPassword })
                }}
              />
            </div>
          ) : null}

          <DeleteAccountSection email={profile.email} disabled={isRefreshing} onDelete={deleteAccount} />
        </div>
      ) : null}
    </PageSection>
  )
}
