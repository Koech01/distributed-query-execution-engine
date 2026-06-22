import { useCallback, useEffect, useRef, useState } from 'react'

import type { QueryResult, QueryStatusResponse } from '@/components/types'
import { AppError, NotFoundError, TimeoutError } from '@/lib/errors'
import { queryApi } from '@/lib/api'

export const QUERY_POLL_INTERVALS_MS = [1_000, 2_000, 3_000, 5_000] as const
export const DEFAULT_QUERY_POLL_TIMEOUT_MS = 120_000

const AMBIGUOUS_RESULT_MESSAGE =
  'The result is not available. It may still be processing, have expired, or refer to an unknown query.'

export type QueryPollPhase =
  | 'idle'
  | 'running'
  | 'paused'
  | 'fetching-result'
  | 'completed'
  | 'cancelled'
  | 'timeout'
  | 'error'

interface UseQueryPollOptions {
  queryId: string | null | undefined
  enabled?: boolean
  timeoutMs?: number
}

interface UseQueryPollResult {
  queryId: string | null
  phase: QueryPollPhase
  status: QueryStatusResponse | null
  result: QueryResult | null
  error: unknown
  elapsedMs: number
  isPolling: boolean
  isPaused: boolean
  cancel: () => void
}

export function getQueryPollDelayMs(attempt: number): number {
  const normalizedAttempt = Math.max(0, Math.floor(attempt))
  return QUERY_POLL_INTERVALS_MS[Math.min(normalizedAttempt, QUERY_POLL_INTERVALS_MS.length - 1)]
}

export function createAmbiguousQueryResultError(): AppError {
  return new AppError({
    code: 'query_result_ambiguous',
    status: 404,
    message: AMBIGUOUS_RESULT_MESSAGE,
  })
}

export function useQueryPoll({
  queryId,
  enabled = true,
  timeoutMs = DEFAULT_QUERY_POLL_TIMEOUT_MS,
}: UseQueryPollOptions): UseQueryPollResult {
  const [phase, setPhase] = useState<QueryPollPhase>('idle')
  const [status, setStatus] = useState<QueryStatusResponse | null>(null)
  const [result, setResult] = useState<QueryResult | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [elapsedMs, setElapsedMs] = useState(0)
  const [stateQueryId, setStateQueryId] = useState<string | null>(null)
  const cancelRef = useRef<() => void>(() => undefined)

  const cancel = useCallback(() => {
    cancelRef.current()
  }, [])

  useEffect(() => {
    if (!enabled || !queryId) {
      cancelRef.current = () => undefined
      return
    }

    let cancelled = false
    let pollTimer: ReturnType<typeof window.setTimeout> | undefined
    let elapsedTimer: ReturnType<typeof window.setInterval> | undefined
    let controller: AbortController | undefined
    let attempt = 0
    let activeElapsedMs = 0
    let visibleStartedAt = Date.now()

    const isHidden = () => typeof document !== 'undefined' && document.hidden
    const currentElapsedMs = () => activeElapsedMs + (isHidden() ? 0 : Date.now() - visibleStartedAt)
    const updateElapsed = () => setElapsedMs(currentElapsedMs())

    const clearPollTimer = () => {
      if (pollTimer !== undefined) {
        window.clearTimeout(pollTimer)
        pollTimer = undefined
      }
    }

    const cleanup = () => {
      clearPollTimer()
      if (elapsedTimer !== undefined) {
        window.clearInterval(elapsedTimer)
        elapsedTimer = undefined
      }
      controller?.abort()
      controller = undefined
    }

    const failWithTimeout = () => {
      cleanup()
      setStateQueryId(queryId)
      setError(
        new TimeoutError({
          code: 'query_poll_timeout',
          status: 408,
          message: 'Polling stopped because the query did not complete before the client timeout.',
        }),
      )
      setPhase('timeout')
      updateElapsed()
    }

    const schedulePoll = (delayMs: number) => {
      clearPollTimer()
      pollTimer = window.setTimeout(() => {
        void poll()
      }, delayMs)
    }

    const fetchCompletedResult = async () => {
      setStateQueryId(queryId)
      setPhase('fetching-result')
      controller = new AbortController()

      try {
        const nextResult = await queryApi.getResult(queryId, controller.signal)
        if (cancelled) {
          return
        }

        setStateQueryId(queryId)
        setResult(nextResult)
        setError(null)
        setPhase('completed')
        updateElapsed()
        cleanup()
      } catch (resultError) {
        if (cancelled || isAbortError(resultError)) {
          return
        }

        setStateQueryId(queryId)
        setError(resultError instanceof NotFoundError ? createAmbiguousQueryResultError() : resultError)
        setPhase('error')
        updateElapsed()
        cleanup()
      }
    }

    const poll = async () => {
      if (cancelled) {
        return
      }

      if (isHidden()) {
        setPhase('paused')
        return
      }

      if (currentElapsedMs() >= timeoutMs) {
        failWithTimeout()
        return
      }

      setStateQueryId(queryId)
      setPhase('running')
      controller = new AbortController()

      try {
        const nextStatus = await queryApi.getStatus(queryId, controller.signal)
        if (cancelled) {
          return
        }

        setStateQueryId(queryId)
        setStatus(nextStatus)
        setError(null)
        updateElapsed()

        if (nextStatus.status === 'completed') {
          await fetchCompletedResult()
          return
        }

        const delayMs = getQueryPollDelayMs(attempt)
        attempt += 1
        schedulePoll(delayMs)
      } catch (statusError) {
        if (cancelled || isAbortError(statusError)) {
          return
        }

        setStateQueryId(queryId)
        setError(statusError)
        setPhase('error')
        updateElapsed()
        cleanup()
      }
    }

    const handleVisibilityChange = () => {
      if (cancelled) {
        return
      }

      if (isHidden()) {
        activeElapsedMs = currentElapsedMs()
        clearPollTimer()
        controller?.abort()
        setStateQueryId(queryId)
        setPhase('paused')
        updateElapsed()
        return
      }

      visibleStartedAt = Date.now()
      setStateQueryId(queryId)
      setPhase('running')
      schedulePoll(0)
      updateElapsed()
    }

    cancelRef.current = () => {
      if (cancelled) {
        return
      }

      cancelled = true
      cleanup()
      setStateQueryId(queryId)
      setPhase('cancelled')
      updateElapsed()
    }

    document.addEventListener('visibilitychange', handleVisibilityChange)
    elapsedTimer = window.setInterval(updateElapsed, 1_000)
    schedulePoll(0)

    return () => {
      cancelled = true
      cleanup()
      document.removeEventListener('visibilitychange', handleVisibilityChange)
      cancelRef.current = () => undefined
    }
  }, [enabled, queryId, timeoutMs])

  const hasActiveQuery = Boolean(enabled && queryId)
  const hasCurrentState = hasActiveQuery && stateQueryId === queryId
  const scopedPhase = hasActiveQuery ? (hasCurrentState ? phase : 'running') : 'idle'

  return {
    queryId: queryId ?? null,
    phase: scopedPhase,
    status: hasCurrentState && status?.queryId === queryId ? status : null,
    result: hasCurrentState && result?.queryId === queryId ? result : null,
    error: hasCurrentState ? error : null,
    elapsedMs: hasCurrentState ? elapsedMs : 0,
    isPolling: scopedPhase === 'running' || scopedPhase === 'fetching-result',
    isPaused: scopedPhase === 'paused',
    cancel,
  }
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}
