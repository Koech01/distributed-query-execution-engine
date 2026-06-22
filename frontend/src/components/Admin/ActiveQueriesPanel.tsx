import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table'
import { Ban, Database, RefreshCw } from 'lucide-react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'

import type { ActiveQuerySummary } from '@/components/types/admin'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { ErrorAlert } from '@/components/ui/error-alert'
import { PageSection } from '@/components/ui/page-section'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { adminApi } from '@/lib/api'
import { formatActiveQueryKind, formatElapsedSince } from '@/lib/admin-display'
import { formatRelativeTime, formatTimestamp } from '@/lib/date'

const REFRESH_INTERVAL_MS = 10_000

function shortHash(value: string): string {
  return value.length > 16 ? `${value.slice(0, 16)}…` : value
}

export function ActiveQueriesPanel() {
  const [queries, setQueries] = useState<ActiveQuerySummary[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [isLoading, setIsLoading] = useState(true)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [lastCheckedAt, setLastCheckedAt] = useState<Date | null>(null)
  const [cancelTarget, setCancelTarget] = useState<ActiveQuerySummary | null>(null)
  const [isCancelling, setIsCancelling] = useState(false)

  const loadQueries = useCallback(async (signal?: AbortSignal, showRefreshing = false) => {
    if (showRefreshing) {
      setIsRefreshing(true)
    }

    try {
      const page = await adminApi.getActiveQueries({ limit: 50, offset: 0 }, signal)
      setQueries(page.queries)
      setTotalCount(page.totalCount)
      setLastCheckedAt(new Date())
      setError(null)
    } catch (loadError) {
      if (signal?.aborted) {
        return
      }

      setError(loadError)
    } finally {
      setIsLoading(false)
      setIsRefreshing(false)
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()

    void loadQueries(controller.signal)

    const intervalId = window.setInterval(() => {
      void loadQueries(controller.signal, true)
    }, REFRESH_INTERVAL_MS)

    return () => {
      controller.abort()
      window.clearInterval(intervalId)
    }
  }, [loadQueries])

  const handleCancel = async () => {
    if (!cancelTarget) {
      return
    }

    setIsCancelling(true)

    try {
      const result = await adminApi.cancelQuery(cancelTarget.queryId)

      if (result.cancellationRequested) {
        toast.success('Cancellation requested', {
          description: result.message,
        })
      } else {
        toast.info('Query not cancelled', {
          description: result.message,
        })
      }

      setCancelTarget(null)
      await loadQueries(undefined, true)
    } catch (cancelError) {
      toast.error('Cancel failed', {
        description: cancelError instanceof Error ? cancelError.message : 'Unable to cancel query.',
      })
    } finally {
      setIsCancelling(false)
    }
  }

  const columns = useMemo<ColumnDef<ActiveQuerySummary>[]>(
    () => [
      {
        accessorKey: 'queryId',
        header: 'Query ID',
        cell: ({ row }) => (
          <Link
            to={`/query/${row.original.queryId}`}
            className="font-mono text-xs underline-offset-4 hover:underline"
          >
            {row.original.queryId}
          </Link>
        ),
      },
      {
        accessorKey: 'kind',
        header: 'Kind',
        cell: ({ row }) => <Badge variant="outline">{formatActiveQueryKind(row.original.kind)}</Badge>,
      },
      {
        accessorKey: 'planHash',
        header: 'Plan hash',
        cell: ({ row }) => (
          <span className="font-mono text-xs" title={row.original.planHash}>
            {shortHash(row.original.planHash)}
          </span>
        ),
      },
      {
        accessorKey: 'subQueryCount',
        header: 'Sub-queries',
        cell: ({ row }) => row.original.subQueryCount.toLocaleString(),
      },
      {
        accessorKey: 'startedAt',
        header: 'Elapsed',
        cell: ({ row }) => (
          <div className="space-y-1">
            <p className="font-medium">{formatElapsedSince(row.original.startedAt)}</p>
            <p className="text-xs text-muted-foreground">{formatTimestamp(row.original.startedAt)}</p>
          </div>
        ),
      },
      {
        id: 'status',
        header: 'Status',
        cell: ({ row }) =>
          row.original.cancellationRequested ? (
            <Badge variant="secondary">Cancellation requested</Badge>
          ) : (
            <Badge variant="default">Running</Badge>
          ),
      },
      {
        id: 'actions',
        header: 'Actions',
        cell: ({ row }) => (
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={row.original.cancellationRequested}
            onClick={() => setCancelTarget(row.original)}
          >
            <Ban className="size-3.5" aria-hidden="true" />
            Cancel
          </Button>
        ),
      },
    ],
    [],
  )

  const table = useReactTable({
    data: queries,
    columns,
    getCoreRowModel: getCoreRowModel(),
  })

  return (
    <PageSection
      title="Active queries"
      titleId="active-queries-heading"
      icon={Database}
      description="In-flight queries across the cluster. Auto-refreshes every 10 seconds."
      contentClassName="space-y-4"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          {totalCount.toLocaleString()} active {totalCount === 1 ? 'query' : 'queries'}
          {lastCheckedAt ? ` · Updated ${formatRelativeTime(lastCheckedAt)}` : null}
        </p>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void loadQueries(undefined, true)}
          aria-busy={isRefreshing}
          disabled={isRefreshing}
        >
          <RefreshCw className={isRefreshing ? 'size-4 animate-spin' : 'size-4'} aria-hidden="true" />
          Refresh
        </Button>
      </div>

      {error ? <ErrorAlert error={error} title="Unable to load active queries" /> : null}

      {isLoading ? (
        <Skeleton className="h-40 w-full rounded-xl" aria-label="Loading active queries" />
      ) : queries.length === 0 ? (
        <Empty aria-label="No active queries">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <Database className="size-5" aria-hidden="true" />
            </EmptyMedia>
            <EmptyTitle>No active queries</EmptyTitle>
            <EmptyDescription>
              The cluster has no in-flight queries right now. New executions from the query console will appear here
              while they run.
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-border/60">
          <Table aria-label="Active queries">
            <caption className="sr-only">
              Active queries with identifiers, execution kind, plan hash, sub-query count, elapsed time, and cancel
              actions.
            </caption>
            <TableHeader>
              {table.getHeaderGroups().map((headerGroup) => (
                <TableRow key={headerGroup.id}>
                  {headerGroup.headers.map((header) => (
                    <TableHead key={header.id} scope="col">
                      {header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}
                    </TableHead>
                  ))}
                </TableRow>
              ))}
            </TableHeader>
            <TableBody>
              {table.getRowModel().rows.map((row) => (
                <TableRow key={row.id}>
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Dialog open={cancelTarget !== null} onOpenChange={(open) => !open && setCancelTarget(null)}>
        <DialogContent aria-describedby="cancel-query-description">
          <DialogHeader>
            <DialogTitle>Cancel active query?</DialogTitle>
            <DialogDescription id="cancel-query-description">
              Request cancellation for query{' '}
              <span className="font-mono text-xs">{cancelTarget?.queryId}</span>. Running shard work may take a moment
              to stop.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline">
                Keep running
              </Button>
            </DialogClose>
            <Button type="button" variant="destructive" onClick={() => void handleCancel()} aria-busy={isCancelling} disabled={isCancelling}>
              {isCancelling ? 'Cancelling…' : 'Confirm cancel'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </PageSection>
  )
}
