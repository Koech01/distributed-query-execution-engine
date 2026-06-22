import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { Skeleton } from '@/components/ui/skeleton'
import { StatTile } from '@/components/ui/stat-tile'

export function AdminDashboardPageSkeleton() {
  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-8" aria-busy="true" aria-live="polite">
      <PageHeader
        title="Administration"
        titleId="admin-dashboard-title"
        description="Loading cluster overview, active queries, and worker health."
        badge="Admin"
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 6 }).map((_, index) => (
          <StatTile key={index} label="Loading metric">
            <Skeleton className="h-7 w-24" />
          </StatTile>
        ))}
      </div>

      <PageSection title="Active queries" titleId="active-queries-skeleton" srOnlyTitle>
        <Skeleton className="h-48 w-full rounded-xl" />
      </PageSection>

      <PageSection title="Worker health" titleId="worker-health-skeleton" srOnlyTitle>
        <Skeleton className="h-48 w-full rounded-xl" />
      </PageSection>
    </div>
  )
}
