import { HttpResponse, http } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { mockQueryId, mockQueryPlan, mockQueryResult } from '@/test/mocks/handlers'
import { server } from '@/test/mocks/server'
import { RateLimitError } from './errors'
import { healthApi, queryApi } from './api'

describe('queryApi', () => {
  beforeEach(() => {
    vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5281')
    vi.stubEnv('VITE_DEV_USE_PROXY', 'false')
  })
  it('submits a sync query and parses a 200 QueryResult', async () => {
    const result = await queryApi.submit({
      sql: 'SELECT * FROM Orders WHERE id = @id',
      parameters: [{ name: '@id', type: 'int', value: '42' }],
      timeoutSeconds: 30,
    })

    expect(result).toEqual(mockQueryResult)
  })

  it('treats 206 Partial Content from submit as a successful degraded QueryResult', async () => {
    server.use(
      http.post('*/queries', () =>
        HttpResponse.json(
          {
            ...mockQueryResult,
            successfulShards: 3,
            failedShards: [2],
            degraded: true,
            degradationReason: 'Shard 2 timed out.',
          },
          { status: 206 },
        ),
      ),
    )

    const result = await queryApi.submit({
      sql: 'SELECT * FROM Orders',
      parameters: [],
    })

    expect('degraded' in result && result.degraded).toBe(true)
    expect('failedShards' in result && result.failedShards).toEqual([2])
  })

  it('parses 202 Accepted async submit responses and resolves the relative status URL', async () => {
    server.use(
      http.post('*/queries', () =>
        HttpResponse.json(
          {
            queryId: mockQueryId,
            statusUrl: `/queries/${mockQueryId}/status`,
          },
          { status: 202 },
        ),
      ),
    )

    const result = await queryApi.submit({
      sql: 'SELECT * FROM Orders',
      parameters: [],
      async: true,
    })

    expect(result).toEqual({
      queryId: mockQueryId,
      statusUrl: `http://localhost:5281/queries/${mockQueryId}/status`,
    })
  })

  it('fetches query status with MSW', async () => {
    await expect(queryApi.getStatus(mockQueryId)).resolves.toEqual({
      queryId: mockQueryId,
      status: 'completed',
      message: null,
    })
  })

  it('treats 206 Partial Content from result fetch as a successful degraded QueryResult', async () => {
    server.use(
      http.get('*/queries/:queryId/result', () =>
        HttpResponse.json(
          {
            ...mockQueryResult,
            successfulShards: 3,
            failedShards: [1],
            degraded: true,
            degradationReason: 'Shard 1 failed.',
          },
          { status: 206 },
        ),
      ),
    )

    await expect(queryApi.getResult(mockQueryId)).resolves.toMatchObject({
      degraded: true,
      failedShards: [1],
    })
  })

  it('fetches query plan details from POST /queries/plan', async () => {
    await expect(
      queryApi.plan({
        sql: 'SELECT TOP 10 * FROM Orders',
        parameters: [],
      }),
    ).resolves.toEqual(mockQueryPlan)
  })

  it('consumes streaming SSE events from POST /queries/stream', async () => {
    const events = []
    for await (const event of queryApi.streamEvents({
      sql: 'SELECT * FROM Orders',
      parameters: [],
    })) {
      events.push(event)
    }

    expect(events.map((event) => event.kind)).toEqual(['metadata', 'columns', 'row', 'row', 'complete'])
    expect(events.at(-1)).toMatchObject({
      kind: 'complete',
      data: { rowCount: mockQueryResult.rowCount },
    })
  })

  it('maps 429 responses with Retry-After for callers to retry later', async () => {
    server.use(
      http.post('*/queries', () =>
        HttpResponse.json(
          { error: 'rate_limited', message: 'Too many concurrent requests. Try again shortly.' },
          {
            status: 429,
            headers: { 'Retry-After': '5' },
          },
        ),
      ),
    )

    await expect(
      queryApi.submit({
        sql: 'SELECT * FROM Orders',
        parameters: [],
      }),
    ).rejects.toMatchObject({
      name: 'RateLimitError',
      retryAfterSeconds: 5,
    } satisfies Partial<RateLimitError>)
  })
})

describe('healthApi', () => {
  it('checks live and ready endpoints with MSW', async () => {
    await expect(healthApi.checkLive()).resolves.toEqual({ status: 'live' })
    await expect(healthApi.checkReady()).resolves.toEqual({ status: 'ready' })
  })

  it('does not require query auth headers for health endpoints', async () => {
    server.use(
      http.get('*/health/live', ({ request }) => {
        expect(request.headers.get('Authorization')).toBeNull()
        return HttpResponse.json({ status: 'live' })
      }),
    )

    await expect(healthApi.checkLive()).resolves.toEqual({ status: 'live' })
  })
})
