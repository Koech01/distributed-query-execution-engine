import { z } from 'zod'

import type {
  ActiveQueryKind,
  ActiveQueryPage,
  AdminCacheFlushRequest,
  AdminCacheFlushResult,
  AdminCacheStats,
  AdminDashboardStats,
  AuthTokenResponse,
  CancelQueryResult,
  ErrorResponse,
  ExchangeTokenRequest,
  HealthResponse,
  LoginRequest,
  ChangePasswordRequest,
  QueryPlanDetails,
  QueryResult,
  QueryStatusResponse,
  QueryStreamComplete,
  QueryStreamMetadata,
  RegisterRequest,
  SubmitQueryRequest,
  SubmitQueryResponse,
  UpdateProfileRequest,
  UpdateProfileResponse,
  UserProfile,
  WorkerHealthDashboard,
  WorkerProbeStatus,
} from '@/components/types'

export const MAX_SQL_LENGTH = 10_000
export const MAX_PARAMETERS = 50
export const MIN_TIMEOUT_SECONDS = 1
export const MAX_TIMEOUT_SECONDS = 120
export const MIN_MAX_NODES = 1
export const MAX_MAX_NODES = 1_000

export const failurePolicySchema = z.enum(['BestEffort', 'StrictAll'])

export const queryParameterSchema = z.object({
  name: z.string().regex(/^@[a-zA-Z][a-zA-Z0-9_]*$/, {
    error: 'Parameter names must start with @ followed by a letter, then letters, numbers, or underscores.',
  }),
  type: z.string().min(1, 'Parameter type is required.').max(128, 'Parameter type is too long.'),
  value: z.string(),
})

export const submitQueryRequestSchema = z.object({
  sql: z
    .string()
    .refine((sql) => sql.trim().length > 0, 'SQL is required.')
    .max(MAX_SQL_LENGTH, `SQL must be ${MAX_SQL_LENGTH} characters or fewer.`)
    .refine((sql) => !sql.includes('\0'), 'SQL must not contain null bytes.'),
  parameters: z.array(queryParameterSchema).max(MAX_PARAMETERS, `Queries support at most ${MAX_PARAMETERS} parameters.`).default([]),
  timeoutSeconds: z.number().int().min(MIN_TIMEOUT_SECONDS).max(MAX_TIMEOUT_SECONDS).optional(),
  maxNodes: z.number().int().min(MIN_MAX_NODES).max(MAX_MAX_NODES).optional(),
  async: z.boolean().default(false),
  failurePolicy: failurePolicySchema.default('BestEffort'),
  queryId: z.string().uuid().optional(),
}) satisfies z.ZodType<SubmitQueryRequest>

export const queryResultSchema = z.object({
  queryId: z.string().uuid(),
  columns: z.array(z.string()),
  rows: z.array(z.array(z.string())),
  rowCount: z.number().int().nonnegative(),
  totalShards: z.number().int().nonnegative(),
  successfulShards: z.number().int().nonnegative(),
  failedShards: z.array(z.number().int().nonnegative()),
  degraded: z.boolean(),
  degradationReason: z.string().nullable(),
  executionMs: z.number().nonnegative(),
  fromCache: z.boolean(),
}) satisfies z.ZodType<QueryResult>

export const submitQueryResponseSchema = z.object({
  queryId: z.string().uuid(),
  statusUrl: z.string().nullable(),
}) satisfies z.ZodType<SubmitQueryResponse>

export const queryStatusResponseSchema = z.object({
  queryId: z.string().uuid(),
  status: z.enum(['running', 'completed']),
  message: z.string().nullable(),
}) satisfies z.ZodType<QueryStatusResponse>

export const errorResponseSchema = z.object({
  error: z.string(),
  message: z.string(),
  details: z.array(z.string()).nullish(),
}) satisfies z.ZodType<ErrorResponse>

export const healthResponseSchema = z.object({
  status: z.string(),
}) satisfies z.ZodType<HealthResponse>

export const queryStreamModeSchema = z.enum(['incremental', 'ordered', 'buffered'])

export const queryStreamMetadataSchema = z.object({
  queryId: z.string().uuid(),
  totalShards: z.number().int().nonnegative(),
  streamMode: queryStreamModeSchema,
}) satisfies z.ZodType<QueryStreamMetadata>

export const queryStreamCompleteSchema = z.object({
  rowCount: z.number().int().nonnegative(),
  totalShards: z.number().int().nonnegative(),
  successfulShards: z.number().int().nonnegative(),
  failedShards: z.array(z.number().int().nonnegative()),
  degraded: z.boolean(),
  degradationReason: z.string().nullable(),
  executionMs: z.number().nonnegative(),
}) satisfies z.ZodType<QueryStreamComplete>

export const aggregateFunctionSchema = z.enum(['Sum', 'Count', 'Avg', 'Min', 'Max', 'CountDistinct'])

export const queryPlanDetailsSchema = z.object({
  planId: z.string().uuid(),
  planHash: z.string().min(1),
  fromCache: z.boolean(),
  targetingStrategy: z.string().min(1),
  clusterShardCount: z.number().int().nonnegative(),
  subQueries: z.array(
    z.object({
      subQueryId: z.string().uuid(),
      shardIndex: z.number().int().nonnegative(),
      totalShards: z.number().int().nonnegative(),
      sql: z.string(),
    }),
  ),
  mergeInstructions: z.object({
    orderBy: z.array(
      z.object({
        columnName: z.string(),
        descending: z.boolean(),
      }),
    ),
    aggregates: z.array(
      z.object({
        function: aggregateFunctionSchema,
        sourceColumn: z.string(),
        outputAlias: z.string(),
      }),
    ),
    limit: z.number().int().nonnegative().nullable(),
    offset: z.number().int().nonnegative().nullable(),
    isDistinct: z.boolean(),
  }),
  createdAt: z.string(),
}) satisfies z.ZodType<QueryPlanDetails>

export const loginRequestSchema = z.object({
  email: z.string().email('Enter a valid email address.'),
  password: z.string().min(1, 'Password is required.'),
}) satisfies z.ZodType<LoginRequest>

export const registerRequestSchema = z.object({
  email: z.string().email('Enter a valid email address.'),
  password: z.string().min(12, 'Password must be at least 12 characters.'),
  displayName: z
    .string()
    .trim()
    .min(2, 'Display name must be at least 2 characters.')
    .max(100, 'Display name must be 100 characters or fewer.'),
}) satisfies z.ZodType<RegisterRequest>

export const exchangeTokenRequestSchema = z.object({
  exchangeCode: z.string().min(1, 'Exchange code is required.'),
}) satisfies z.ZodType<ExchangeTokenRequest>

export const authTokenResponseSchema = z.object({
  accessToken: z.string().min(1),
  expiresIn: z.number().int().positive(),
  tokenType: z.string().min(1),
}) satisfies z.ZodType<AuthTokenResponse>

export const userProfileSchema = z.object({
  userId: z.string().min(1),
  email: z.string().email(),
  displayName: z.string().nullable(),
  hasPasswordLogin: z.boolean(),
  linkedProviders: z.array(z.string()),
  scopes: z.array(z.string()),
  createdAt: z.string(),
  updatedAt: z.string(),
}) satisfies z.ZodType<UserProfile>

export const updateProfileRequestSchema = z
  .object({
    displayName: z
      .string()
      .trim()
      .min(2, 'Display name must be at least 2 characters.')
      .max(100, 'Display name must be 100 characters or fewer.')
      .optional(),
    email: z.string().email('Enter a valid email address.').optional(),
  })
  .refine((value) => value.displayName !== undefined || value.email !== undefined, {
    message: 'At least one profile field must be provided.',
  }) satisfies z.ZodType<UpdateProfileRequest>

export const changePasswordRequestSchema = z.object({
  currentPassword: z.string().min(1, 'Current password is required.'),
  newPassword: z.string().min(12, 'New password must be at least 12 characters.'),
}) satisfies z.ZodType<ChangePasswordRequest>

export const updateProfileResponseSchema = z.object({
  profile: userProfileSchema,
  token: authTokenResponseSchema.nullable(),
}) satisfies z.ZodType<UpdateProfileResponse>

export const storedUserPreferencesSchema = z.object({
  defaultTimeoutSeconds: z.number().int().min(MIN_TIMEOUT_SECONDS).max(MAX_TIMEOUT_SECONDS),
  defaultFailurePolicy: failurePolicySchema,
  defaultAsync: z.boolean(),
  saveSqlInHistory: z.boolean(),
})

export const PLAN_HASH_REGEX = /^[a-fA-F0-9]{64}$/

const ACTIVE_QUERY_KIND_NAMES = ['Sync', 'Stream', 'Async'] as const
const WORKER_PROBE_STATUS_NAMES = ['Healthy', 'Unhealthy', 'Unreachable'] as const

/** API serializes enums as strings; coordinator-internal payloads may use numeric values. */
export const activeQueryKindSchema = z
  .union([z.literal(0), z.literal(1), z.literal(2), z.enum(ACTIVE_QUERY_KIND_NAMES)])
  .transform((value): ActiveQueryKind => {
    if (typeof value === 'number') {
      return value as ActiveQueryKind
    }

    const map: Record<(typeof ACTIVE_QUERY_KIND_NAMES)[number], ActiveQueryKind> = {
      Sync: 0,
      Stream: 1,
      Async: 2,
    }

    return map[value]
  })

/** API serializes enums as strings; coordinator-internal payloads may use numeric values. */
export const workerProbeStatusSchema = z
  .union([z.literal(0), z.literal(1), z.literal(2), z.enum(WORKER_PROBE_STATUS_NAMES)])
  .transform((value): WorkerProbeStatus => {
    if (typeof value === 'number') {
      return value as WorkerProbeStatus
    }

    const map: Record<(typeof WORKER_PROBE_STATUS_NAMES)[number], WorkerProbeStatus> = {
      Healthy: 0,
      Unhealthy: 1,
      Unreachable: 2,
    }

    return map[value]
  })

export const adminDashboardStatsSchema = z.object({
  activeQueries: z.number().int().nonnegative(),
  healthyWorkers: z.number().int().nonnegative(),
  totalWorkers: z.number().int().nonnegative(),
  planCacheEntries: z.number().int().nonnegative(),
  resultCacheEntries: z.number().int().nonnegative(),
  asyncQueryStatusEntries: z.number().int().nonnegative(),
  generatedAt: z.string(),
}) satisfies z.ZodType<AdminDashboardStats>

export const adminCacheStatsSchema = z.object({
  planCacheEntries: z.number().int().nonnegative(),
  resultCacheEntries: z.number().int().nonnegative(),
  asyncQueryStatusEntries: z.number().int().nonnegative(),
  generatedAt: z.string(),
}) satisfies z.ZodType<AdminCacheStats>

export const adminCacheFlushRequestSchema = z.object({
  planHash: z
    .string()
    .trim()
    .regex(PLAN_HASH_REGEX, 'Plan hash must be a 64-character hexadecimal SHA-256 value.')
    .optional()
    .nullable(),
}) satisfies z.ZodType<AdminCacheFlushRequest>

export const adminCacheFlushResultSchema = z.object({
  deletedPlanEntries: z.number().int().nonnegative(),
  scope: z.string().min(1),
  flushedAt: z.string(),
}) satisfies z.ZodType<AdminCacheFlushResult>

export const activeQuerySummarySchema = z.object({
  queryId: z.string().uuid(),
  kind: activeQueryKindSchema,
  planHash: z.string().min(1),
  subQueryCount: z.number().int().nonnegative(),
  startedAt: z.string(),
  cancellationRequested: z.boolean(),
})

export const activeQueryPageSchema = z.object({
  queries: z.array(activeQuerySummarySchema),
  totalCount: z.number().int().nonnegative(),
  limit: z.number().int().positive(),
  offset: z.number().int().nonnegative(),
}) satisfies z.ZodType<ActiveQueryPage>

export const cancelQueryResultSchema = z.object({
  queryId: z.string().uuid(),
  found: z.boolean(),
  cancellationRequested: z.boolean(),
  message: z.string(),
}) satisfies z.ZodType<CancelQueryResult>

export const workerHealthEntrySchema = z.object({
  nodeId: z.string().min(1),
  address: z.string().min(1),
  grpcPort: z.number().int().positive(),
  healthPort: z.number().int().positive(),
  shards: z.array(z.number().int().nonnegative()),
  version: z.string().min(1),
  liveStatus: workerProbeStatusSchema,
  readyStatus: workerProbeStatusSchema,
  grpcStatus: workerProbeStatusSchema,
  liveLatencyMs: z.number().int().nonnegative().nullable(),
  readyLatencyMs: z.number().int().nonnegative().nullable(),
  grpcLatencyMs: z.number().int().nonnegative().nullable(),
  registeredInConsul: z.boolean(),
})

export const workerHealthDashboardSchema = z.object({
  workers: z.array(workerHealthEntrySchema),
  healthyCount: z.number().int().nonnegative(),
  totalCount: z.number().int().nonnegative(),
  generatedAt: z.string(),
}) satisfies z.ZodType<WorkerHealthDashboard>

export function validateSubmitQueryRequest(request: SubmitQueryRequest): SubmitQueryRequest {
  return submitQueryRequestSchema.parse(request)
}

export function validateStoredUserPreferences(preferences: unknown) {
  return storedUserPreferencesSchema.parse(preferences)
}

export function validateAdminCacheFlushRequest(request: AdminCacheFlushRequest): AdminCacheFlushRequest {
  return adminCacheFlushRequestSchema.parse(request)
}
