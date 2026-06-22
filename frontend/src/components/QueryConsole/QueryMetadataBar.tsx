import { Clock3, Database, ExternalLink, Hash, Rows3 } from 'lucide-react'

import { CopyButton } from '@/components/ui/copy-button'
import { StatTile } from '@/components/ui/stat-tile'
import type { QueryResult } from '@/components/types'
import { formatExecutionMs, formatShardStats } from '@/lib/utils'

interface QueryMetadataBarProps {
  result: QueryResult
}

function getJaegerUrl(): string | undefined {
  const value = import.meta.env.VITE_JAEGER_URL
  return typeof value === 'string' && value.length > 0 ? value : undefined
}

export function QueryMetadataBar({ result }: QueryMetadataBarProps) {
  const jaegerUrl = getJaegerUrl()

  return (
    <section aria-label="Query execution metadata" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <StatTile label="Query ID" icon={Hash}>
        <div className="flex flex-wrap items-center gap-2">
          <code className="rounded-md border border-border/60 bg-muted/40 px-2 py-1 font-mono text-xs">
            {result.queryId}
          </code>
          <CopyButton value={result.queryId} label="Copy query ID" />
        </div>
      </StatTile>

      <StatTile label="Execution time" icon={Clock3}>
        <p className="text-lg font-semibold tracking-tight">{formatExecutionMs(result.executionMs)}</p>
      </StatTile>

      <StatTile label="Shard coverage" icon={Database}>
        <p className="text-lg font-semibold tracking-tight">
          {formatShardStats(result.successfulShards, result.totalShards)}
        </p>
      </StatTile>

      <StatTile
        label="Rows returned"
        icon={Rows3}
        hint="Cache metadata is informational only; do not rely on it for cache-hit accuracy."
      >
        <p className="text-lg font-semibold tracking-tight">
          {result.rowCount.toLocaleString()} row{result.rowCount === 1 ? '' : 's'}
        </p>
      </StatTile>

      {jaegerUrl ? (
        <div className="sm:col-span-2 xl:col-span-4">
          <a
            href={jaegerUrl}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-2 text-sm font-medium text-foreground underline-offset-4 transition-colors hover:text-muted-foreground hover:underline"
          >
            Open Jaeger trace explorer
            <ExternalLink className="size-4" aria-hidden="true" />
          </a>
        </div>
      ) : null}
    </section>
  )
}
