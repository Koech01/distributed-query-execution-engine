import { HttpResponse, http } from 'msw'

import { buildAuthCookieHeader, requestHasAuthCookie } from '@/lib/auth-cookie'
import { clearMockAuthCookie, setMockAuthCookie } from '@/test/mocks/auth-cookie'

export const mockQueryId = '3fa85f64-5717-4562-b3fc-2c963f66afa6'
export const mockPlanId = '7c9e6679-7425-40de-944b-e07fc1f90ae7'
export const mockActiveQueryId = '11111111-1111-4111-8111-111111111111'
export const mockPlanHash = 'a'.repeat(64)

export const mockAdminDashboardStats = {
  activeQueries: 2,
  healthyWorkers: 3,
  totalWorkers: 4,
  planCacheEntries: 18,
  resultCacheEntries: 6,
  asyncQueryStatusEntries: 1,
  generatedAt: '2026-06-20T12:00:00.000Z',
}

export const mockAdminCacheStats = {
  planCacheEntries: 18,
  resultCacheEntries: 6,
  asyncQueryStatusEntries: 1,
  generatedAt: '2026-06-20T12:00:00.000Z',
}

export const mockActiveQueryPage = {
  queries: [
    {
      queryId: mockActiveQueryId,
      kind: 0,
      planHash: mockPlanHash,
      subQueryCount: 4,
      startedAt: '2026-06-20T11:59:30.000Z',
      cancellationRequested: false,
    },
  ],
  totalCount: 1,
  limit: 50,
  offset: 0,
}

export const mockAccountProfile = {
  userId: '11111111-1111-4111-8111-111111111111',
  email: 'reader@example.com',
  displayName: 'Reader User',
  hasPasswordLogin: true,
  linkedProviders: [] as string[],
  scopes: ['query:read'],
  createdAt: '2026-06-20T10:00:00.000Z',
  updatedAt: '2026-06-20T10:00:00.000Z',
}

let mockTokenNonce = 0

export function resetMockAccountProfile(): void {
  mockTokenNonce = 0
  mockAccountProfile.userId = '11111111-1111-4111-8111-111111111111'
  mockAccountProfile.email = 'reader@example.com'
  mockAccountProfile.displayName = 'Reader User'
  mockAccountProfile.hasPasswordLogin = true
  mockAccountProfile.linkedProviders = []
  mockAccountProfile.scopes = ['query:read']
  mockAccountProfile.createdAt = '2026-06-20T10:00:00.000Z'
  mockAccountProfile.updatedAt = '2026-06-20T10:00:00.000Z'
  clearMockAuthCookie()
}

function createMockAccessToken(scope = 'query:read'): string {
  mockTokenNonce += 1
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
  const body = btoa(
    JSON.stringify({
      scope,
      nonce: mockTokenNonce,
      exp: Math.floor(Date.now() / 1000) + 3600,
    }),
  )
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')

  return `${header}.${body}.signature`
}

function issueAuthTokenResponse(scope = 'query:read') {
  const accessToken = createMockAccessToken(scope)
  setMockAuthCookie(accessToken, scope.split(/\s+/).filter(Boolean))

  return HttpResponse.json(
    {
      accessToken,
      expiresIn: 3600,
      tokenType: 'Bearer',
    },
    {
      headers: {
        'Set-Cookie': buildAuthCookieHeader(accessToken),
      },
    },
  )
}

function unauthorizedResponse() {
  return HttpResponse.json(
    {
      error: 'unauthorized',
      message: 'Authentication required.',
    },
    { status: 401 },
  )
}

export const mockWorkerHealthDashboard = {
  workers: [
    {
      nodeId: 'worker-1',
      address: '127.0.0.1',
      grpcPort: 5100,
      healthPort: 5101,
      shards: [0, 1],
      version: '1.0.0',
      liveStatus: 0,
      readyStatus: 0,
      grpcStatus: 0,
      liveLatencyMs: 5,
      readyLatencyMs: 6,
      grpcLatencyMs: 7,
      registeredInConsul: true,
    },
    {
      nodeId: 'worker-2',
      address: '127.0.0.2',
      grpcPort: 5200,
      healthPort: 5201,
      shards: [2, 3],
      version: '1.0.0',
      liveStatus: 1,
      readyStatus: 0,
      grpcStatus: 2,
      liveLatencyMs: null,
      readyLatencyMs: 12,
      grpcLatencyMs: null,
      registeredInConsul: false,
    },
  ],
  healthyCount: 1,
  totalCount: 2,
  generatedAt: '2026-06-20T12:00:00.000Z',
}

export const mockQueryResult = {
  queryId: mockQueryId,
  columns: ['id', 'name'],
  rows: [
    ['1', 'Ada'],
    ['2', 'Grace'],
  ],
  rowCount: 2,
  totalShards: 4,
  successfulShards: 4,
  failedShards: [],
  degraded: false,
  degradationReason: null,
  executionMs: 87,
  fromCache: false,
}

export const mockQueryPlan = {
  planId: mockPlanId,
  planHash: 'plan-hash-abc123',
  fromCache: false,
  targetingStrategy: 'broadcast',
  clusterShardCount: 4,
  subQueries: [
    {
      subQueryId: '11111111-1111-4111-8111-111111111111',
      shardIndex: 0,
      totalShards: 4,
      sql: 'SELECT * FROM Orders WHERE ShardId = 0',
    },
    {
      subQueryId: '22222222-2222-4222-8222-222222222222',
      shardIndex: 1,
      totalShards: 4,
      sql: 'SELECT * FROM Orders WHERE ShardId = 1',
    },
  ],
  mergeInstructions: {
    orderBy: [{ columnName: 'id', descending: false }],
    aggregates: [],
    limit: 10,
    offset: null,
    isDistinct: false,
  },
  createdAt: '2026-06-19T12:00:00.000Z',
}

function buildMockStreamBody() {
  return [
    `event: metadata\ndata: ${JSON.stringify({ queryId: mockQueryId, totalShards: 4, streamMode: 'incremental' })}\n`,
    `event: columns\ndata: ${JSON.stringify({ columns: mockQueryResult.columns })}\n`,
    ...mockQueryResult.rows.map(
      (row) => `event: row\ndata: ${JSON.stringify({ values: row })}\n`,
    ),
    `event: complete\ndata: ${JSON.stringify({
      rowCount: mockQueryResult.rowCount,
      totalShards: mockQueryResult.totalShards,
      successfulShards: mockQueryResult.successfulShards,
      failedShards: mockQueryResult.failedShards,
      degraded: mockQueryResult.degraded,
      degradationReason: mockQueryResult.degradationReason,
      executionMs: mockQueryResult.executionMs,
    })}\n\n`,
  ].join('\n')
}

export const handlers = [
  http.post('*/queries', () => HttpResponse.json(mockQueryResult)),
  http.post('*/queries/plan', () => HttpResponse.json(mockQueryPlan)),
  http.post('*/queries/stream', () =>
    new HttpResponse(buildMockStreamBody(), {
      status: 200,
      headers: {
        'Content-Type': 'text/event-stream',
      },
    }),
  ),
  http.get('*/queries/:queryId/status', ({ params }) =>
    HttpResponse.json({
      queryId: params.queryId,
      status: 'completed',
      message: null,
    }),
  ),
  http.get('*/queries/:queryId/result', () => HttpResponse.json(mockQueryResult)),
  http.get('*/health/live', () => HttpResponse.json({ status: 'live' })),
  http.get('*/health/ready', () => HttpResponse.json({ status: 'ready' })),
  http.get('*/admin/stats', () => HttpResponse.json(mockAdminDashboardStats)),
  http.get('*/admin/cache/stats', () => HttpResponse.json(mockAdminCacheStats)),
  http.post('*/admin/cache/flush', async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as { planHash?: string | null }
    const scope = body.planHash ? `plan:${body.planHash}` : 'all_plans'

    return HttpResponse.json({
      deletedPlanEntries: body.planHash ? 1 : 4,
      scope,
      flushedAt: '2026-06-20T12:01:00.000Z',
    })
  }),
  http.get('*/admin/queries/active', () => HttpResponse.json(mockActiveQueryPage)),
  http.post('*/admin/queries/:queryId/cancel', ({ params }) =>
    HttpResponse.json({
      queryId: params.queryId,
      found: true,
      cancellationRequested: true,
      message: 'Cancellation requested.',
    }),
  ),
  http.get('*/admin/workers', () => HttpResponse.json(mockWorkerHealthDashboard)),
  http.post('*/auth/login', () => issueAuthTokenResponse()),
  http.post('*/auth/register', () => issueAuthTokenResponse()),
  http.post('*/auth/token/exchange', () => issueAuthTokenResponse()),
  http.get('*/auth/account', ({ request }) => {
    if (!requestHasAuthCookie(request)) {
      return unauthorizedResponse()
    }

    return HttpResponse.json(mockAccountProfile)
  }),
  http.patch('*/auth/account', async ({ request }) => {
    if (!requestHasAuthCookie(request)) {
      return unauthorizedResponse()
    }

    const body = (await request.json()) as { displayName?: string; email?: string }
    const emailChanged = body.email !== undefined && body.email !== mockAccountProfile.email

    if (body.displayName !== undefined) {
      mockAccountProfile.displayName = body.displayName
    }
    if (body.email !== undefined) {
      mockAccountProfile.email = body.email
    }
    mockAccountProfile.updatedAt = '2026-06-20T12:30:00.000Z'

    if (emailChanged) {
      const accessToken = createMockAccessToken()
      setMockAuthCookie(accessToken)

      return HttpResponse.json(
        {
          profile: { ...mockAccountProfile },
          token: {
            accessToken,
            expiresIn: 3600,
            tokenType: 'Bearer',
          },
        },
        {
          headers: {
            'Set-Cookie': buildAuthCookieHeader(accessToken),
          },
        },
      )
    }

    return HttpResponse.json({
      profile: { ...mockAccountProfile },
      token: null,
    })
  }),
  http.post('*/auth/account/change-password', async ({ request }) => {
    if (!requestHasAuthCookie(request)) {
      return unauthorizedResponse()
    }

    const body = (await request.json()) as { currentPassword: string; newPassword: string }
    if (body.currentPassword !== 'correct-horse-battery-staple') {
      return HttpResponse.json(
        {
          error: 'authentication_failed',
          message: 'Current password is incorrect.',
        },
        { status: 401 },
      )
    }

    const accessToken = createMockAccessToken()
    setMockAuthCookie(accessToken)

    return HttpResponse.json(
      {
        accessToken,
        expiresIn: 3600,
        tokenType: 'Bearer',
      },
      {
        headers: {
          'Set-Cookie': buildAuthCookieHeader(accessToken),
        },
      },
    )
  }),
  http.delete('*/auth/account', ({ request }) => {
    if (!requestHasAuthCookie(request)) {
      return unauthorizedResponse()
    }

    clearMockAuthCookie()
    return new HttpResponse(null, { status: 204 })
  }),
]
