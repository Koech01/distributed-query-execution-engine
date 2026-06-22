import { Activity } from 'lucide-react'

import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { Skeleton } from '@/components/ui/skeleton'

export function OperationsPageSkeleton() {
  return (
    <div aria-label="Loading operations dashboard" aria-busy="true" className="space-y-8">
      <PageHeader
        title="Operations"
        titleId="operations-title"
        description="Checking API liveness, readiness, and observability configuration."
        badge="Health & observability"
      />

      <div className="grid gap-5 lg:grid-cols-2">
        <Skeleton className="h-56 rounded-xl" />
        <Skeleton className="h-56 rounded-xl" />
      </div>

      <PageSection title="System context" icon={Activity}>
        <div className="grid gap-4 md:grid-cols-2">
          <Skeleton className="h-28 rounded-xl" />
          <Skeleton className="h-28 rounded-xl" />
        </div>
      </PageSection>
    </div>
  )
}
