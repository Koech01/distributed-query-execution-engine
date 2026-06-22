import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'

import {
  mockActiveQueryId,
  mockActiveQueryPage,
  mockAdminCacheStats,
  mockAdminDashboardStats,
  mockPlanHash,
  mockWorkerHealthDashboard,
} from '@/test/mocks/handlers'
import { server } from '@/test/mocks/server'
import { ValidationError } from './errors'
import { adminApi } from './api'

describe('adminApi', () => {
  it('fetches admin dashboard stats with MSW', async () => {
    await expect(adminApi.getStats()).resolves.toEqual(mockAdminDashboardStats)
  })

  it('fetches cache stats with MSW', async () => {
    await expect(adminApi.getCacheStats()).resolves.toEqual(mockAdminCacheStats)
  })

  it('flushes all plan cache entries', async () => {
    await expect(adminApi.flushCache({})).resolves.toEqual({
      deletedPlanEntries: 4,
      scope: 'all_plans',
      flushedAt: '2026-06-20T12:01:00.000Z',
    })
  })

  it('flushes plan cache by hash after client validation', async () => {
    await expect(adminApi.flushCache({ planHash: mockPlanHash })).resolves.toEqual({
      deletedPlanEntries: 1,
      scope: `plan:${mockPlanHash}`,
      flushedAt: '2026-06-20T12:01:00.000Z',
    })
  })

  it('rejects invalid plan hash values before calling the API', async () => {
    await expect(adminApi.flushCache({ planHash: 'not-a-hash' })).rejects.toThrow()
  })

  it('fetches active queries with MSW', async () => {
    await expect(adminApi.getActiveQueries()).resolves.toEqual(mockActiveQueryPage)
  })

  it('cancels an active query with MSW', async () => {
    await expect(adminApi.cancelQuery(mockActiveQueryId)).resolves.toEqual({
      queryId: mockActiveQueryId,
      found: true,
      cancellationRequested: true,
      message: 'Cancellation requested.',
    })
  })

  it('fetches worker health dashboard with MSW', async () => {
    await expect(adminApi.getWorkers()).resolves.toEqual(mockWorkerHealthDashboard)
  })

  it('parses worker health responses when probe statuses are string enums', async () => {
    server.use(
      http.get('*/admin/workers', () =>
        HttpResponse.json({
          workers: [
            {
              nodeId: 'worker-node-01',
              address: 'worker',
              grpcPort: 5100,
              healthPort: 5101,
              shards: [0, 1, 2, 3],
              version: '1.0.0',
              liveStatus: 'Healthy',
              readyStatus: 'Unhealthy',
              grpcStatus: 'Unreachable',
              liveLatencyMs: 12,
              readyLatencyMs: 15,
              grpcLatencyMs: null,
              registeredInConsul: true,
            },
          ],
          healthyCount: 0,
          totalCount: 1,
          generatedAt: '2026-06-20T12:00:00.000Z',
        }),
      ),
    )

    await expect(adminApi.getWorkers()).resolves.toMatchObject({
      workers: [
        {
          liveStatus: 0,
          readyStatus: 1,
          grpcStatus: 2,
        },
      ],
    })
  })

  it('maps admin cache flush 400 responses to validation errors', async () => {
    server.use(
      http.post('*/admin/cache/flush', () =>
        HttpResponse.json(
          {
            type: 'invalid_request',
            message: 'Plan hash must be a 64-character hexadecimal SHA256 value.',
          },
          { status: 400 },
        ),
      ),
    )

    await expect(adminApi.flushCache({ planHash: mockPlanHash })).rejects.toBeInstanceOf(ValidationError)
  })
})
