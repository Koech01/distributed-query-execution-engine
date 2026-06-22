import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table'
import { RefreshCw, Server } from 'lucide-react'

import type { WorkerHealthEntry } from '@/components/types/admin'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { ErrorAlert } from '@/components/ui/error-alert'
import { PageSection } from '@/components/ui/page-section'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { adminApi } from '@/lib/api'
import { formatRelativeTime } from '@/lib/date'

import { WorkerProbeStatusBadge } from './WorkerProbeStatusBadge'

const REFRESH_INTERVAL_MS = 30_000

export function WorkerHealthPanel() {
  const [workers, setWorkers] = useState<WorkerHealthEntry[]>([])
  const [healthyCount, setHealthyCount] = useState(0)
  const [totalCount, setTotalCount] = useState(0)
  const [isLoading, setIsLoading] = useState(true)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [lastCheckedAt, setLastCheckedAt] = useState<Date | null>(null)

  const loadWorkers = useCallback(async (signal?: AbortSignal, showRefreshing = false) => {
    if (showRefreshing) {
      setIsRefreshing(true)
    }

    try {
      const dashboard = await adminApi.getWorkers(signal)
      setWorkers(dashboard.workers)
      setHealthyCount(dashboard.healthyCount)
      setTotalCount(dashboard.totalCount)
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

    void loadWorkers(controller.signal)

    const intervalId = window.setInterval(() => {
      void loadWorkers(controller.signal, true)
    }, REFRESH_INTERVAL_MS)

    return () => {
      controller.abort()
      window.clearInterval(intervalId)
    }
  }, [loadWorkers])

  const columns = useMemo<ColumnDef<WorkerHealthEntry>[]>(
    () => [
      {
        accessorKey: 'nodeId',
        header: 'Node',
        cell: ({ row }) => <span className="font-medium">{row.original.nodeId}</span>,
      },
      {
        id: 'endpoint',
        header: 'Endpoint',
        cell: ({ row }) => (
          <span className="font-mono text-xs">
            {row.original.address}:{row.original.grpcPort}
          </span>
        ),
      },
      {
        accessorKey: 'shards',
        header: 'Shards',
        cell: ({ row }) => row.original.shards.join(', ') || 'None',
      },
      {
        accessorKey: 'version',
        header: 'Version',
        cell: ({ row }) => row.original.version,
      },
      {
        id: 'probes',
        header: 'Probe status',
        cell: ({ row }) => (
          <div className="flex flex-wrap gap-2">
            <WorkerProbeStatusBadge label="Live" status={row.original.liveStatus} latencyMs={row.original.liveLatencyMs} />
            <WorkerProbeStatusBadge label="Ready" status={row.original.readyStatus} latencyMs={row.original.readyLatencyMs} />
            <WorkerProbeStatusBadge label="gRPC" status={row.original.grpcStatus} latencyMs={row.original.grpcLatencyMs} />
          </div>
        ),
      },
      {
        accessorKey: 'registeredInConsul',
        header: 'Consul',
        cell: ({ row }) => (
          <Badge variant={row.original.registeredInConsul ? 'default' : 'secondary'}>
            {row.original.registeredInConsul ? 'Registered' : 'Not registered'}
          </Badge>
        ),
      },
    ],
    [],
  )

  const table = useReactTable({
    data: workers,
    columns,
    getCoreRowModel: getCoreRowModel(),
  })

  return (
    <PageSection
      title="Worker health"
      titleId="worker-health-heading"
      icon={Server}
      description="Worker nodes, shard assignments, and probe status across live, ready, and gRPC checks."
      contentClassName="space-y-4"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          {healthyCount.toLocaleString()} of {totalCount.toLocaleString()} workers healthy
          {lastCheckedAt ? ` · Updated ${formatRelativeTime(lastCheckedAt)}` : null}
        </p>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void loadWorkers(undefined, true)}
          aria-busy={isRefreshing}
          disabled={isRefreshing}
        >
          <RefreshCw className={isRefreshing ? 'size-4 animate-spin' : 'size-4'} aria-hidden="true" />
          Refresh
        </Button>
      </div>

      {error ? <ErrorAlert error={error} title="Unable to load worker health" /> : null}

      {isLoading ? (
        <Skeleton className="h-40 w-full rounded-xl" aria-label="Loading worker health" />
      ) : workers.length === 0 ? (
        <Empty aria-label="No workers reported">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <Server className="size-5" aria-hidden="true" />
            </EmptyMedia>
            <EmptyTitle>No workers available</EmptyTitle>
            <EmptyDescription>
              The coordinator did not report any worker nodes. Verify cluster registration and worker health probes.
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-border/60">
          <Table aria-label="Worker health">
            <caption className="sr-only">
              Worker nodes with endpoints, shard assignments, versions, probe statuses, and Consul registration.
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
    </PageSection>
  )
}
