import type { ZodIssue } from 'zod'

import type { SubmitQueryRequest } from '@/components/types'
import { submitQueryRequestSchema } from '@/lib/schemas'

import type { ParameterFieldErrors } from './ParameterEditor'

export interface QueryFormErrors {
  sql?: string
  timeoutSeconds?: string
  maxNodes?: string
  parameters?: Record<number, ParameterFieldErrors>
  form?: string
}

export type QueryFormValidationResult =
  | { success: true; data: SubmitQueryRequest }
  | { success: false; errors: QueryFormErrors }

export function validateQueryForm(request: SubmitQueryRequest): QueryFormValidationResult {
  const parsed = submitQueryRequestSchema.safeParse(request)

  if (parsed.success) {
    return { success: true, data: parsed.data }
  }

  return {
    success: false,
    errors: mapZodIssuesToFormErrors(parsed.error.issues),
  }
}

function mapZodIssuesToFormErrors(issues: ZodIssue[]): QueryFormErrors {
  const errors: QueryFormErrors = {}

  for (const issue of issues) {
    const [root, second, third] = issue.path

    if (root === 'sql' && !errors.sql) {
      errors.sql = issue.message
      continue
    }

    if (root === 'timeoutSeconds' && !errors.timeoutSeconds) {
      errors.timeoutSeconds = issue.message
      continue
    }

    if (root === 'maxNodes' && !errors.maxNodes) {
      errors.maxNodes = issue.message
      continue
    }

    if (root === 'parameters' && typeof second === 'number' && typeof third === 'string') {
      const parameterIndex = second
      const field = third as keyof ParameterFieldErrors
      errors.parameters ??= {}
      errors.parameters[parameterIndex] ??= {}
      if (!errors.parameters[parameterIndex][field]) {
        errors.parameters[parameterIndex][field] = issue.message
      }
      continue
    }

    if (!errors.form) {
      errors.form = issue.message
    }
  }

  return errors
}

export function isQueryResult(value: unknown): value is import('@/components/types').QueryResult {
  return (
    typeof value === 'object' &&
    value !== null &&
    'columns' in value &&
    'rows' in value &&
    'queryId' in value
  )
}
