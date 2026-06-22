import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { Skeleton } from '@/components/ui/skeleton'
import { StatTile } from '@/components/ui/stat-tile'

export function CacheManagementPageSkeleton() {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-8" aria-busy="true" aria-live="polite">
      <PageHeader
        title="Cache management"
        titleId="cache-management-title"
        description="Loading plan cache statistics and flush controls."
        badge="Admin"
      />

      <div className="grid gap-4 md:grid-cols-3">
        {Array.from({ length: 3 }).map((_, index) => (
          <StatTile key={index} label="Loading cache metric">
            <Skeleton className="h-7 w-24" />
          </StatTile>
        ))}
      </div>

      <PageSection title="Flush controls" titleId="cache-flush-skeleton" srOnlyTitle>
        <Skeleton className="h-56 w-full rounded-xl" />
      </PageSection>
    </div>
  )
}
