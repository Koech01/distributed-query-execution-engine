import { describe, expect, it } from 'vitest'

import { buildQueryResultFromStream, parseQueryStreamEvent, parseServerSentEventBlock } from '@/lib/query-stream'
import { mockQueryId, mockQueryResult } from '@/test/mocks/handlers'

describe('parseServerSentEventBlock', () => {
  it('parses event name and JSON data lines', () => {
    expect(
      parseServerSentEventBlock('event: metadata\ndata: {"queryId":"abc","totalShards":1,"streamMode":"incremental"}'),
    ).toEqual({
      event: 'metadata',
      data: '{"queryId":"abc","totalShards":1,"streamMode":"incremental"}',
    })
  })
})

describe('parseQueryStreamEvent', () => {
  it('parses metadata, columns, row, and complete events', () => {
    expect(
      parseQueryStreamEvent(
        'metadata',
        JSON.stringify({ queryId: mockQueryId, totalShards: 4, streamMode: 'incremental' }),
      ),
    ).toMatchObject({
      kind: 'metadata',
      data: { queryId: mockQueryId, totalShards: 4, streamMode: 'incremental' },
    })

    expect(parseQueryStreamEvent('columns', JSON.stringify({ columns: ['id', 'name'] }))).toEqual({
      kind: 'columns',
      data: { columns: ['id', 'name'] },
    })

    expect(parseQueryStreamEvent('row', JSON.stringify({ values: ['1', 'Ada'] }))).toEqual({
      kind: 'row',
      data: { values: ['1', 'Ada'] },
    })

    expect(
      parseQueryStreamEvent(
        'complete',
        JSON.stringify({
          rowCount: 1,
          totalShards: 4,
          successfulShards: 4,
          failedShards: [],
          degraded: false,
          degradationReason: null,
          executionMs: 12,
        }),
      ),
    ).toMatchObject({
      kind: 'complete',
      data: { rowCount: 1, degraded: false },
    })
  })
})

describe('buildQueryResultFromStream', () => {
  it('builds a terminal QueryResult from stream events', () => {
    const result = buildQueryResultFromStream({
      metadata: {
        queryId: mockQueryId,
        totalShards: 4,
        streamMode: 'incremental',
      },
      columns: mockQueryResult.columns,
      rows: mockQueryResult.rows,
      complete: {
        rowCount: mockQueryResult.rowCount,
        totalShards: mockQueryResult.totalShards,
        successfulShards: mockQueryResult.successfulShards,
        failedShards: mockQueryResult.failedShards,
        degraded: mockQueryResult.degraded,
        degradationReason: mockQueryResult.degradationReason,
        executionMs: mockQueryResult.executionMs,
      },
    })

    expect(result).toMatchObject({
      queryId: mockQueryId,
      columns: mockQueryResult.columns,
      rows: mockQueryResult.rows,
      rowCount: mockQueryResult.rowCount,
      degraded: false,
      fromCache: false,
    })
  })
})
