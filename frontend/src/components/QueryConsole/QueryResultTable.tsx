import { useMemo, useRef, useState } from 'react'
import {
  flexRender,
  getCoreRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type SortingState,
} from '@tanstack/react-table'
import { useVirtualizer } from '@tanstack/react-virtual'
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'

import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from '@/components/ui/empty'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { QueryResult } from '@/components/types'
import { cn } from '@/lib/utils'

import { VIRTUALIZATION_ROW_THRESHOLD } from './constants'

interface QueryResultTableProps {
  result: Pick<QueryResult, 'columns' | 'rows' | 'rowCount'>
  isStreaming?: boolean
}

interface ResultRow {
  id: string
  values: Record<string, string>
}

const ROW_HEIGHT_PX = 40
const VIRTUAL_VIEWPORT_HEIGHT_PX = 480

function buildRows(columns: string[], rows: string[][]): ResultRow[] {
  return rows.map((row, rowIndex) => ({
    id: `row-${rowIndex}`,
    values: Object.fromEntries(columns.map((column, columnIndex) => [column, row[columnIndex] ?? ''])),
  }))
}

function SortIndicator({ direction }: { direction: false | 'asc' | 'desc' }) {
  if (direction === 'asc') {
    return <ArrowUp className="size-4" aria-hidden="true" />
  }

  if (direction === 'desc') {
    return <ArrowDown className="size-4" aria-hidden="true" />
  }

  return <ArrowUpDown className="size-4 opacity-50" aria-hidden="true" />
}

export function QueryResultTable({ result, isStreaming = false }: QueryResultTableProps) {
  const [sorting, setSorting] = useState<SortingState>([])
  const parentRef = useRef<HTMLDivElement>(null)
  const data = useMemo(() => buildRows(result.columns, result.rows), [result.columns, result.rows])
  const shouldVirtualize = data.length > VIRTUALIZATION_ROW_THRESHOLD

  const columns = useMemo<ColumnDef<ResultRow>[]>(
    () =>
      result.columns.map((column) => ({
        accessorKey: `values.${column}`,
        id: column,
        header: ({ column: tableColumn }) => {
          const sortDirection = tableColumn.getIsSorted()

          return (
            <button
              type="button"
              className="inline-flex w-full items-center gap-2 text-left font-medium"
              onClick={() => tableColumn.toggleSorting(sortDirection === 'asc')}
              aria-label={`Sort by ${column}`}
            >
              <span>{column}</span>
              <SortIndicator direction={sortDirection} />
            </button>
          )
        },
        accessorFn: (row) => row.values[column] ?? '',
        cell: ({ getValue }) => <span className="font-mono text-xs">{String(getValue() ?? '')}</span>,
      })),
    [result.columns],
  )

  const table = useReactTable({
    data,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  })

  const tableRows = table.getRowModel().rows

  const rowVirtualizer = useVirtualizer({
    count: shouldVirtualize ? tableRows.length : 0,
    getScrollElement: () => parentRef.current,
    estimateSize: () => ROW_HEIGHT_PX,
    overscan: 12,
  })

  if (result.columns.length === 0) {
    return (
      <Empty aria-label="No query results">
        <EmptyHeader>
          <EmptyTitle>{isStreaming ? 'Waiting for columns' : 'No rows returned'}</EmptyTitle>
          <EmptyDescription>
            {isStreaming
              ? 'Column metadata will appear when the stream begins.'
              : 'The query completed successfully but did not return any rows.'}
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    )
  }

  if (result.rowCount === 0 && result.rows.length === 0) {
    return (
      <Empty aria-label="No query results">
        <EmptyHeader>
          <EmptyTitle>{isStreaming ? 'Waiting for rows' : 'No rows returned'}</EmptyTitle>
          <EmptyDescription>
            {isStreaming
              ? 'Rows will appear here as they arrive from the coordinator.'
              : 'The query completed successfully but did not return any rows.'}
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    )
  }

  const visibleRowCount = result.rows.length

  const virtualRows = rowVirtualizer.getVirtualItems()
  const paddingTop = virtualRows.length > 0 ? virtualRows[0]?.start ?? 0 : 0
  const paddingBottom =
    virtualRows.length > 0 ? rowVirtualizer.getTotalSize() - (virtualRows[virtualRows.length - 1]?.end ?? 0) : 0

  return (
    <section aria-label="Query results table" className="space-y-3 p-4 sm:p-5">
      <p className="text-sm text-muted-foreground">
        Showing {visibleRowCount.toLocaleString()} row{visibleRowCount === 1 ? '' : 's'}
        {isStreaming ? ' so far while streaming.' : result.rowCount !== visibleRowCount ? ` of ${result.rowCount.toLocaleString()} reported.` : '.'}
        {shouldVirtualize ? ' Virtualized scrolling is enabled for performance.' : ''}
      </p>

      <div
        ref={parentRef}
        className={cn('overflow-hidden rounded-xl border border-border/60', shouldVirtualize && 'max-h-[480px] overflow-auto')}
        style={shouldVirtualize ? { maxHeight: `${VIRTUAL_VIEWPORT_HEIGHT_PX}px` } : undefined}
      >
        <Table aria-label={`Query results with ${result.columns.length} columns`}>
          <TableHeader className={shouldVirtualize ? 'sticky top-0 z-10 bg-muted/40 backdrop-blur-sm' : undefined}>
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
            {shouldVirtualize ? (
              <>
                {paddingTop > 0 ? (
                  <TableRow aria-hidden="true">
                    <TableCell colSpan={result.columns.length} style={{ height: `${paddingTop}px`, padding: 0 }} />
                  </TableRow>
                ) : null}
                {virtualRows.map((virtualRow) => {
                  const row = tableRows[virtualRow.index]
                  if (!row) {
                    return null
                  }

                  return (
                    <TableRow key={row.id} data-index={virtualRow.index}>
                      {row.getVisibleCells().map((cell) => (
                        <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
                      ))}
                    </TableRow>
                  )
                })}
                {paddingBottom > 0 ? (
                  <TableRow aria-hidden="true">
                    <TableCell colSpan={result.columns.length} style={{ height: `${paddingBottom}px`, padding: 0 }} />
                  </TableRow>
                ) : null}
              </>
            ) : (
              tableRows.map((row) => (
                <TableRow key={row.id}>
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </section>
  )
}
