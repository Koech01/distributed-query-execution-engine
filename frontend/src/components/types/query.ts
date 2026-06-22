export type FailurePolicy = 'BestEffort' | 'StrictAll'

export type QueryStatus = 'running' | 'completed'

export type ParameterType = string

export interface QueryParameterDto {
  name: string
  type: ParameterType
  value: string
}

export interface SubmitQueryRequest {
  sql: string
  parameters: QueryParameterDto[]
  timeoutSeconds?: number
  maxNodes?: number
  async?: boolean
  failurePolicy?: FailurePolicy
  queryId?: string
}

export interface QueryResult {
  queryId: string
  columns: string[]
  rows: string[][]
  rowCount: number
  totalShards: number
  successfulShards: number
  failedShards: number[]
  degraded: boolean
  degradationReason: string | null
  executionMs: number
  fromCache: boolean
}

export interface SubmitQueryResponse {
  queryId: string
  statusUrl: string | null
}

export interface QueryStatusResponse {
  queryId: string
  status: QueryStatus
  message: string | null
}

export interface ErrorResponse {
  error: string
  message: string
  details?: string[]
}

export interface HealthResponse {
  status: string
}

export type QueryStreamMode = 'incremental' | 'ordered' | 'buffered'

export interface QueryStreamMetadata {
  queryId: string
  totalShards: number
  streamMode: QueryStreamMode
}

export interface QueryStreamComplete {
  rowCount: number
  totalShards: number
  successfulShards: number
  failedShards: number[]
  degraded: boolean
  degradationReason: string | null
  executionMs: number
}

export type QueryStreamEvent =
  | { kind: 'metadata'; data: QueryStreamMetadata }
  | { kind: 'columns'; data: { columns: string[] } }
  | { kind: 'row'; data: { values: string[] } }
  | { kind: 'complete'; data: QueryStreamComplete }

export type AggregateFunction = 'Sum' | 'Count' | 'Avg' | 'Min' | 'Max' | 'CountDistinct'

export interface QueryPlanOrderByColumn {
  columnName: string
  descending: boolean
}

export interface QueryPlanAggregateOperation {
  function: AggregateFunction
  sourceColumn: string
  outputAlias: string
}

export interface QueryPlanMergeDetails {
  orderBy: QueryPlanOrderByColumn[]
  aggregates: QueryPlanAggregateOperation[]
  limit: number | null
  offset: number | null
  isDistinct: boolean
}

export interface QueryPlanSubQueryDetails {
  subQueryId: string
  shardIndex: number
  totalShards: number
  sql: string
}

export interface QueryPlanDetails {
  planId: string
  planHash: string
  fromCache: boolean
  targetingStrategy: string
  clusterShardCount: number
  subQueries: QueryPlanSubQueryDetails[]
  mergeInstructions: QueryPlanMergeDetails
  createdAt: string
}

export type SubmitQueryResult = QueryResult | SubmitQueryResponse
