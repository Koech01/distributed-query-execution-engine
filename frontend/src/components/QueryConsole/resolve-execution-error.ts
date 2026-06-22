import type { QueryPollPhase } from '@/hooks/use-query-poll'
import type { QueryStreamPhase } from '@/hooks/use-query-stream'

/**
 * Returns an error for the standalone ErrorAlert, or null when a status banner
 * already surfaces the same failure (async poll or streaming).
 */
export function resolveStandaloneExecutionError({
  syncError,
  pollError,
  pollPhase,
  hasAsyncQuery,
  streamError,
  streamPhase,
}: {
  syncError: unknown
  pollError: unknown
  pollPhase: QueryPollPhase
  hasAsyncQuery: boolean
  streamError: unknown
  streamPhase: QueryStreamPhase
}): unknown {
  if (syncError) {
    return syncError
  }

  if (streamPhase === 'error') {
    return null
  }

  if (hasAsyncQuery && (pollPhase === 'error' || pollPhase === 'timeout')) {
    return null
  }

  return pollError ?? streamError ?? null
}
