import { useCallback, useEffect, useRef, useState } from 'react'

import type { QueryResult, QueryStreamMetadata, QueryStreamMode, SubmitQueryRequest } from '@/components/types'
import { queryApi } from '@/lib/api'
import { buildQueryResultFromStream } from '@/lib/query-stream'

export type QueryStreamPhase = 'idle' | 'streaming' | 'complete' | 'cancelled' | 'error'

export interface UseQueryStreamResult {
  phase: QueryStreamPhase
  metadata: QueryStreamMetadata | null
  streamMode: QueryStreamMode | null
  columns: string[]
  rows: string[][]
  rowCount: number
  result: QueryResult | null
  error: unknown
  start: (request: SubmitQueryRequest) => Promise<QueryResult | null>
  cancel: () => void
  reset: () => void
}

export function useQueryStream(): UseQueryStreamResult {
  const abortControllerRef = useRef<AbortController | null>(null)
  const [phase, setPhase] = useState<QueryStreamPhase>('idle')
  const [metadata, setMetadata] = useState<QueryStreamMetadata | null>(null)
  const [columns, setColumns] = useState<string[]>([])
  const [rows, setRows] = useState<string[][]>([])
  const [result, setResult] = useState<QueryResult | null>(null)
  const [error, setError] = useState<unknown>(null)

  const reset = useCallback(() => {
    abortControllerRef.current?.abort()
    abortControllerRef.current = null
    setPhase('idle')
    setMetadata(null)
    setColumns([])
    setRows([])
    setResult(null)
    setError(null)
  }, [])

  const cancel = useCallback(() => {
    abortControllerRef.current?.abort()
    abortControllerRef.current = null
    setPhase((currentPhase) => (currentPhase === 'streaming' ? 'cancelled' : currentPhase))
  }, [])

  const start = useCallback(async (request: SubmitQueryRequest): Promise<QueryResult | null> => {
    abortControllerRef.current?.abort()
    const controller = new AbortController()
    abortControllerRef.current = controller

    setPhase('streaming')
    setMetadata(null)
    setColumns([])
    setRows([])
    setResult(null)
    setError(null)

    let streamMetadata: QueryStreamMetadata | null = null
    let streamColumns: string[] = []
    const streamRows: string[][] = []

    try {
      for await (const event of queryApi.streamEvents(request, controller.signal)) {
        switch (event.kind) {
          case 'metadata':
            streamMetadata = event.data
            setMetadata(event.data)
            break
          case 'columns':
            streamColumns = event.data.columns
            setColumns(event.data.columns)
            break
          case 'row':
            streamRows.push(event.data.values)
            setRows((currentRows) => [...currentRows, event.data.values])
            break
          case 'complete': {
            if (!streamMetadata) {
              throw new Error('Streaming response ended before metadata was received.')
            }

            const finalResult = buildQueryResultFromStream({
              metadata: streamMetadata,
              columns: streamColumns,
              rows: streamRows,
              complete: event.data,
            })
            setResult(finalResult)
            setPhase('complete')
            abortControllerRef.current = null
            return finalResult
          }
          default:
            break
        }
      }

      throw new Error('Streaming response ended before a completion event was received.')
    } catch (streamError) {
      if (controller.signal.aborted) {
        setPhase('cancelled')
        return null
      }

      setError(streamError)
      setPhase('error')
      abortControllerRef.current = null
      throw streamError
    }
  }, [])

  useEffect(() => () => abortControllerRef.current?.abort(), [])

  return {
    phase,
    metadata,
    streamMode: metadata?.streamMode ?? null,
    columns,
    rows,
    rowCount: rows.length,
    result,
    error,
    start,
    cancel,
    reset,
  }
}
