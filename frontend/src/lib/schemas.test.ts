import { describe, expect, it } from 'vitest'

import {
  MAX_SQL_LENGTH,
  MAX_TIMEOUT_SECONDS,
  MIN_TIMEOUT_SECONDS,
  changePasswordRequestSchema,
  registerRequestSchema,
  submitQueryRequestSchema,
  updateProfileRequestSchema,
  validateSubmitQueryRequest,
  workerHealthDashboardSchema,
} from './schemas'

const validRequest = {
  sql: 'SELECT * FROM Orders WHERE id = @id',
  parameters: [{ name: '@id', type: 'int', value: '42' }],
  timeoutSeconds: 30,
  failurePolicy: 'BestEffort' as const,
}

describe('submitQueryRequestSchema', () => {
  it('accepts a valid outbound query payload and applies backend defaults', () => {
    const result = validateSubmitQueryRequest(validRequest)

    expect(result.async).toBe(false)
    expect(result.failurePolicy).toBe('BestEffort')
    expect(result.parameters).toEqual(validRequest.parameters)
  })

  it.each(['id', '@1id', '@bad-name', '@', ''])('rejects invalid parameter name %s', (name) => {
    const result = submitQueryRequestSchema.safeParse({
      ...validRequest,
      parameters: [{ name, type: 'int', value: '42' }],
    })

    expect(result.success).toBe(false)
  })

  it.each([MIN_TIMEOUT_SECONDS - 1, MAX_TIMEOUT_SECONDS + 1, 1.5])('rejects invalid timeout %s', (timeoutSeconds) => {
    const result = submitQueryRequestSchema.safeParse({
      ...validRequest,
      timeoutSeconds,
    })

    expect(result.success).toBe(false)
  })

  it('rejects SQL over the backend maximum length', () => {
    const result = submitQueryRequestSchema.safeParse({
      ...validRequest,
      sql: 'S'.repeat(MAX_SQL_LENGTH + 1),
    })

    expect(result.success).toBe(false)
  })

  it('rejects blank SQL and SQL containing null bytes', () => {
    expect(submitQueryRequestSchema.safeParse({ ...validRequest, sql: '   ' }).success).toBe(false)
    expect(submitQueryRequestSchema.safeParse({ ...validRequest, sql: 'SELECT \0' }).success).toBe(false)
  })

  it('rejects more than 50 parameters', () => {
    const parameters = Array.from({ length: 51 }, (_, index) => ({
      name: `@p${index}`,
      type: 'nvarchar',
      value: 'value',
    }))

    expect(submitQueryRequestSchema.safeParse({ ...validRequest, parameters }).success).toBe(false)
  })
})

describe('registerRequestSchema', () => {
  it('accepts a valid registration payload', () => {
    const result = registerRequestSchema.safeParse({
      email: 'reader@example.com',
      password: 'correct-horse-battery-staple',
      displayName: 'Reader',
    })

    expect(result.success).toBe(true)
  })

  it('rejects invalid email, short password, and short display name', () => {
    const result = registerRequestSchema.safeParse({
      email: 'not-an-email',
      password: 'short',
      displayName: 'A',
    })

    expect(result.success).toBe(false)
  })
})

describe('updateProfileRequestSchema', () => {
  it('accepts display name or email updates', () => {
    expect(updateProfileRequestSchema.safeParse({ displayName: 'Reader' }).success).toBe(true)
    expect(updateProfileRequestSchema.safeParse({ email: 'reader@example.com' }).success).toBe(true)
  })

  it('rejects empty payloads and invalid values', () => {
    expect(updateProfileRequestSchema.safeParse({}).success).toBe(false)
    expect(updateProfileRequestSchema.safeParse({ displayName: 'A' }).success).toBe(false)
    expect(updateProfileRequestSchema.safeParse({ email: 'not-an-email' }).success).toBe(false)
  })
})

describe('changePasswordRequestSchema', () => {
  it('accepts a valid password change payload', () => {
    const result = changePasswordRequestSchema.safeParse({
      currentPassword: 'correct-horse-battery-staple',
      newPassword: 'another-secure-password',
    })

    expect(result.success).toBe(true)
  })

  it('rejects missing current password and short new password', () => {
    expect(
      changePasswordRequestSchema.safeParse({
        currentPassword: '',
        newPassword: 'short',
      }).success,
    ).toBe(false)
  })
})

describe('workerHealthDashboardSchema', () => {
  it('accepts string enum probe statuses from the API', () => {
    const result = workerHealthDashboardSchema.safeParse({
      workers: [
        {
          nodeId: 'worker-node-01',
          address: 'worker',
          grpcPort: 5100,
          healthPort: 5101,
          shards: [0, 1, 2, 3],
          version: '1.0.0',
          liveStatus: 'Healthy',
          readyStatus: 'Healthy',
          grpcStatus: 'Healthy',
          liveLatencyMs: 12,
          readyLatencyMs: 15,
          grpcLatencyMs: 8,
          registeredInConsul: true,
        },
      ],
      healthyCount: 1,
      totalCount: 1,
      generatedAt: '2026-06-20T12:00:00.000Z',
    })

    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.workers[0]?.liveStatus).toBe(0)
      expect(result.data.workers[0]?.readyStatus).toBe(0)
      expect(result.data.workers[0]?.grpcStatus).toBe(0)
    }
  })

  it('accepts numeric probe statuses from mocked payloads', () => {
    const result = workerHealthDashboardSchema.safeParse({
      workers: [
        {
          nodeId: 'worker-node-01',
          address: 'worker',
          grpcPort: 5100,
          healthPort: 5101,
          shards: [0],
          version: '1.0.0',
          liveStatus: 1,
          readyStatus: 2,
          grpcStatus: 0,
          liveLatencyMs: null,
          readyLatencyMs: null,
          grpcLatencyMs: null,
          registeredInConsul: false,
        },
      ],
      healthyCount: 0,
      totalCount: 1,
      generatedAt: '2026-06-20T12:00:00.000Z',
    })

    expect(result.success).toBe(true)
  })
})
