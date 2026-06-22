import type { ActiveQueryKind, WorkerProbeStatus } from '@/components/types/admin'

export const ACTIVE_QUERY_KIND_LABELS: Record<ActiveQueryKind, string> = {
  0: 'Sync',
  1: 'Stream',
  2: 'Async',
}

export const WORKER_PROBE_STATUS_LABELS: Record<WorkerProbeStatus, string> = {
  0: 'Healthy',
  1: 'Unhealthy',
  2: 'Unreachable',
}

export function formatActiveQueryKind(kind: ActiveQueryKind): string {
  return ACTIVE_QUERY_KIND_LABELS[kind] ?? `Unknown (${kind})`
}

export function formatWorkerProbeStatus(status: WorkerProbeStatus): string {
  return WORKER_PROBE_STATUS_LABELS[status] ?? `Unknown (${status})`
}

export function formatElapsedSince(startedAt: string, now: Date = new Date()): string {
  const started = new Date(startedAt)

  if (Number.isNaN(started.getTime())) {
    return 'Unknown'
  }

  const elapsedSeconds = Math.max(0, Math.floor((now.getTime() - started.getTime()) / 1_000))

  if (elapsedSeconds < 60) {
    return `${elapsedSeconds}s`
  }

  const minutes = Math.floor(elapsedSeconds / 60)
  const seconds = elapsedSeconds % 60

  if (minutes < 60) {
    return seconds > 0 ? `${minutes}m ${seconds}s` : `${minutes}m`
  }

  const hours = Math.floor(minutes / 60)
  const remainingMinutes = minutes % 60
  return remainingMinutes > 0 ? `${hours}h ${remainingMinutes}m` : `${hours}h`
}
