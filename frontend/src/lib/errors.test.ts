import { describe, expect, it } from 'vitest'

import {
  AppError,
  type AppErrorOptions,
  AuthError,
  BACKEND_ERROR_CODES,
  NotFoundError,
  QueryParseError,
  RateLimitError,
  SystemUnavailableError,
  TimeoutError,
  ValidationError,
  getErrorMessage,
  parseRetryAfterSeconds,
  toAppError,
} from './errors'

describe('toAppError', () => {
  it.each([
    ['invalid_json', 400, ValidationError],
    ['validation_error', 400, ValidationError],
    ['query_parse_error', 400, QueryParseError],
    ['rate_limited', 429, RateLimitError],
    ['insufficient_nodes', 503, SystemUnavailableError],
    ['query_timeout', 408, TimeoutError],
    ['shard_execution_error', 502, SystemUnavailableError],
    ['shard_configuration_error', 500, AppError],
    ['request_cancelled', 499, TimeoutError],
    ['not_found', 404, NotFoundError],
    ['internal_error', 500, AppError],
  ] satisfies Array<[string, number, new (options: AppErrorOptions) => Error]>)(
    'maps backend error code %s to the expected error class',
    (code, status, ErrorClass) => {
      const error = toAppError(status, {
        error: code,
        message: `Backend message for ${code}`,
        details: ['detail'],
      })

      expect(BACKEND_ERROR_CODES).toContain(code)
      expect(error).toBeInstanceOf(ErrorClass)
      expect(error.code).toBe(code)
      expect(error.status).toBe(status)
      expect(error.details).toEqual(['detail'])
    },
  )

  it('maps auth statuses without backend error bodies', () => {
    expect(toAppError(401)).toBeInstanceOf(AuthError)
    expect(toAppError(403)).toBeInstanceOf(AuthError)
  })

  it('uses Retry-After when provided for rate limits', () => {
    const error = toAppError(429, { error: 'rate_limited', message: 'Slow down.' }, '7')

    expect(error).toBeInstanceOf(RateLimitError)
    expect((error as RateLimitError).retryAfterSeconds).toBe(7)
    expect(getErrorMessage(error)).toContain('7 seconds')
  })

  it('falls back to a bounded retry delay for rate limits without Retry-After', () => {
    const error = toAppError(429, { error: 'rate_limited', message: 'Slow down.' })

    expect(error).toBeInstanceOf(RateLimitError)
    expect((error as RateLimitError).retryAfterSeconds).toBeGreaterThanOrEqual(1)
    expect((error as RateLimitError).retryAfterSeconds).toBeLessThanOrEqual(30)
  })

  it('parses Retry-After HTTP date values and bounds distant dates', () => {
    const retryAfter = parseRetryAfterSeconds(
      'Sat, 13 Jun 2026 08:03:00 GMT',
      new Date('Sat, 13 Jun 2026 08:02:00 GMT'),
    )

    expect(retryAfter).toBe(30)
  })
})
