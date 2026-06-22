import { Skeleton } from '@/components/ui/skeleton'

export function QueryConsolePageSkeleton() {
  return (
    <div aria-busy="true" aria-label="Loading query console" className="space-y-8">
      <div className="space-y-3 border-b border-border/60 pb-6">
        <Skeleton className="h-5 w-28" />
        <Skeleton className="h-10 w-56" />
        <Skeleton className="h-5 w-full max-w-2xl" />
      </div>
      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
        <div className="space-y-6">
          <Skeleton className="h-56 w-full rounded-xl" />
          <Skeleton className="h-40 w-full rounded-xl" />
          <Skeleton className="h-16 w-full rounded-xl" />
        </div>
        <Skeleton className="h-96 w-full rounded-xl" />
      </div>
    </div>
  )
}
