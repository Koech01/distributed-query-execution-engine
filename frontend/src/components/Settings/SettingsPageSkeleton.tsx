import { Settings, UserRound } from 'lucide-react'

import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { Skeleton } from '@/components/ui/skeleton'

export function SettingsPageSkeleton() {
  return (
    <div aria-busy="true" aria-label="Loading settings" className="space-y-8">
      <PageHeader
        title="Settings"
        titleId="settings-title"
        description="Loading your account profile, saved query defaults, and history preferences."
        badge="Preferences and account"
      />

      <PageSection title="Account" titleId="settings-account-heading" icon={UserRound}>
        <div className="grid gap-4 md:grid-cols-2">
          <Skeleton className="h-28 rounded-xl" />
          <Skeleton className="h-28 rounded-xl" />
        </div>
        <Skeleton className="h-40 rounded-xl" />
      </PageSection>

      <PageSection title="Query defaults" titleId="settings-defaults-heading" icon={Settings}>
        <div className="grid gap-5 md:grid-cols-2">
          <Skeleton className="h-24 rounded-xl" />
          <Skeleton className="h-24 rounded-xl" />
          <Skeleton className="h-24 rounded-xl md:col-span-2" />
        </div>
        <div className="flex flex-wrap gap-3 pt-2">
          <Skeleton className="h-10 w-36 rounded-md" />
          <Skeleton className="h-10 w-32 rounded-md" />
        </div>
      </PageSection>
    </div>
  )
}
