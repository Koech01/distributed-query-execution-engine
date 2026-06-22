import { LoaderCircle, Radio, Square } from 'lucide-react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import type { QueryStreamMode } from '@/components/types'

import type { QueryStreamPhase } from '@/hooks/use-query-stream'

interface StreamingStatusBannerProps {
  phase: QueryStreamPhase
  queryId?: string | null
  streamMode?: QueryStreamMode | null
  rowCount: number
  totalShards?: number | null
  error?: unknown
  onCancel?: () => void
}

function formatStreamMode(mode: QueryStreamMode | null | undefined): string {
  switch (mode) {
    case 'incremental':
      return 'Incremental merge'
    case 'ordered':
      return 'Ordered merge'
    case 'buffered':
      return 'Buffered merge'
    default:
      return 'Streaming'
  }
}

export function StreamingStatusBanner({
  phase,
  queryId,
  streamMode,
  rowCount,
  totalShards,
  error,
  onCancel,
}: StreamingStatusBannerProps) {
  if (phase === 'idle') {
    return null
  }

  if (phase === 'cancelled') {
    return (
      <Alert variant="warning">
        <Square className="size-4" aria-hidden="true" />
        <AlertTitle>Stream cancelled</AlertTitle>
        <AlertDescription>
          Result streaming was cancelled. {rowCount > 0 ? `${rowCount.toLocaleString()} row(s) were received before cancellation.` : 'No rows were received.'}
        </AlertDescription>
      </Alert>
    )
  }

  if (phase === 'error') {
    return (
      <Alert variant="destructive">
        <Radio className="size-4" aria-hidden="true" />
        <AlertTitle>Streaming failed</AlertTitle>
        <AlertDescription>
          {error instanceof Error ? error.message : 'The result stream ended unexpectedly.'}
        </AlertDescription>
      </Alert>
    )
  }

  const isStreaming = phase === 'streaming'
  const title = isStreaming ? 'Streaming results' : 'Stream completed'
  const description = isStreaming
    ? 'Rows arrive incrementally from the coordinator while shards finish execution.'
    : 'All rows were streamed successfully from the coordinator.'

  return (
    <section
      aria-label="Streaming query status"
      role="status"
      aria-live="polite"
      className="surface-panel flex flex-col gap-4 border-border/60 bg-card p-5 sm:flex-row sm:items-center sm:justify-between"
    >
      <div className="flex items-start gap-3">
        {isStreaming ? (
          <LoaderCircle className="mt-0.5 size-5 shrink-0 animate-spin text-primary" aria-hidden="true" />
        ) : (
          <Radio className="mt-0.5 size-5 shrink-0 text-primary" aria-hidden="true" />
        )}
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-base font-semibold tracking-tight">{title}</h2>
            <Badge variant="secondary">{formatStreamMode(streamMode)}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">{description}</p>
          <dl className="grid gap-2 text-sm sm:grid-cols-3">
            {queryId ? (
              <div>
                <dt className="text-muted-foreground">Query ID</dt>
                <dd className="font-mono text-xs">{queryId}</dd>
              </div>
            ) : null}
            <div>
              <dt className="text-muted-foreground">Rows received</dt>
              <dd>{rowCount.toLocaleString()}</dd>
            </div>
            {totalShards != null ? (
              <div>
                <dt className="text-muted-foreground">Target shards</dt>
                <dd>{totalShards.toLocaleString()}</dd>
              </div>
            ) : null}
          </dl>
        </div>
      </div>

      {isStreaming && onCancel ? (
        <Button type="button" variant="outline" onClick={onCancel} className="gap-2 self-start sm:self-center">
          <Square className="size-4" aria-hidden="true" />
          Cancel stream
        </Button>
      ) : null}
    </section>
  )
}
