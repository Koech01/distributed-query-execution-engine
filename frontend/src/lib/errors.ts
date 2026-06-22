import type { ErrorResponse } from '@/components/types'
import { isRecord } from './utils'

const USER_MESSAGES: Record<string, string> = {
  invalid_json: 'The request body was not valid JSON.',
  email_already_exists: 'An account with this email address already exists. Sign in instead or use a different email.',
  validation_error: 'Check the query input and try again.',
  query_parse_error: 'The SQL query could not be parsed.',
  rate_limited: 'Too many requests. Try again shortly.',
  insufficient_nodes: 'The query engine does not have enough healthy nodes to run this query.',
  query_timeout: 'The query timed out before all work completed.',
  shard_execution_error: 'One or more shards failed while executing the query.',
  shard_configuration_error: 'A shard configuration problem prevented the query from running.',
  request_cancelled: 'The request was cancelled.',
  not_found: 'The requested query resource was not found.',
  internal_error: 'An unexpected server error occurred.',
}

export const BACKEND_ERROR_CODES = [
  'invalid_json',
  'email_already_exists',
  'validation_error',
  'query_parse_error',
  'rate_limited',
  'insufficient_nodes',
  'query_timeout',
  'shard_execution_error',
  'shard_configuration_error',
  'request_cancelled',
  'not_found',
  'internal_error',
] as const

export type BackendErrorCode = (typeof BACKEND_ERROR_CODES)[number]

export interface AppErrorOptions {
  code: string
  status: number
  message: string
  details?: string[]
  retryAfterSeconds?: number
}

export class AppError extends Error {
  readonly code: string
  readonly status: number
  readonly details: string[]

  constructor({ code, status, message, details = [] }: AppErrorOptions) {
    super(message)
    this.name = 'AppError'
    this.code = code
    this.status = status
    this.details = details
  }
}

export class ValidationError extends AppError {
  constructor(options: AppErrorOptions) {
    super(options)
    this.name = 'ValidationError'
  }
}

export class AuthError extends AppError {
  constructor(options: AppErrorOptions) {
    super(options)
    this.name = 'AuthError'
  }
}

export class QueryParseError extends AppError {
  constructor(options: AppErrorOptions) {
    super(options)
    this.name = 'QueryParseError'
  }
}

export class RateLimitError extends AppError {
  readonly retryAfterSeconds: number

  constructor(options: AppErrorOptions) {
    super(options)
    this.name = 'RateLimitError'
    this.retryAfterSeconds = options.retryAfterSeconds ?? getBoundedBackoffSeconds()
  }
}

export class SystemUnavailableError extends AppError {
  constructor(options: AppErrorOptions) {
    super(options)
    this.name = 'SystemUnavailableError'
  }
}

export class NotFoundError extends AppError {
  constructor(options: AppErrorOptions) {
    super(options)
    this.name = 'NotFoundError'
  }
}

export class TimeoutError extends AppError {
  constructor(options: AppErrorOptions) {
    super(options)
    this.name = 'TimeoutError'
  }
}

export class NetworkError extends AppError {
  constructor(message = 'Network request failed.') {
    super({ code: 'network_error', status: 0, message })
    this.name = 'NetworkError'
  }
}

export function isErrorResponse(value: unknown): value is ErrorResponse {
  return (
    isRecord(value) &&
    typeof value.error === 'string' &&
    typeof value.message === 'string' &&
    (value.details === undefined ||
      value.details === null ||
      (Array.isArray(value.details) && value.details.every((detail) => typeof detail === 'string')))
  )
}

export function getBoundedBackoffSeconds(attempt = 1): number {
  const normalizedAttempt = Math.max(1, Math.floor(attempt))
  return Math.min(30, 2 ** normalizedAttempt)
}

export function parseRetryAfterSeconds(value: string | null, now: Date = new Date()): number | undefined {
  if (!value) {
    return undefined
  }

  const seconds = Number(value)
  if (Number.isFinite(seconds) && seconds >= 0) {
    return Math.min(30, Math.ceil(seconds))
  }

  const retryDate = new Date(value)
  if (!Number.isNaN(retryDate.getTime())) {
    return Math.min(30, Math.max(0, Math.ceil((retryDate.getTime() - now.getTime()) / 1_000)))
  }

  return undefined
}

export function toAppError(status: number, response?: ErrorResponse, retryAfterHeader: string | null = null): AppError {
  const code = response?.error ?? defaultCodeForStatus(status)
  const message = response?.message ?? USER_MESSAGES[code] ?? defaultMessageForStatus(status)
  const details = response?.details ?? []

  if (status === 400 && code === 'query_parse_error') {
    return new QueryParseError({ code, status, message, details })
  }

  if (status === 400 || status === 409) {
    return new ValidationError({ code, status, message, details })
  }

  if (status === 401 || status === 403) {
    return new AuthError({ code, status, message, details })
  }

  if (status === 404) {
    return new NotFoundError({ code, status, message, details })
  }

  if (status === 408 || status === 499) {
    return new TimeoutError({ code, status, message, details })
  }

  if (status === 429) {
    return new RateLimitError({
      code,
      status,
      message,
      details,
      retryAfterSeconds: parseRetryAfterSeconds(retryAfterHeader) ?? getBoundedBackoffSeconds(),
    })
  }

  if (status === 502 || status === 503) {
    return new SystemUnavailableError({ code, status, message, details })
  }

  return new AppError({ code, status, message, details })
}

export function getErrorMessage(error: unknown): string {
  if (error instanceof RateLimitError) {
    return `${error.message} Retry in about ${error.retryAfterSeconds} seconds.`
  }

  if (error instanceof AppError) {
    return error.message
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'An unexpected error occurred.'
}

function defaultCodeForStatus(status: number): string {
  switch (status) {
    case 401:
      return 'unauthorized'
    case 403:
      return 'forbidden'
    case 404:
      return 'not_found'
    case 408:
      return 'query_timeout'
    case 429:
      return 'rate_limited'
    case 499:
      return 'request_cancelled'
    case 502:
      return 'shard_execution_error'
    case 503:
      return 'insufficient_nodes'
    case 500:
      return 'internal_error'
    default:
      return 'http_error'
  }
}

function defaultMessageForStatus(status: number): string {
  switch (status) {
    case 401:
      return 'Sign in again to continue.'
    case 403:
      return 'You do not have access to this resource.'
    case 404:
      return USER_MESSAGES.not_found
    case 408:
      return USER_MESSAGES.query_timeout
    case 429:
      return USER_MESSAGES.rate_limited
    case 499:
      return USER_MESSAGES.request_cancelled
    case 502:
      return USER_MESSAGES.shard_execution_error
    case 503:
      return USER_MESSAGES.insufficient_nodes
    default:
      return USER_MESSAGES.internal_error
  }
}
