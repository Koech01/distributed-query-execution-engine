import { describe, expect, it } from 'vitest'

import { resolveStandaloneExecutionError } from '@/components/QueryConsole/resolve-execution-error'

describe('resolveStandaloneExecutionError', () => {
  it('prefers sync submit errors', () => {
    expect(
      resolveStandaloneExecutionError({
        syncError: new Error('Sync failed'),
        pollError: new Error('Poll failed'),
        pollPhase: 'error',
        hasAsyncQuery: true,
        streamError: new Error('Stream failed'),
        streamPhase: 'error',
      }),
    ).toEqual(new Error('Sync failed'))
  })

  it('suppresses duplicate alerts when streaming fails', () => {
    expect(
      resolveStandaloneExecutionError({
        syncError: null,
        pollError: null,
        pollPhase: 'idle',
        hasAsyncQuery: false,
        streamError: new Error('Request body is not valid JSON.'),
        streamPhase: 'error',
      }),
    ).toBeNull()
  })

  it('suppresses duplicate alerts when async polling fails', () => {
    expect(
      resolveStandaloneExecutionError({
        syncError: null,
        pollError: new Error('Status check failed'),
        pollPhase: 'error',
        hasAsyncQuery: true,
        streamError: null,
        streamPhase: 'idle',
      }),
    ).toBeNull()
  })

  it('falls back to poll or stream errors when no banner owns them', () => {
    expect(
      resolveStandaloneExecutionError({
        syncError: null,
        pollError: new Error('Unexpected poll error'),
        pollPhase: 'running',
        hasAsyncQuery: true,
        streamError: null,
        streamPhase: 'idle',
      }),
    ).toEqual(new Error('Unexpected poll error'))
  })
})
