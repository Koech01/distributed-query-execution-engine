/** Normalized ActiveQueryKind used in UI code (0=Sync, 1=Stream, 2=Async). */
export type ActiveQueryKind = 0 | 1 | 2

/** Normalized WorkerProbeStatus used in UI code (0=Healthy, 1=Unhealthy, 2=Unreachable). */
export type WorkerProbeStatus = 0 | 1 | 2

export interface AdminDashboardStats {
  activeQueries: number
  healthyWorkers: number
  totalWorkers: number
  planCacheEntries: number
  resultCacheEntries: number
  asyncQueryStatusEntries: number
  generatedAt: string
}

export interface AdminCacheStats {
  planCacheEntries: number
  resultCacheEntries: number
  asyncQueryStatusEntries: number
  generatedAt: string
}

export interface AdminCacheFlushRequest {
  planHash?: string | null
}

export interface AdminCacheFlushResult {
  deletedPlanEntries: number
  scope: string
  flushedAt: string
}

export interface ActiveQuerySummary {
  queryId: string
  kind: ActiveQueryKind
  planHash: string
  subQueryCount: number
  startedAt: string
  cancellationRequested: boolean
}

export interface ActiveQueryPage {
  queries: ActiveQuerySummary[]
  totalCount: number
  limit: number
  offset: number
}

export interface CancelQueryResult {
  queryId: string
  found: boolean
  cancellationRequested: boolean
  message: string
}

export interface WorkerHealthEntry {
  nodeId: string
  address: string
  grpcPort: number
  healthPort: number
  shards: number[]
  version: string
  liveStatus: WorkerProbeStatus
  readyStatus: WorkerProbeStatus
  grpcStatus: WorkerProbeStatus
  liveLatencyMs: number | null
  readyLatencyMs: number | null
  grpcLatencyMs: number | null
  registeredInConsul: boolean
}

export interface WorkerHealthDashboard {
  workers: WorkerHealthEntry[]
  healthyCount: number
  totalCount: number
  generatedAt: string
}
