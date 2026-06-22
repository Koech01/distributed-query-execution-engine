import type { Page, Route } from '@playwright/test'

export const mockQueryId = '3fa85f64-5717-4562-b3fc-2c963f66afa6'

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

function jsonResponse(route: Route, status: number, body: unknown) {
  return route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  })
}

export async function installApiMocks(page: Page): Promise<void> {
  await page.route('**/health/live', (route) => jsonResponse(route, 200, { status: 'live' }))
  await page.route('**/health/ready', (route) => jsonResponse(route, 200, { status: 'ready' }))

  await page.route('**/queries/*/status', (route) => {
    const url = route.request().url()
    const queryId = url.split('/queries/')[1]?.split('/status')[0] ?? mockQueryId

    return jsonResponse(route, 200, {
      queryId,
      status: 'completed',
      message: 'Result is ready.',
    })
  })

  await page.route('**/queries/*/result', (route) => jsonResponse(route, 200, mockQueryResult))

  await page.route('**/queries', async (route) => {
    if (route.request().method() !== 'POST') {
      await route.continue()
      return
    }

    const body = JSON.parse(route.request().postData() ?? '{}') as { async?: boolean }

    if (body.async) {
      await jsonResponse(route, 202, {
        queryId: mockQueryId,
        statusUrl: `/queries/${mockQueryId}/status`,
      })
      return
    }

    await jsonResponse(route, 200, mockQueryResult)
  })
}

export function shouldMockApi(): boolean {
  return process.env.PLAYWRIGHT_MOCK_API !== 'false'
}
