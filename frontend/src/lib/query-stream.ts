import type { QueryResult, QueryStreamComplete, QueryStreamEvent, QueryStreamMetadata } from '@/components/types'
import {
  queryStreamCompleteSchema,
  queryStreamMetadataSchema,
} from '@/lib/schemas'

interface ParsedServerSentEvent {
  event: string
  data: string
}

export function parseServerSentEventBlock(block: string): ParsedServerSentEvent | null {
  const lines = block.split('\n')
  let eventName = 'message'
  const dataLines: string[] = []

  for (const line of lines) {
    if (line.startsWith('event:')) {
      eventName = line.slice('event:'.length).trim()
      continue
    }

    if (line.startsWith('data:')) {
      dataLines.push(line.slice('data:'.length).trim())
    }
  }

  if (dataLines.length === 0) {
    return null
  }

  return {
    event: eventName,
    data: dataLines.join('\n'),
  }
}

export function parseQueryStreamEvent(eventName: string, dataJson: string): QueryStreamEvent | null {
  if (!dataJson) {
    return null
  }

  try {
    const payload = JSON.parse(dataJson) as unknown

    switch (eventName) {
      case 'metadata':
        return {
          kind: 'metadata',
          data: queryStreamMetadataSchema.parse(payload),
        }
      case 'columns': {
        const columnsPayload = payload as { columns?: unknown }
        return {
          kind: 'columns',
          data: {
            columns: Array.isArray(columnsPayload.columns)
              ? columnsPayload.columns.filter((column): column is string => typeof column === 'string')
              : [],
          },
        }
      }
      case 'row': {
        const rowPayload = payload as { values?: unknown }
        return {
          kind: 'row',
          data: {
            values: Array.isArray(rowPayload.values)
              ? rowPayload.values.map((value) => String(value ?? ''))
              : [],
          },
        }
      }
      case 'complete':
        return {
          kind: 'complete',
          data: queryStreamCompleteSchema.parse(payload),
        }
      default:
        return null
    }
  } catch {
    return null
  }
}

export async function* readQueryStreamEvents(body: ReadableStream<Uint8Array>): AsyncGenerator<QueryStreamEvent> {
  const reader = body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  try {
    while (true) {
      const { done, value } = await reader.read()
      if (done) {
        break
      }

      buffer += decoder.decode(value, { stream: true })

      let separatorIndex = buffer.indexOf('\n\n')
      while (separatorIndex >= 0) {
        const block = buffer.slice(0, separatorIndex)
        buffer = buffer.slice(separatorIndex + 2)
        const parsedBlock = parseServerSentEventBlock(block)

        if (parsedBlock) {
          const event = parseQueryStreamEvent(parsedBlock.event, parsedBlock.data)
          if (event) {
            yield event
          }
        }

        separatorIndex = buffer.indexOf('\n\n')
      }
    }

    if (buffer.trim().length > 0) {
      const parsedBlock = parseServerSentEventBlock(buffer)
      if (parsedBlock) {
        const event = parseQueryStreamEvent(parsedBlock.event, parsedBlock.data)
        if (event) {
          yield event
        }
      }
    }
  } finally {
    reader.releaseLock()
  }
}

export function buildQueryResultFromStream(input: {
  metadata: QueryStreamMetadata
  columns: string[]
  rows: string[][]
  complete: QueryStreamComplete
}): QueryResult {
  return {
    queryId: input.metadata.queryId,
    columns: input.columns,
    rows: input.rows,
    rowCount: input.complete.rowCount,
    totalShards: input.complete.totalShards,
    successfulShards: input.complete.successfulShards,
    failedShards: input.complete.failedShards,
    degraded: input.complete.degraded,
    degradationReason: input.complete.degradationReason,
    executionMs: input.complete.executionMs,
    fromCache: false,
  }
}
