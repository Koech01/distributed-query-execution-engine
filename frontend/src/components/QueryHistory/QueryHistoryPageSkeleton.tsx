import { History } from 'lucide-react'

import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { Skeleton } from '@/components/ui/skeleton'

export function QueryHistoryPageSkeleton() {
  return (
    <div aria-label="Loading query history" aria-busy="true" className="space-y-8">
      <PageHeader
        title="Query History"
        titleId="query-history-title"
        description="Loading locally stored query metadata from this browser."
        badge="Local history"
      />

      <div className="grid gap-4 md:grid-cols-3">
        {Array.from({ length: 3 }, (_, index) => (
          <Skeleton key={index} className="h-28 rounded-xl" />
        ))}
      </div>

      <PageSection title="Recent executions" icon={History} contentClassName="space-y-4">
        <div className="space-y-3">
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-12 rounded-lg" />
          ))}
        </div>
      </PageSection>
    </div>
  )
}
