import { GitBranch, Layers3, ListTree, SearchCode } from 'lucide-react'

import type { QueryPlanDetails } from '@/components/types'
import { ErrorAlert } from '@/components/ui/error-alert'
import { PageSection } from '@/components/ui/page-section'
import { StatTile } from '@/components/ui/stat-tile'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from '@/components/ui/empty'
import { formatTimestamp } from '@/lib/date'
import { cn } from '@/lib/utils'

interface QueryPlanPanelProps {
  plan: QueryPlanDetails | null
  isLoading: boolean
  error: unknown
  onInspect: () => void
  onDismiss: () => void
  disabled?: boolean
}

function formatTargetingStrategy(strategy: string): string {
  return strategy
    .split('_')
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ')
}

function formatAggregateFunction(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

export function QueryPlanPanel({
  plan,
  isLoading,
  error,
  onInspect,
  onDismiss,
  disabled = false,
}: QueryPlanPanelProps) {
  return (
    <PageSection
      title="Query plan"
      titleId="query-plan-heading"
      icon={GitBranch}
      description="Inspect shard targeting and merge instructions without executing the query."
    >
      <div className="flex flex-wrap gap-3">
        <Button type="button" onClick={onInspect} disabled={disabled || isLoading} aria-busy={isLoading} className="gap-2">
          <SearchCode className="size-4" aria-hidden="true" />
          {isLoading ? 'Inspecting plan...' : 'Inspect plan'}
        </Button>
        {plan || error ? (
          <Button type="button" variant="outline" onClick={onDismiss} disabled={disabled || isLoading}>
            Clear plan
          </Button>
        ) : null}
      </div>

      {isLoading ? (
        <div className="space-y-4" aria-live="polite" aria-busy="true">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {Array.from({ length: 4 }).map((_, index) => (
              <Skeleton key={index} className="h-24 rounded-xl" />
            ))}
          </div>
          <Skeleton className="h-40 rounded-xl" />
        </div>
      ) : null}

      {error ? <ErrorAlert error={error} title="Plan inspection failed" /> : null}

      {!isLoading && !error && !plan ? (
        <Empty aria-label="No query plan inspected">
          <EmptyHeader>
            <EmptyTitle>No plan loaded</EmptyTitle>
            <EmptyDescription>
              Inspect the current SQL and parameters to preview shard routing, sub-queries, and merge behavior.
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      ) : null}

      {plan ? (
        <div className="animate-fade-in-up space-y-6">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <StatTile label="Targeting" icon={Layers3}>
              <p className="text-lg font-semibold tracking-tight">{formatTargetingStrategy(plan.targetingStrategy)}</p>
            </StatTile>
            <StatTile label="Cluster shards" icon={GitBranch}>
              <p className="text-lg font-semibold tracking-tight">{plan.clusterShardCount.toLocaleString()}</p>
            </StatTile>
            <StatTile label="Sub-queries" icon={ListTree}>
              <p className="text-lg font-semibold tracking-tight">{plan.subQueries.length.toLocaleString()}</p>
            </StatTile>
            <StatTile label="Plan cache" hint={`Hash ${plan.planHash.slice(0, 12)}…`}>
              <div className="flex items-center gap-2">
                <p className="text-lg font-semibold tracking-tight">{plan.fromCache ? 'Hit' : 'Miss'}</p>
                <Badge variant={plan.fromCache ? 'secondary' : 'outline'}>{plan.fromCache ? 'Cached' : 'Fresh'}</Badge>
              </div>
            </StatTile>
          </div>

          <div className="rounded-xl border border-border/60 bg-muted/20 p-4">
            <h3 className="text-sm font-semibold">Merge instructions</h3>
            <dl className="mt-3 grid gap-3 text-sm sm:grid-cols-2">
              <div>
                <dt className="text-muted-foreground">Order by</dt>
                <dd>
                  {plan.mergeInstructions.orderBy.length > 0
                    ? plan.mergeInstructions.orderBy
                        .map((column) => `${column.columnName}${column.descending ? ' DESC' : ' ASC'}`)
                        .join(', ')
                    : 'None'}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Aggregates</dt>
                <dd>
                  {plan.mergeInstructions.aggregates.length > 0
                    ? plan.mergeInstructions.aggregates
                        .map(
                          (aggregate) =>
                            `${formatAggregateFunction(aggregate.function)}(${aggregate.sourceColumn}) AS ${aggregate.outputAlias}`,
                        )
                        .join('; ')
                    : 'None'}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Limit / offset</dt>
                <dd>
                  {plan.mergeInstructions.limit ?? 'No limit'}
                  {plan.mergeInstructions.offset != null ? ` · offset ${plan.mergeInstructions.offset}` : ''}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Distinct</dt>
                <dd>{plan.mergeInstructions.isDistinct ? 'Yes' : 'No'}</dd>
              </div>
              <div className="sm:col-span-2">
                <dt className="text-muted-foreground">Created</dt>
                <dd>{formatTimestamp(plan.createdAt)}</dd>
              </div>
            </dl>
          </div>

          <div className="overflow-hidden rounded-xl border border-border/60">
            <div className="border-b border-border/60 bg-muted/30 px-4 py-3">
              <h3 className="text-sm font-semibold">Sub-queries</h3>
              <p className="text-sm text-muted-foreground">Per-shard SQL rewritten by the planner.</p>
            </div>
            {plan.subQueries.length === 0 ? (
              <div className="p-4">
                <Empty aria-label="No sub-queries in plan">
                  <EmptyHeader>
                    <EmptyTitle>No sub-queries generated</EmptyTitle>
                    <EmptyDescription>The planner did not produce shard-specific SQL for this request.</EmptyDescription>
                  </EmptyHeader>
                </Empty>
              </div>
            ) : (
              <Table aria-label="Query plan sub-queries">
                <TableHeader>
                  <TableRow>
                    <TableHead scope="col">Shard</TableHead>
                    <TableHead scope="col">Total shards</TableHead>
                    <TableHead scope="col">Sub-query ID</TableHead>
                    <TableHead scope="col">SQL</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {plan.subQueries.map((subQuery) => (
                    <TableRow key={subQuery.subQueryId}>
                      <TableCell>{subQuery.shardIndex}</TableCell>
                      <TableCell>{subQuery.totalShards}</TableCell>
                      <TableCell className="font-mono text-xs">{subQuery.subQueryId}</TableCell>
                      <TableCell>
                        <pre className={cn('max-w-full overflow-x-auto whitespace-pre-wrap font-mono text-xs leading-relaxed')}>
                          {subQuery.sql}
                        </pre>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </div>
        </div>
      ) : null}
    </PageSection>
  )
}
