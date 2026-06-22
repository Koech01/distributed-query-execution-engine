import type {
  ActiveQueryPage,
  AdminCacheFlushRequest,
  AdminCacheFlushResult,
  AdminCacheStats,
  AdminDashboardStats,
  AuthTokenResponse,
  CancelQueryResult,
  ChangePasswordRequest,
  ExchangeTokenRequest,
  HealthResponse,
  LoginRequest,
  QueryPlanDetails,
  QueryResult,
  QueryStatusResponse,
  QueryStreamEvent,
  RegisterRequest,
  SubmitQueryRequest,
  SubmitQueryResult,
  SubmitQueryResponse,
  UpdateProfileRequest,
  UpdateProfileResponse,
  UserProfile,
  WorkerHealthDashboard,
} from '@/components/types'
import { clearSession } from './auth'
import { NetworkError, isErrorResponse, toAppError } from './errors'
import { captureError, generateTraceParent } from './observability'
import { readQueryStreamEvents } from './query-stream'
import {
  activeQueryPageSchema,
  adminCacheFlushRequestSchema,
  adminCacheFlushResultSchema,
  adminCacheStatsSchema,
  adminDashboardStatsSchema,
  authTokenResponseSchema,
  cancelQueryResultSchema,
  changePasswordRequestSchema,
  errorResponseSchema,
  exchangeTokenRequestSchema,
  healthResponseSchema,
  loginRequestSchema,
  queryPlanDetailsSchema,
  queryResultSchema,
  queryStatusResponseSchema,
  registerRequestSchema,
  submitQueryRequestSchema,
  submitQueryResponseSchema,
  updateProfileRequestSchema,
  updateProfileResponseSchema,
  userProfileSchema,
  workerHealthDashboardSchema,
} from './schemas'

import { resolveApiBaseUrl } from './api-base-url'

export function getApiBaseUrl(): string {
  return resolveApiBaseUrl()
}

export function resolveApiUrl(pathOrUrl: string): string {
  return new URL(pathOrUrl, withTrailingSlash(getApiBaseUrl())).toString()
}

export const queryApi = {
  async submit(request: SubmitQueryRequest, signal?: AbortSignal): Promise<SubmitQueryResult> {
    const payload = submitQueryRequestSchema.parse(request)
    const response = await requestJson('/queries', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
      includeAuth: true,
      successStatuses: [200, 202, 206],
    })

    if (response.status === 202) {
      return normalizeSubmitQueryResponse(submitQueryResponseSchema.parse(response.body))
    }

    return queryResultSchema.parse(response.body)
  },

  async getStatus(queryId: string, signal?: AbortSignal): Promise<QueryStatusResponse> {
    return queryStatusResponseSchema.parse(await getJson(`/queries/${encodeURIComponent(queryId)}/status`, signal))
  },

  async getResult(queryId: string, signal?: AbortSignal): Promise<QueryResult> {
    return queryResultSchema.parse(
      await requestJson(`/queries/${encodeURIComponent(queryId)}/result`, {
        method: 'GET',
        signal,
        includeAuth: true,
        successStatuses: [200, 206],
      }).then((response) => response.body),
    )
  },

  async plan(request: SubmitQueryRequest, signal?: AbortSignal): Promise<QueryPlanDetails> {
    const payload = submitQueryRequestSchema.parse(request)
    const response = await requestJson('/queries/plan', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
      includeAuth: true,
      successStatuses: [200],
    })

    return queryPlanDetailsSchema.parse(response.body)
  },

  async *streamEvents(request: SubmitQueryRequest, signal?: AbortSignal): AsyncGenerator<QueryStreamEvent> {
    const payload = submitQueryRequestSchema.parse({ ...request, async: false })
    let response: Response

    try {
      response = await fetch(resolveApiUrl('/queries/stream'), {
        method: 'POST',
        headers: buildHeaders(true, true, 'text/event-stream'),
        body: JSON.stringify(payload),
        signal,
        credentials: 'include',
      })
    } catch (error) {
      const networkError = new NetworkError(error instanceof Error ? error.message : undefined)
      captureError(networkError)
      throw networkError
    }

    if (!response.ok) {
      const body = await readJsonBody(response)
      const parsedErrorResponse = errorResponseSchema.safeParse(body)
      const errorResponse = parsedErrorResponse.success
        ? parsedErrorResponse.data
        : isErrorResponse(body)
          ? body
          : undefined
      const appError = toAppError(response.status, errorResponse, response.headers.get('Retry-After'))

      captureError(appError, {
        status: appError.status,
        code: appError.code,
      })

      if (response.status === 401) {
        clearSession()
        redirectToLogin()
      }

      if (response.status === 403) {
        redirectToUnauthorized()
      }

      throw appError
    }

    if (!response.body) {
      throw new NetworkError('Streaming response did not include a body.')
    }

    for await (const event of readQueryStreamEvents(response.body)) {
      if (signal?.aborted) {
        throw new DOMException('The stream was cancelled.', 'AbortError')
      }

      yield event
    }
  },
}

export const healthApi = {
  async checkLive(signal?: AbortSignal): Promise<HealthResponse> {
    return healthResponseSchema.parse(await getJson('/health/live', signal, false))
  },

  async checkReady(signal?: AbortSignal): Promise<HealthResponse> {
    return healthResponseSchema.parse(await getJson('/health/ready', signal, false))
  },
}

export const adminApi = {
  async getStats(signal?: AbortSignal): Promise<AdminDashboardStats> {
    return adminDashboardStatsSchema.parse(await getJson('/admin/stats', signal))
  },

  async getCacheStats(signal?: AbortSignal): Promise<AdminCacheStats> {
    return adminCacheStatsSchema.parse(await getJson('/admin/cache/stats', signal))
  },

  async flushCache(request: AdminCacheFlushRequest = {}, signal?: AbortSignal): Promise<AdminCacheFlushResult> {
    const payload = adminCacheFlushRequestSchema.parse(request)
    const response = await requestJson('/admin/cache/flush', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
      includeAuth: true,
      successStatuses: [200],
    })

    return adminCacheFlushResultSchema.parse(response.body)
  },

  async getActiveQueries(
    options: { limit?: number; offset?: number } = {},
    signal?: AbortSignal,
  ): Promise<ActiveQueryPage> {
    const limit = options.limit ?? 50
    const offset = options.offset ?? 0
    const path = `/admin/queries/active?limit=${encodeURIComponent(String(limit))}&offset=${encodeURIComponent(String(offset))}`

    return activeQueryPageSchema.parse(await getJson(path, signal))
  },

  async cancelQuery(queryId: string, signal?: AbortSignal): Promise<CancelQueryResult> {
    const response = await requestJson(`/admin/queries/${encodeURIComponent(queryId)}/cancel`, {
      method: 'POST',
      signal,
      includeAuth: true,
      successStatuses: [200],
    })

    return cancelQueryResultSchema.parse(response.body)
  },

  async getWorkers(signal?: AbortSignal): Promise<WorkerHealthDashboard> {
    return workerHealthDashboardSchema.parse(await getJson('/admin/workers', signal))
  },
}

export const authApi = {
  async login(request: LoginRequest, signal?: AbortSignal): Promise<AuthTokenResponse> {
    const payload = loginRequestSchema.parse(request)
    const response = await requestJson('/auth/login', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
      includeAuth: false,
      successStatuses: [200],
      redirectOnUnauthorized: false,
    })

    return authTokenResponseSchema.parse(response.body)
  },

  async register(request: RegisterRequest, signal?: AbortSignal): Promise<AuthTokenResponse> {
    const payload = registerRequestSchema.parse(request)
    const response = await requestJson('/auth/register', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
      includeAuth: false,
      successStatuses: [200],
      redirectOnUnauthorized: false,
    })

    return authTokenResponseSchema.parse(response.body)
  },

  async exchangeToken(request: ExchangeTokenRequest, signal?: AbortSignal): Promise<AuthTokenResponse> {
    const payload = exchangeTokenRequestSchema.parse(request)
    const response = await requestJson('/auth/token/exchange', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
      includeAuth: false,
      successStatuses: [200],
      redirectOnUnauthorized: false,
    })

    return authTokenResponseSchema.parse(response.body)
  },
}

export const accountApi = {
  async getProfile(signal?: AbortSignal, options?: { redirectOnUnauthorized?: boolean }): Promise<UserProfile> {
    return userProfileSchema.parse(
      await getJson('/auth/account', signal, true, options?.redirectOnUnauthorized ?? true),
    )
  },

  async updateProfile(request: UpdateProfileRequest, signal?: AbortSignal): Promise<UpdateProfileResponse> {
    const payload = updateProfileRequestSchema.parse(request)
    const response = await requestJson('/auth/account', {
      method: 'PATCH',
      body: JSON.stringify(payload),
      signal,
      includeAuth: true,
      successStatuses: [200],
    })

    return updateProfileResponseSchema.parse(response.body)
  },

  async changePassword(request: ChangePasswordRequest, signal?: AbortSignal): Promise<AuthTokenResponse> {
    const payload = changePasswordRequestSchema.parse(request)
    const response = await requestJson('/auth/account/change-password', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
      includeAuth: true,
      successStatuses: [200],
    })

    return authTokenResponseSchema.parse(response.body)
  },

  async deleteAccount(signal?: AbortSignal): Promise<void> {
    await requestJson('/auth/account', {
      method: 'DELETE',
      signal,
      includeAuth: true,
      successStatuses: [204],
    })
  },
}

interface JsonRequestOptions {
  method: 'GET' | 'POST' | 'PATCH' | 'DELETE'
  body?: BodyInit
  signal?: AbortSignal
  includeAuth: boolean
  successStatuses: number[]
  redirectOnUnauthorized?: boolean
}

interface JsonResponse {
  status: number
  body: unknown
}

async function getJson(
  path: string,
  signal?: AbortSignal,
  includeAuth = true,
  redirectOnUnauthorized = true,
): Promise<unknown> {
  const response = await requestJson(path, {
    method: 'GET',
    signal,
    includeAuth,
    successStatuses: [200],
    redirectOnUnauthorized,
  })

  return response.body
}

async function requestJson(path: string, options: JsonRequestOptions): Promise<JsonResponse> {
  let response: Response

  try {
    response = await fetch(resolveApiUrl(path), {
      method: options.method,
      headers: buildHeaders(options.includeAuth, options.body !== undefined),
      body: options.body,
      signal: options.signal,
      credentials: 'include',
    })
  } catch (error) {
    const networkError = new NetworkError(error instanceof Error ? error.message : undefined)
    captureError(networkError)
    throw networkError
  }

  const body = await readJsonBody(response)

  if (!options.successStatuses.includes(response.status)) {
    const parsedErrorResponse = errorResponseSchema.safeParse(body)
    const errorResponse = parsedErrorResponse.success
      ? parsedErrorResponse.data
      : isErrorResponse(body)
        ? body
        : undefined
    const appError = toAppError(
      response.status,
      errorResponse,
      response.headers.get('Retry-After'),
    )

    captureError(appError, {
      status: appError.status,
      code: appError.code,
    })

    if (response.status === 401) {
      clearSession()
      if (options.redirectOnUnauthorized !== false) {
        redirectToLogin()
      }
    }

    if (response.status === 403) {
      redirectToUnauthorized()
    }

    throw appError
  }

  return {
    status: response.status,
    body,
  }
}

function buildHeaders(_includeAuth: boolean, hasBody: boolean, accept = 'application/json'): HeadersInit {
  const headers: Record<string, string> = {
    Accept: accept,
    traceparent: generateTraceParent(),
  }

  if (hasBody) {
    headers['Content-Type'] = 'application/json'
  }

  return headers
}

async function readJsonBody(response: Response): Promise<unknown> {
  if (response.status === 204) {
    return null
  }

  const text = await response.text()
  if (text.length === 0) {
    return null
  }

  try {
    return JSON.parse(text)
  } catch {
    return {
      error: 'invalid_json',
      message: 'Response body is not valid JSON.',
    }
  }
}

function normalizeSubmitQueryResponse(response: SubmitQueryResponse): SubmitQueryResponse {
  if (!response.statusUrl) {
    return response
  }

  return {
    ...response,
    statusUrl: resolveApiUrl(response.statusUrl),
  }
}

function redirectToLogin(): void {
  if (typeof window === 'undefined') {
    return
  }

  const currentPath = `${window.location.pathname}${window.location.search}`
  window.location.assign(`/login/?returnTo=${encodeURIComponent(currentPath)}`)
}

function redirectToUnauthorized(): void {
  if (typeof window !== 'undefined') {
    window.location.assign('/unauthorized')
  }
}

function withTrailingSlash(value: string): string {
  return value.endsWith('/') ? value : `${value}/`
}
