import { Info, RotateCcw, Save } from 'lucide-react'
import { useId, useState } from 'react'

import type { FailurePolicy } from '@/components/types'
import { DEFAULT_USER_PREFERENCES } from '@/components/types/preferences'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { SuccessMessage } from '@/components/ui/success-message'
import { Switch } from '@/components/ui/switch'
import type { StoredUserPreferences } from '@/hooks/use-preferences'
import { MAX_TIMEOUT_SECONDS, MIN_TIMEOUT_SECONDS } from '@/lib/schemas'

interface PreferencesFormProps {
  preferences: StoredUserPreferences
  onSave: (preferences: StoredUserPreferences) => Promise<void>
  onReset: () => Promise<void>
  disabled?: boolean
}

function toStoredPreferences(preferences: StoredUserPreferences): StoredUserPreferences {
  return {
    defaultTimeoutSeconds: preferences.defaultTimeoutSeconds,
    defaultFailurePolicy: preferences.defaultFailurePolicy,
    defaultAsync: preferences.defaultAsync,
    saveSqlInHistory: preferences.saveSqlInHistory,
  }
}

export function PreferencesForm({ preferences, onSave, onReset, disabled = false }: PreferencesFormProps) {
  const formId = useId()
  const timeoutErrorId = `${formId}-timeout-error`
  const failurePolicyHelpId = `${formId}-failure-policy-help`
  const asyncHelpId = `${formId}-async-help`
  const saveSqlHelpId = `${formId}-save-sql-help`
  const statusMessageId = `${formId}-status-message`

  const [draft, setDraft] = useState<StoredUserPreferences>(() => toStoredPreferences(preferences))
  const [timeoutError, setTimeoutError] = useState<string | null>(null)
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [isResetting, setIsResetting] = useState(false)

  const validateTimeout = (value: number): string | null => {
    if (!Number.isFinite(value) || !Number.isInteger(value)) {
      return 'Timeout must be a whole number of seconds.'
    }

    if (value < MIN_TIMEOUT_SECONDS || value > MAX_TIMEOUT_SECONDS) {
      return `Timeout must be between ${MIN_TIMEOUT_SECONDS} and ${MAX_TIMEOUT_SECONDS} seconds.`
    }

    return null
  }

  const handleSave = async () => {
    const nextTimeoutError = validateTimeout(draft.defaultTimeoutSeconds)
    setTimeoutError(nextTimeoutError)
    setStatusMessage(null)

    if (nextTimeoutError) {
      return
    }

    setIsSaving(true)

    try {
      await onSave(toStoredPreferences(draft))
      setStatusMessage('Preferences saved. Query Console defaults will apply the next time you open it.')
    } finally {
      setIsSaving(false)
    }
  }

  const handleReset = async () => {
    setTimeoutError(null)
    setStatusMessage(null)
    setIsResetting(true)

    try {
      await onReset()
      setDraft(toStoredPreferences(DEFAULT_USER_PREFERENCES))
      setStatusMessage('Preferences reset to defaults.')
    } finally {
      setIsResetting(false)
    }
  }

  const isBusy = disabled || isSaving || isResetting

  return (
    <form
      className="space-y-6"
      onSubmit={(event) => {
        event.preventDefault()
        void handleSave()
      }}
    >
      <div className="grid gap-5 md:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor={`${formId}-timeout`}>Default timeout (seconds)</Label>
          <Input
            id={`${formId}-timeout`}
            type="number"
            min={MIN_TIMEOUT_SECONDS}
            max={MAX_TIMEOUT_SECONDS}
            value={draft.defaultTimeoutSeconds}
            onChange={(event) => {
              setDraft((current) => ({
                ...current,
                defaultTimeoutSeconds: Number(event.target.value),
              }))
              setTimeoutError(null)
            }}
            disabled={isBusy}
            aria-required="true"
            aria-invalid={timeoutError ? 'true' : undefined}
            aria-describedby={timeoutError ? timeoutErrorId : undefined}
          />
          {timeoutError ? (
            <p id={timeoutErrorId} className="text-sm text-destructive" role="alert" aria-live="polite">
              {timeoutError}
            </p>
          ) : (
            <p className="text-sm text-muted-foreground">
              Applied to new Query Console sessions. Allowed range: {MIN_TIMEOUT_SECONDS}–{MAX_TIMEOUT_SECONDS} seconds.
            </p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor={`${formId}-failure-policy`}>Default failure policy</Label>
          <Select
            value={draft.defaultFailurePolicy}
            onValueChange={(value) =>
              setDraft((current) => ({
                ...current,
                defaultFailurePolicy: value as FailurePolicy,
              }))
            }
            disabled={isBusy}
          >
            <SelectTrigger id={`${formId}-failure-policy`} aria-describedby={failurePolicyHelpId}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="BestEffort">Best effort</SelectItem>
              <SelectItem value="StrictAll">Strict all</SelectItem>
            </SelectContent>
          </Select>
          <p id={failurePolicyHelpId} className="flex items-start gap-2 text-sm text-muted-foreground">
            <Info className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <span>
              The backend accepts <strong>StrictAll</strong> but does not currently enforce strict-all execution.
              Results may still include partial shard data.
            </span>
          </p>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="flex items-center justify-between gap-4 rounded-xl border border-border/60 bg-muted/20 p-4">
          <div className="space-y-1">
            <Label htmlFor={`${formId}-default-async`}>Default async execution</Label>
            <p id={asyncHelpId} className="text-sm text-muted-foreground">
              Start new Query Console sessions with async polling enabled.
            </p>
          </div>
          <Switch
            id={`${formId}-default-async`}
            checked={draft.defaultAsync}
            onCheckedChange={(checked) =>
              setDraft((current) => ({
                ...current,
                defaultAsync: checked,
              }))
            }
            disabled={isBusy}
            aria-describedby={asyncHelpId}
          />
        </div>

        <div className="flex items-center justify-between gap-4 rounded-xl border border-border/60 bg-muted/20 p-4">
          <div className="space-y-1">
            <Label htmlFor={`${formId}-save-sql`}>Save SQL in local history</Label>
            <p id={saveSqlHelpId} className="text-sm text-muted-foreground">
              Opt in to storing full SQL text for re-run actions. Parameter values are never stored.
            </p>
          </div>
          <Switch
            id={`${formId}-save-sql`}
            checked={draft.saveSqlInHistory}
            onCheckedChange={(checked) =>
              setDraft((current) => ({
                ...current,
                saveSqlInHistory: checked,
              }))
            }
            disabled={isBusy}
            aria-describedby={saveSqlHelpId}
          />
        </div>
      </div>

      {statusMessage ? (
        <SuccessMessage id={statusMessageId} onDismiss={() => setStatusMessage(null)}>
          {statusMessage}
        </SuccessMessage>
      ) : null}

      <div className="flex flex-wrap gap-3">
        <Button type="submit" disabled={isBusy} aria-busy={isSaving} className="gap-2">
          <Save className="size-4" aria-hidden="true" />
          {isSaving ? 'Saving...' : 'Save preferences'}
        </Button>
        <Button type="button" variant="outline" disabled={isBusy} aria-busy={isResetting} className="gap-2" onClick={() => void handleReset()}>
          <RotateCcw className="size-4" aria-hidden="true" />
          {isResetting ? 'Resetting...' : 'Reset to defaults'}
        </Button>
      </div>
    </form>
  )
}
