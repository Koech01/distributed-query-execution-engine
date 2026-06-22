import { Skeleton } from '@/components/ui/skeleton'

export function QueryResultPageSkeleton() {
  return (
    <div aria-busy="true" aria-label="Loading query result" className="space-y-8">
      <div className="space-y-3 border-b border-border/60 pb-6">
        <Skeleton className="h-5 w-32" />
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-5 w-full max-w-2xl" />
      </div>
      <Skeleton className="h-36 w-full rounded-xl" />
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Skeleton className="h-32 rounded-xl" />
        <Skeleton className="h-32 rounded-xl" />
        <Skeleton className="h-32 rounded-xl" />
        <Skeleton className="h-32 rounded-xl" />
      </div>
      <Skeleton className="h-96 w-full rounded-xl" />
    </div>
  )
}
