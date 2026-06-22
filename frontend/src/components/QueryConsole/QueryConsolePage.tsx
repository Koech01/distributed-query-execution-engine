import { Braces, Play, RotateCcw, Settings2, Terminal } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useLocation } from 'react-router-dom'

import type { FailurePolicy, QueryParameterDto, QueryPlanDetails, QueryResult } from '@/components/types'
import { ErrorAlert } from '@/components/ui/error-alert'
import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { queryApi } from '@/lib/api'
import { captureError } from '@/lib/observability'
import { useLocalQueryHistory } from '@/hooks/use-local-query-history'
import { usePreferences } from '@/hooks/use-preferences'
import { useQueryExecutionScroll } from '@/hooks/use-query-execution-scroll'
import { useQueryPoll } from '@/hooks/use-query-poll'
import { useQueryStream } from '@/hooks/use-query-stream'

import { AsyncStatusBanner } from './AsyncStatusBanner'
import { DegradationBanner } from './DegradationBanner'
import { ParameterEditor } from './ParameterEditor'
import { resolveStandaloneExecutionError } from './resolve-execution-error'
import { QueryConsolePageSkeleton } from './QueryConsolePageSkeleton'
import { QueryMetadataBar } from './QueryMetadataBar'
import { QueryOptionsPanel } from './QueryOptionsPanel'
import { QueryPlanPanel } from './QueryPlanPanel'
import { QueryResultTable } from './QueryResultTable'
import { SqlEditor } from './SqlEditor'
import { StreamingStatusBanner } from './StreamingStatusBanner'
import { isQueryResult, validateQueryForm, type QueryFormErrors } from './validate-query-form'

const DEFAULT_SQL = 'SELECT TOP 10 * FROM Orders'

interface QueryConsoleLocationState {
  sql?: string
  fromHistory?: boolean
}

function getInitialSql(locationState: unknown): string {
  if (
    typeof locationState === 'object' &&
    locationState !== null &&
    'sql' in locationState &&
    typeof (locationState as QueryConsoleLocationState).sql === 'string'
  ) {
    return (locationState as QueryConsoleLocationState).sql ?? DEFAULT_SQL
  }

  return DEFAULT_SQL
}

export function QueryConsolePage() {
  const location = useLocation()
  const initialSql = getInitialSql(location.state)
  const openedFromHistory =
    typeof location.state === 'object' &&
    location.state !== null &&
    'fromHistory' in location.state &&
    (location.state as QueryConsoleLocationState).fromHistory === true
  const { addEntry: addHistoryEntry } = useLocalQueryHistory()
  const { preferences, isLoading: isPreferencesLoading } = usePreferences()
  const preferencesApplied = useRef(false)
  const [sql, setSql] = useState(initialSql)
  const [parameters, setParameters] = useState<QueryParameterDto[]>([])
  const [timeoutSeconds, setTimeoutSeconds] = useState(30)
  const [failurePolicy, setFailurePolicy] = useState<FailurePolicy>('BestEffort')
  const [asyncEnabled, setAsyncEnabled] = useState(false)
  const [streamEnabled, setStreamEnabled] = useState(false)
  const [maxNodes, setMaxNodes] = useState<number | undefined>(undefined)
  const [showAdvanced, setShowAdvanced] = useState(false)
  const [isExecuting, setIsExecuting] = useState(false)
  const [result, setResult] = useState<QueryResult | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [formErrors, setFormErrors] = useState<QueryFormErrors>({})
  const [asyncQueryId, setAsyncQueryId] = useState<string | null>(null)
  const [pendingHistorySql, setPendingHistorySql] = useState<string | null>(null)
  const [plan, setPlan] = useState<QueryPlanDetails | null>(null)
  const [planError, setPlanError] = useState<unknown>(null)
  const [isInspectingPlan, setIsInspectingPlan] = useState(false)
  const poll = useQueryPoll({ queryId: asyncQueryId, enabled: Boolean(asyncQueryId) })
  const stream = useQueryStream()
  const { feedbackRef, resultsRef, streamingResultsRef, scrollToTarget, resetScrollMemory } = useQueryExecutionScroll()
  const recordedAsyncResultId = useRef<string | null>(null)
  const isPollingActive = poll.phase === 'running' || poll.phase === 'paused' || poll.phase === 'fetching-result'
  const isStreamingActive = stream.phase === 'streaming'
  const isBusy = isExecuting || isPollingActive || isStreamingActive
  const visibleResult = poll.result ?? stream.result ?? result
  const standaloneExecutionError = resolveStandaloneExecutionError({
    syncError: error,
    pollError: poll.error,
    pollPhase: poll.phase,
    hasAsyncQuery: Boolean(asyncQueryId),
    streamError: stream.error,
    streamPhase: stream.phase,
  })
  const hasExecutionFeedback =
    Boolean(formErrors.form) ||
    Boolean(standaloneExecutionError) ||
    Boolean(asyncQueryId) ||
    stream.phase !== 'idle'
  const streamingPreviewResult =
    stream.phase === 'streaming' || stream.phase === 'cancelled'
      ? {
          columns: stream.columns,
          rows: stream.rows,
          rowCount: stream.rowCount,
        }
      : null
  const hasResultsPanel = Boolean(visibleResult) || Boolean(streamingPreviewResult)

  useEffect(() => {
    if (isPreferencesLoading || preferencesApplied.current) {
      return
    }

    preferencesApplied.current = true
    setTimeoutSeconds(preferences.defaultTimeoutSeconds)
    setFailurePolicy(preferences.defaultFailurePolicy)
    setAsyncEnabled(preferences.defaultAsync)
  }, [isPreferencesLoading, preferences.defaultAsync, preferences.defaultFailurePolicy, preferences.defaultTimeoutSeconds])

  useEffect(() => {
    if (asyncEnabled && streamEnabled) {
      setStreamEnabled(false)
    }
  }, [asyncEnabled, streamEnabled])

  useEffect(() => {
    if (!poll.result || !pendingHistorySql || recordedAsyncResultId.current === poll.result.queryId) {
      return
    }

    recordedAsyncResultId.current = poll.result.queryId
    void addHistoryEntry({
      result: poll.result,
      sql: pendingHistorySql,
      async: true,
      saveSql: preferences.saveSqlInHistory,
    })
  }, [addHistoryEntry, pendingHistorySql, poll.result, preferences.saveSqlInHistory])

  useEffect(() => {
    if (!visibleResult) {
      return
    }

    scrollToTarget('results', visibleResult.queryId)
  }, [scrollToTarget, visibleResult])

  useEffect(() => {
    if (!streamingPreviewResult || stream.phase !== 'streaming') {
      return
    }

    scrollToTarget('streaming-results', stream.metadata?.queryId ?? 'streaming')
  }, [scrollToTarget, stream.metadata?.queryId, stream.phase, streamingPreviewResult])

  useEffect(() => {
    if (!asyncQueryId || visibleResult) {
      return
    }

    scrollToTarget('feedback', asyncQueryId)
  }, [asyncQueryId, scrollToTarget, visibleResult])

  useEffect(() => {
    if (!standaloneExecutionError) {
      return
    }

    scrollToTarget('feedback', standaloneExecutionError instanceof Error ? standaloneExecutionError.message : 'execution-error')
  }, [scrollToTarget, standaloneExecutionError])

  const handleExecute = async () => {
    resetScrollMemory()
    setError(null)
    setResult(null)
    setAsyncQueryId(null)
    setPendingHistorySql(null)
    recordedAsyncResultId.current = null
    stream.reset()

    const validation = validateQueryForm({
      sql,
      parameters,
      timeoutSeconds,
      failurePolicy,
      async: asyncEnabled,
      maxNodes,
    })

    if (!validation.success) {
      setFormErrors(validation.errors)
      scrollToTarget('feedback', 'validation')
      return
    }

    setFormErrors({})
    setIsExecuting(true)

    try {
      if (streamEnabled && !asyncEnabled) {
        const streamedResult = await stream.start(validation.data)
        if (streamedResult) {
          setResult(streamedResult)
          void addHistoryEntry({
            result: streamedResult,
            sql: validation.data.sql,
            async: false,
            saveSql: preferences.saveSqlInHistory,
          })
        }
        return
      }

      const response = await queryApi.submit(validation.data)

      if (!isQueryResult(response)) {
        setPendingHistorySql(validation.data.sql)
        setAsyncQueryId(response.queryId)
        return
      }

      setResult(response)
      void addHistoryEntry({
        result: response,
        sql: validation.data.sql,
        async: false,
        saveSql: preferences.saveSqlInHistory,
      })
    } catch (submitError) {
      captureError(submitError, { route: '/query' })
      if (streamEnabled && !asyncEnabled) {
        return
      }
      setError(submitError)
    } finally {
      setIsExecuting(false)
    }
  }

  const handleClear = () => {
    resetScrollMemory()
    setSql('')
    setParameters([])
    setTimeoutSeconds(preferences.defaultTimeoutSeconds)
    setFailurePolicy(preferences.defaultFailurePolicy)
    setAsyncEnabled(preferences.defaultAsync)
    setStreamEnabled(false)
    setMaxNodes(undefined)
    setShowAdvanced(false)
    setResult(null)
    setError(null)
    setFormErrors({})
    setAsyncQueryId(null)
    setPendingHistorySql(null)
    recordedAsyncResultId.current = null
    stream.reset()
    setPlan(null)
    setPlanError(null)
  }

  const handleInspectPlan = async () => {
    setPlan(null)
    setPlanError(null)

    const validation = validateQueryForm({
      sql,
      parameters,
      timeoutSeconds,
      failurePolicy,
      async: false,
      maxNodes,
    })

    if (!validation.success) {
      setFormErrors(validation.errors)
      return
    }

    setFormErrors({})
    setIsInspectingPlan(true)

    try {
      const nextPlan = await queryApi.plan(validation.data)
      setPlan(nextPlan)
    } catch (inspectError) {
      captureError(inspectError, { route: '/query' })
      setPlanError(inspectError)
    } finally {
      setIsInspectingPlan(false)
    }
  }

  if (isPreferencesLoading) {
    return (
      <main aria-labelledby="query-console-title" className="space-y-8">
        <QueryConsolePageSkeleton />
      </main>
    )
  }

  return (
    <main aria-labelledby="query-console-title" className="space-y-8">
      <PageHeader
        title="Query Console"
        titleId="query-console-title"
        description="Compose parameterized T-SQL, execute synchronously or asynchronously, and inspect distributed results with shard-level metadata."
        badge={
          openedFromHistory
            ? 'History re-run'
            : streamEnabled
              ? 'Streaming'
              : asyncEnabled
                ? 'Async ready'
                : 'Sync execution'
        }
      />

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_340px] xl:items-start">
        <div className="min-w-0 space-y-6">
          <PageSection title="SQL query" titleId="sql-editor-heading" icon={Terminal}>
            <SqlEditor labelledBy="sql-editor-heading" value={sql} onChange={setSql} error={formErrors.sql} disabled={isBusy} />
          </PageSection>

          <PageSection
            title="Parameters"
            titleId="parameter-editor-heading"
            icon={Braces}
            description="Bind named placeholders to typed values before execution."
          >
            <ParameterEditor
              parameters={parameters}
              onChange={setParameters}
              errors={formErrors.parameters}
              disabled={isBusy}
            />
          </PageSection>

          <section
            ref={feedbackRef}
            aria-label="Query execution feedback"
            className="surface-panel sticky bottom-4 z-10 overflow-hidden shadow-lg"
          >
            {hasExecutionFeedback ? (
              <div className="animate-fade-in-up space-y-3 border-b border-border/60 bg-muted p-4">
                {formErrors.form ? (
                  <p
                    className="rounded-lg border border-destructive/50 bg-card px-4 py-3 text-sm text-destructive dark:text-destructive"
                    role="alert"
                    aria-live="polite"
                  >
                    {formErrors.form}
                  </p>
                ) : null}

                {standaloneExecutionError ? (
                  <ErrorAlert error={standaloneExecutionError} title="Query execution failed" />
                ) : null}

                {asyncQueryId ? (
                  <AsyncStatusBanner
                    queryId={asyncQueryId}
                    phase={poll.phase}
                    status={poll.status?.status}
                    message={poll.status?.message}
                    elapsedMs={poll.elapsedMs}
                    error={poll.error}
                    onCancel={poll.cancel}
                  />
                ) : null}

                {stream.phase !== 'idle' ? (
                  <StreamingStatusBanner
                    phase={stream.phase}
                    queryId={stream.metadata?.queryId}
                    streamMode={stream.streamMode}
                    rowCount={stream.rowCount}
                    totalShards={stream.metadata?.totalShards}
                    error={stream.error}
                    onCancel={stream.cancel}
                  />
                ) : null}
              </div>
            ) : null}

            <div className="flex flex-wrap items-center justify-between gap-3 bg-card p-4">
              <p className="text-sm text-muted-foreground">
                {isExecuting
                  ? streamEnabled
                    ? 'Opening result stream from the coordinator...'
                    : asyncEnabled
                      ? 'Submitting async query to the coordinator...'
                      : 'Executing query across the cluster...'
                  : isStreamingActive
                    ? 'Receiving rows from the streaming coordinator response.'
                    : isPollingActive
                      ? 'Polling async query status with bounded backoff.'
                      : 'Ready to execute against the distributed engine.'}
              </p>
              <div className="flex flex-wrap gap-3">
                <Button
                  type="button"
                  onClick={() => void handleExecute()}
                  disabled={isBusy}
                  aria-busy={isBusy}
                  className="gap-2"
                >
                  <Play className="size-4" aria-hidden="true" />
                  {isBusy ? (streamEnabled ? 'Streaming...' : asyncEnabled ? 'Running async...' : 'Executing...') : 'Execute'}
                </Button>
                <Button type="button" variant="outline" onClick={handleClear} disabled={isExecuting} className="gap-2">
                  <RotateCcw className="size-4" aria-hidden="true" />
                  Clear
                </Button>
              </div>
            </div>
          </section>
        </div>

        <aside
          className={cn(
            'min-w-0 xl:col-start-2 xl:row-start-1',
            !hasResultsPanel && 'xl:sticky xl:top-24 xl:self-start',
          )}
        >
          <PageSection title="Query options" titleId="query-options-heading" icon={Settings2}>
            <QueryOptionsPanel
              timeoutSeconds={timeoutSeconds}
              onTimeoutSecondsChange={setTimeoutSeconds}
              failurePolicy={failurePolicy}
              onFailurePolicyChange={setFailurePolicy}
              asyncEnabled={asyncEnabled}
              onAsyncEnabledChange={setAsyncEnabled}
              streamEnabled={streamEnabled}
              onStreamEnabledChange={setStreamEnabled}
              maxNodes={maxNodes}
              onMaxNodesChange={setMaxNodes}
              showAdvanced={showAdvanced}
              onShowAdvancedChange={setShowAdvanced}
              timeoutError={formErrors.timeoutSeconds}
              maxNodesError={formErrors.maxNodes}
              disabled={isBusy}
            />
          </PageSection>
        </aside>
      </div>

      {streamingPreviewResult ? (
        <section
          ref={streamingResultsRef}
          aria-labelledby="streaming-results-heading"
          className="animate-fade-in-up scroll-mt-24 space-y-5"
        >
          <div className="space-y-1">
            <h2
              id="streaming-results-heading"
              data-scroll-focus="true"
              className="text-xl font-semibold tracking-tight"
            >
              Live results
            </h2>
            <p className="text-sm text-muted-foreground">Rows append as the coordinator streams them from worker shards.</p>
          </div>
          <div className="surface-panel overflow-hidden">
            <QueryResultTable result={streamingPreviewResult} isStreaming={isStreamingActive} />
          </div>
        </section>
      ) : null}

      {visibleResult ? (
        <section
          ref={resultsRef}
          aria-labelledby="query-results-heading"
          className="animate-fade-in-up scroll-mt-24 space-y-5"
        >
          <div className="space-y-1">
            <h2 id="query-results-heading" data-scroll-focus="true" className="text-xl font-semibold tracking-tight">
              Results
            </h2>
            <p className="text-sm text-muted-foreground">
              Review execution metadata, degradation signals, and returned rows.
            </p>
          </div>
          <DegradationBanner result={visibleResult} />
          <QueryMetadataBar result={visibleResult} />
          <div className="surface-panel overflow-hidden">
            <QueryResultTable result={visibleResult} />
          </div>
        </section>
      ) : null}

      <QueryPlanPanel
        plan={plan}
        isLoading={isInspectingPlan}
        error={planError}
        onInspect={() => void handleInspectPlan()}
        onDismiss={() => {
          setPlan(null)
          setPlanError(null)
        }}
        disabled={isBusy}
      />
    </main>
  )
}
