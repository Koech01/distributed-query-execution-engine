import { Ban, CheckCircle2, Clock3, Loader2, PauseCircle, RotateCcw, SearchX, XCircle } from 'lucide-react'

import type { QueryStatus } from '@/components/types'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { CopyButton } from '@/components/ui/copy-button'
import { getErrorMessage } from '@/lib/errors'
import { cn, formatExecutionMs } from '@/lib/utils'
import type { QueryPollPhase } from '@/hooks/use-query-poll'

interface AsyncStatusBannerProps {
  queryId: string
  phase: QueryPollPhase
  status?: QueryStatus | null
  message?: string | null
  elapsedMs: number
  error?: unknown
  onCancel?: () => void
  className?: string
}

const PHASE_COPY: Record<QueryPollPhase, { label: string; title: string; description: string }> = {
  idle: {
    label: 'Idle',
    title: 'Async query ready',
    description: 'Polling will begin after the query is accepted by the API.',
  },
  running: {
    label: 'Running',
    title: 'Query is running asynchronously',
    description: 'Status checks are using bounded backoff and will pause while this tab is hidden.',
  },
  paused: {
    label: 'Paused',
    title: 'Polling paused',
    description: 'This tab is hidden. Polling will resume automatically when you return.',
  },
  'fetching-result': {
    label: 'Fetching result',
    title: 'Query completed',
    description: 'The final result is being fetched from the query result endpoint.',
  },
  completed: {
    label: 'Completed',
    title: 'Async query completed',
    description: 'The terminal result has been loaded successfully.',
  },
  cancelled: {
    label: 'Cancelled',
    title: 'Polling cancelled',
    description: 'No more status checks will run for this query in this view.',
  },
  timeout: {
    label: 'Timed out',
    title: 'Polling timed out',
    description: 'The client stopped polling before the query reached a completed status.',
  },
  error: {
    label: 'Needs attention',
    title: 'Async query needs attention',
    description: 'The latest status or result request did not complete successfully.',
  },
}

const PHASE_ICONS = {
  idle: RotateCcw,
  running: Loader2,
  paused: PauseCircle,
  'fetching-result': Loader2,
  completed: CheckCircle2,
  cancelled: XCircle,
  timeout: Clock3,
  error: SearchX,
} satisfies Record<QueryPollPhase, typeof Loader2>

export function AsyncStatusBanner({
  queryId,
  phase,
  status,
  message,
  elapsedMs,
  error,
  onCancel,
  className,
}: AsyncStatusBannerProps) {
  const copy = PHASE_COPY[phase]
  const canCancel = phase === 'running' || phase === 'paused' || phase === 'fetching-result'
  const Icon = PHASE_ICONS[phase]

  return (
    <section
      role={phase === 'error' || phase === 'timeout' ? 'alert' : 'status'}
      aria-live="polite"
      aria-label="Async query status"
      className={cn(
        'surface-panel animate-fade-in-up overflow-hidden border-border/70 bg-card',
        (phase === 'error' || phase === 'timeout') && 'border-destructive/40',
        className,
      )}
    >
      <div className="flex flex-col gap-5 bg-muted p-5 sm:flex-row sm:items-start sm:justify-between sm:p-6">
        <div className="flex min-w-0 gap-4">
          <div
            className={cn(
              'flex size-11 shrink-0 items-center justify-center rounded-xl border border-border/60 bg-background shadow-xs',
              phase === 'running' && 'text-primary',
              phase === 'paused' && 'text-warning',
              (phase === 'error' || phase === 'timeout') && 'text-destructive',
              phase === 'completed' && 'text-emerald-600 dark:text-emerald-400',
            )}
            aria-hidden="true"
          >
            <Icon className={cn('size-5', (phase === 'running' || phase === 'fetching-result') && 'animate-spin')} />
          </div>
          <div className="min-w-0 space-y-3">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant={phase === 'error' || phase === 'timeout' ? 'destructive' : phase === 'paused' ? 'warning' : 'secondary'}>
                {copy.label}
              </Badge>
              {status ? <span className="text-sm text-muted-foreground">Backend status: {status}</span> : null}
            </div>
            <div className="space-y-1">
              <h2 className="text-base font-semibold tracking-tight">{copy.title}</h2>
              <p className="max-w-2xl text-sm leading-relaxed text-muted-foreground">
                {error ? getErrorMessage(error) : message || copy.description}
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
              <span>Elapsed active polling: {formatExecutionMs(elapsedMs)}</span>
              <span aria-hidden="true">/</span>
              <code className="rounded-md border border-border/60 bg-background/80 px-2 py-1 font-mono text-xs text-foreground">
                {queryId}
              </code>
              <CopyButton value={queryId} label="Copy async query ID" />
            </div>
          </div>
        </div>

        {canCancel && onCancel ? (
          <Button type="button" variant="outline" onClick={onCancel} className="shrink-0">
            <Ban className="size-4" aria-hidden="true" />
            Cancel polling
          </Button>
        ) : null}
      </div>
    </section>
  )
}
