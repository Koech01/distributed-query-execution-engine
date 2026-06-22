import { ArrowLeft, DatabaseZap, RefreshCw, SearchX } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { ErrorAlert } from '@/components/ui/error-alert'
import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { useQueryPoll } from '@/hooks/use-query-poll'
import { AppError, NotFoundError } from '@/lib/errors'

import { AsyncStatusBanner } from './AsyncStatusBanner'
import { DegradationBanner } from './DegradationBanner'
import { QueryMetadataBar } from './QueryMetadataBar'
import { QueryResultPageSkeleton } from './QueryResultPageSkeleton'
import { QueryResultTable } from './QueryResultTable'

export function QueryResultPage() {
  const { queryId } = useParams<{ queryId: string }>()
  const poll = useQueryPoll({ queryId, enabled: Boolean(queryId) })

  if (!queryId) {
    return <MissingQueryIdState />
  }

  const isInitialLoading = poll.phase === 'idle' || (poll.phase === 'running' && !poll.status)
  const isAmbiguousResult = poll.error instanceof AppError && poll.error.code === 'query_result_ambiguous'

  if (isInitialLoading) {
    return (
      <main className="space-y-8">
        <QueryResultPageSkeleton />
      </main>
    )
  }

  return (
    <main aria-labelledby="query-result-title" className="space-y-8">
      <PageHeader
        title="Query Result"
        titleId="query-result-title"
        description="Inspect an asynchronous query by ID, with status polling and terminal result loading handled through the API client."
        badge="Async result"
        actions={
          <Button asChild variant="outline">
            <Link to="/query">
              <ArrowLeft className="size-4" aria-hidden="true" />
              Back to console
            </Link>
          </Button>
        }
      />

      <AsyncStatusBanner
        queryId={queryId}
        phase={poll.phase}
        status={poll.status?.status}
        message={poll.status?.message}
        elapsedMs={poll.elapsedMs}
        error={poll.error}
        onCancel={poll.cancel}
      />

      {poll.error && poll.phase === 'error' && !(poll.error instanceof NotFoundError) && !isAmbiguousResult ? (
        <ErrorAlert error={poll.error} title="Could not load query result" />
      ) : null}

      {poll.result ? (
        <section aria-labelledby="async-query-results-heading" className="animate-fade-in-up space-y-5">
          <div className="space-y-1">
            <h2 id="async-query-results-heading" className="text-xl font-semibold tracking-tight">
              Result snapshot
            </h2>
            <p className="text-sm text-muted-foreground">
              Result fetches treat both complete and partial content responses as terminal successes.
            </p>
          </div>
          <DegradationBanner result={poll.result} />
          <QueryMetadataBar result={poll.result} />
          <div className="surface-panel overflow-hidden">
            <QueryResultTable result={poll.result} />
          </div>
        </section>
      ) : null}

      {poll.phase === 'cancelled' ? (
        <PageSection title="Polling stopped" icon={RefreshCw}>
          <Empty aria-label="Polling cancelled">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <RefreshCw className="size-5" aria-hidden="true" />
              </EmptyMedia>
              <EmptyTitle>Polling has been cancelled</EmptyTitle>
              <EmptyDescription>
                Return to the query console to submit a new query, or refresh this route to start status checks for this query again.
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        </PageSection>
      ) : null}
    </main>
  )
}

function MissingQueryIdState() {
  return (
    <main aria-labelledby="missing-query-id-title" className="space-y-8">
      <PageHeader
        title="Query Result"
        titleId="missing-query-id-title"
        description="A query ID is required to load an async result."
        badge="Missing query ID"
      />
      <Empty aria-label="Missing query ID">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <SearchX className="size-5" aria-hidden="true" />
          </EmptyMedia>
          <EmptyTitle>No query selected</EmptyTitle>
          <EmptyDescription>Open a result route with a query ID, or start a new async query from the console.</EmptyDescription>
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
    </main>
  )
}
