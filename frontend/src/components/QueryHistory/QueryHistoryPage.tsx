import { useMemo, useState } from 'react'
import {
  flexRender,
  getCoreRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type SortingState,
} from '@tanstack/react-table'
import {
  AlertTriangle,
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  DatabaseZap,
  History,
  Play,
  RefreshCw,
  ShieldCheck,
  Trash2,
} from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'

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
  DialogTrigger,
} from '@/components/ui/dialog'
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { ErrorAlert } from '@/components/ui/error-alert'
import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { StatTile } from '@/components/ui/stat-tile'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { type LocalQueryHistoryEntry, useLocalQueryHistory } from '@/hooks/use-local-query-history'
import { formatRelativeTime, formatTimestamp } from '@/lib/date'
import { formatExecutionMs } from '@/lib/utils'

import { QueryHistoryPageSkeleton } from './QueryHistoryPageSkeleton'

function shortHash(value: string): string {
  return value.length > 12 ? `${value.slice(0, 12)}...` : value
}

function SortIndicator({ direction }: { direction: false | 'asc' | 'desc' }) {
  if (direction === 'asc') {
    return <ArrowUp className="size-3.5" aria-hidden="true" />
  }

  if (direction === 'desc') {
    return <ArrowDown className="size-3.5" aria-hidden="true" />
  }

  return <ArrowUpDown className="size-3.5 opacity-50" aria-hidden="true" />
}

export function QueryHistoryPage() {
  const navigate = useNavigate()
  const { entries, isLoading, error, clearHistory, refresh } = useLocalQueryHistory()
  const [sorting, setSorting] = useState<SortingState>([{ id: 'timestamp', desc: true }])
  const [isConfirmOpen, setIsConfirmOpen] = useState(false)
  const [isClearing, setIsClearing] = useState(false)

  const columns = useMemo<ColumnDef<LocalQueryHistoryEntry>[]>(
    () => [
      {
        accessorKey: 'timestamp',
        header: 'Executed',
        cell: ({ row }) => (
          <div className="space-y-1">
            <p className="font-medium">{formatRelativeTime(row.original.timestamp)}</p>
            <p className="text-xs text-muted-foreground">{formatTimestamp(row.original.timestamp)}</p>
          </div>
        ),
        sortingFn: (left, right) =>
          new Date(left.original.timestamp).getTime() - new Date(right.original.timestamp).getTime(),
      },
      {
        accessorKey: 'queryId',
        header: 'Query ID',
        cell: ({ row }) => <span className="font-mono text-xs">{row.original.queryId}</span>,
      },
      {
        accessorKey: 'sqlHash',
        header: 'SQL hash',
        cell: ({ row }) => (
          <span className="font-mono text-xs" title={row.original.sqlHash}>
            {shortHash(row.original.sqlHash)}
          </span>
        ),
      },
      {
        accessorKey: 'rowCount',
        header: 'Rows',
        cell: ({ row }) => row.original.rowCount.toLocaleString(),
      },
      {
        accessorKey: 'executionMs',
        header: 'Duration',
        cell: ({ row }) => formatExecutionMs(row.original.executionMs),
      },
      {
        accessorKey: 'degraded',
        header: 'Mode',
        cell: ({ row }) => (
          <div className="flex flex-wrap gap-2">
            <Badge variant={row.original.async ? 'secondary' : 'outline'}>{row.original.async ? 'Async' : 'Sync'}</Badge>
            {row.original.degraded ? (
              <Badge variant="destructive" className="gap-1">
                <AlertTriangle className="size-3" aria-hidden="true" />
                Degraded
              </Badge>
            ) : (
              <Badge variant="outline" className="gap-1">
                <ShieldCheck className="size-3" aria-hidden="true" />
                Complete
              </Badge>
            )}
          </div>
        ),
      },
      {
        id: 'actions',
        header: 'Actions',
        cell: ({ row }) => {
          const entry = row.original
          const canRerun = Boolean(entry.sql)

          return (
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={!canRerun}
                title={canRerun ? 'Re-run saved SQL' : 'SQL text was not stored for this entry'}
                onClick={() => {
                  if (entry.sql) {
                    navigate('/query', { state: { sql: entry.sql, fromHistory: true } })
                  }
                }}
              >
                <Play className="size-3.5" aria-hidden="true" />
                Re-run
              </Button>
              <Button asChild size="sm" variant="outline">
                <Link to={`/query/${entry.queryId}`}>
                  <DatabaseZap className="size-3.5" aria-hidden="true" />
                  View result
                </Link>
              </Button>
            </div>
          )
        },
        enableSorting: false,
      },
    ],
    [navigate],
  )

  const table = useReactTable({
    data: entries,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  })

  const totalRows = entries.reduce((sum, entry) => sum + entry.rowCount, 0)
  const degradedCount = entries.filter((entry) => entry.degraded).length
  const rerunnableCount = entries.filter((entry) => Boolean(entry.sql)).length

  const handleClearHistory = async () => {
    setIsClearing(true)
    await clearHistory()
    setIsClearing(false)
    setIsConfirmOpen(false)
  }

  if (isLoading) {
    return (
      <main aria-labelledby="query-history-title" className="space-y-8">
        <QueryHistoryPageSkeleton />
      </main>
    )
  }

  return (
    <main aria-labelledby="query-history-title" className="space-y-8">
      <PageHeader
        title="Query History"
        titleId="query-history-title"
        description="Review recent query executions stored locally in this browser. The default history record keeps SQL hashes and metadata only."
        badge="Local IndexedDB"
        actions={
          <>
            <Button type="button" variant="outline" onClick={() => void refresh()} className="gap-2">
              <RefreshCw className="size-4" aria-hidden="true" />
              Refresh
            </Button>
            <Dialog open={isConfirmOpen} onOpenChange={setIsConfirmOpen}>
              <DialogTrigger asChild>
                <Button type="button" variant="destructive" disabled={entries.length === 0} className="gap-2">
                  <Trash2 className="size-4" aria-hidden="true" />
                  Clear history
                </Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Clear local query history?</DialogTitle>
                  <DialogDescription>
                    This removes locally stored query metadata from IndexedDB on this device. It does not cancel running
                    queries or remove backend result cache entries.
                  </DialogDescription>
                </DialogHeader>
                <DialogFooter>
                  <DialogClose asChild>
                    <Button type="button" variant="outline">
                      Keep history
                    </Button>
                  </DialogClose>
                  <Button type="button" variant="destructive" onClick={() => void handleClearHistory()} aria-busy={isClearing}>
                    {isClearing ? 'Clearing...' : 'Clear history'}
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </>
        }
      />

      {error ? <ErrorAlert error={error} title="Local history unavailable" /> : null}

      <div className="grid gap-4 md:grid-cols-3">
        <StatTile label="Saved executions" icon={History}>
          <p className="text-2xl font-semibold tracking-tight">{entries.length.toLocaleString()}</p>
        </StatTile>
        <StatTile label="Rows observed" icon={DatabaseZap} hint="Aggregated from local metadata only.">
          <p className="text-2xl font-semibold tracking-tight">{totalRows.toLocaleString()}</p>
        </StatTile>
        <StatTile label="Recovery signals" icon={AlertTriangle} hint={`${rerunnableCount} entries include opt-in SQL text.`}>
          <p className="text-2xl font-semibold tracking-tight">{degradedCount.toLocaleString()} degraded</p>
        </StatTile>
      </div>

      {entries.length === 0 ? (
        <Empty aria-label="No query history">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <History className="size-5" aria-hidden="true" />
            </EmptyMedia>
            <EmptyTitle>No query history yet</EmptyTitle>
            <EmptyDescription>
              Successful sync and async query results will appear here after execution. Parameter values and tokens are
              never stored in this local history database.
            </EmptyDescription>
          </EmptyHeader>
          <EmptyContent>
            <Button asChild>
              <Link to="/query">
                <DatabaseZap className="size-4" aria-hidden="true" />
                Open query console
              </Link>
            </Button>
          </EmptyContent>
        </Empty>
      ) : (
        <PageSection
          title="Recent executions"
          titleId="query-history-table-heading"
          icon={History}
          description="Use row actions to inspect cached async results or re-run entries that include explicitly saved SQL."
          contentClassName="p-0"
        >
          <div className="overflow-hidden">
            <Table aria-label="Local query history">
              <TableHeader>
                {table.getHeaderGroups().map((headerGroup) => (
                  <TableRow key={headerGroup.id}>
                    {headerGroup.headers.map((header) => (
                      <TableHead key={header.id} scope="col" className="px-4 py-3">
                        {header.isPlaceholder ? null : header.column.getCanSort() ? (
                          <button
                            type="button"
                            className="inline-flex items-center gap-2 text-left font-medium"
                            onClick={header.column.getToggleSortingHandler()}
                            aria-label={`Sort by ${String(header.column.columnDef.header)}`}
                          >
                            {flexRender(header.column.columnDef.header, header.getContext())}
                            <SortIndicator direction={header.column.getIsSorted()} />
                          </button>
                        ) : (
                          flexRender(header.column.columnDef.header, header.getContext())
                        )}
                      </TableHead>
                    ))}
                  </TableRow>
                ))}
              </TableHeader>
              <TableBody>
                {table.getRowModel().rows.map((row) => (
                  <TableRow key={row.id}>
                    {row.getVisibleCells().map((cell) => (
                      <TableCell key={cell.id} className="px-4 py-3">
                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </PageSection>
      )}
    </main>
  )
}
