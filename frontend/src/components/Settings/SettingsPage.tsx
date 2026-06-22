import { AlertTriangle, DatabaseZap, Settings, ShieldCheck } from 'lucide-react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { ErrorAlert } from '@/components/ui/error-alert'
import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { StatTile } from '@/components/ui/stat-tile'
import { usePreferences } from '@/hooks/use-preferences'

import { AccountSettingsSection } from './AccountSettingsSection'
import { PreferencesForm } from './PreferencesForm'
import { SettingsPageSkeleton } from './SettingsPageSkeleton'

export function SettingsPage() {
  const { preferences, isLoading, error, savePreferences, resetPreferences } = usePreferences()

  if (isLoading) {
    return (
      <main aria-labelledby="settings-title" className="space-y-8">
        <SettingsPageSkeleton />
      </main>
    )
  }

  return (
    <main aria-labelledby="settings-title" className="space-y-8">
      <PageHeader
        title="Settings"
        titleId="settings-title"
        description="Manage your account profile, query defaults, and local history behavior. Access tokens stay in HttpOnly cookies and are never written to browser storage."
        badge="Preferences and account"
      />

      {error ? (
        <ErrorAlert error={error} title="Preferences unavailable" />
      ) : null}

      <AccountSettingsSection />

      <PageSection
        title="Query defaults"
        titleId="settings-defaults-heading"
        icon={Settings}
        description="These values pre-fill the Query Console when you open it. They do not change queries already in progress."
      >
        <PreferencesForm
          preferences={preferences}
          onSave={savePreferences}
          onReset={resetPreferences}
          disabled={Boolean(error)}
        />
      </PageSection>

      <PageSection
        title="Privacy and storage"
        titleId="settings-privacy-heading"
        icon={ShieldCheck}
        description="What the frontend stores locally for convenience."
      >
        <div className="grid gap-4 md:grid-cols-2">
          <StatTile label="Stored locally" icon={DatabaseZap} hint="Browser localStorage key: dqee-user-preferences">
            <p className="text-sm leading-relaxed">
              Default timeout, failure policy, async toggle, and save-SQL opt-in only. No JWTs, parameter values, or
              backend credentials.
            </p>
          </StatTile>
          <StatTile label="History behavior" icon={AlertTriangle} hint="IndexedDB stores metadata by default">
            <p className="text-sm leading-relaxed">
              {preferences.saveSqlInHistory
                ? 'Full SQL text is saved for re-run when you opt in. Disable above to keep metadata-only history entries.'
                : 'Query history keeps metadata and SQL hashes only until you opt in to saving full SQL text.'}
            </p>
          </StatTile>
        </div>

        <Alert aria-live="polite">
          <ShieldCheck className="size-4" aria-hidden="true" />
          <AlertTitle>Non-sensitive storage only</AlertTitle>
          <AlertDescription>
            Theme selection continues to use the header toggle and next-themes storage. Access tokens are stored in
            HttpOnly cookies issued by the backend and are not accessible to frontend JavaScript.
          </AlertDescription>
        </Alert>
      </PageSection>
    </main>
  )
}
