import { AppError } from './errors'

type SafeErrorContext = {
  route?: string
  queryId?: string
  status?: number
  code?: string
}

export interface CapturedErrorEvent {
  name: string
  message: string
  code?: string
  status?: number
  route?: string
  queryId?: string
}

export function captureError(error: unknown, context: SafeErrorContext = {}): CapturedErrorEvent {
  const event: CapturedErrorEvent = {
    name: error instanceof Error ? error.name : 'UnknownError',
    message: error instanceof Error ? error.message : 'Unknown error',
    code: context.code,
    status: context.status,
    route: context.route,
    queryId: context.queryId,
  }

  if (error instanceof AppError) {
    event.code = error.code
    event.status = error.status
  }

  if (typeof window !== 'undefined') {
    window.dispatchEvent(new CustomEvent<CapturedErrorEvent>('dqee:error', { detail: event }))
  }
  return event
}

export function generateTraceParent(): string {
  return `00-${randomHex(16)}-${randomHex(8)}-01`
}

function randomHex(byteCount: number): string {
  const bytes = new Uint8Array(byteCount)
  globalThis.crypto.getRandomValues(bytes)
  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')
}
