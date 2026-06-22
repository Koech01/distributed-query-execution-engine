# Architecture

## Overview

The Distributed Query Execution Engine (DQEE) accepts SQL over HTTP, splits queries into shard-targeted sub-queries, executes them in parallel on worker nodes, and merges partial results into a single response.

For a complete feature list, see [features.md](features.md).

The repository contains two deployable parts:

| Part | Location | Role |
|---|---|---|
| Backend | `src/`, `tests/` | API, coordinator, workers, and infrastructure integrations |
| Web UI | `frontend/` | React SPA for query execution, operations, and admin tools |

```
Web UI -> API -> Coordinator -> Workers (gRPC)
           |         |              |
           +-> Redis +-> Consul     +-> shard databases
           +-> RabbitMQ (async path)
```

Backend API contracts are documented in [api.md](api.md). For deeper backend detail, see [backend-architecture.md](backend-architecture.md). Web UI detail is in [frontend.md](frontend.md).

## Design principles

| Principle | Implication |
|---|---|
| Shared-nothing workers | Workers hold independent shards and do not communicate with each other |
| Externalized shared state | Plans, results, and async rendezvous state live in Redis |
| Fail partial, not total | Worker failures produce degraded results when policy allows |
| Parse once, cache the plan | Identical SQL and parameter types reuse cached query plans |
| Layered boundaries | Core defines contracts; Infrastructure implements I/O; hosts orchestrate |

## Solution layout

```
src/
  DistributedQuery.Core/            Domain models and interfaces (no NuGet dependencies)
  DistributedQuery.QueryParser/     SQL parsing and query planning
  DistributedQuery.Infrastructure/  Redis, gRPC, Consul, RabbitMQ, auth adapters
  DistributedQuery.Api/             Public HTTP API
  DistributedQuery.Coordinator/     Planning, fan-out, aggregation
  DistributedQuery.Worker/          Sub-query execution and health endpoints
tests/
  DistributedQuery.UnitTests/
  DistributedQuery.IntegrationTests/
```

### Dependency rules

- `Core` has zero project dependencies.
- `QueryParser` depends only on `Core`.
- `Infrastructure` depends on `Core` and `QueryParser`.
- `Api`, `Coordinator`, and `Worker` depend on `Core` and `Infrastructure` and never reference each other.

## Component responsibilities

### API (`DistributedQuery.Api`)

- Public HTTP surface (queries, auth, admin, health)
- JWT authentication and rate limiting
- Request validation and exception mapping
- Proxies query execution to the Coordinator over HTTP

### Coordinator (`DistributedQuery.Coordinator`)

- Loads or creates query plans via `QueryPlanningService`
- Resolves healthy workers through Consul (`INodeRegistry`)
- Fans out sub-queries in parallel (`FanOutService`)
- Merges partial results (`ResultAggregator`)
- Exposes internal HTTP endpoints used by the API client

### Worker (`DistributedQuery.Worker`)

- Executes sub-queries against configured shard databases
- Streams row chunks back to the Coordinator over gRPC
- Registers with Consul and exposes liveness/readiness HTTP endpoints

### Query parser (`DistributedQuery.QueryParser`)

- Parses T-SQL with Microsoft.SqlServer.TransactSql.ScriptDom
- Validates statement type, complexity, and blocked tokens
- Resolves shard targets from configuration
- Produces `QueryPlan` with sub-queries and merge instructions

### Infrastructure (`DistributedQuery.Infrastructure`)

- Implements Core interfaces for Redis, gRPC, Consul, MassTransit, and auth
- Contains transport adapters only; no query planning or merge logic

## Query lifecycle (synchronous path)

1. Client sends `POST /queries` with SQL and optional parameters.
2. API authenticates the request and forwards it to the Coordinator.
3. Coordinator checks the Redis plan cache. On miss, `IQueryPlanner` builds a plan and stores it.
4. Coordinator discovers healthy workers and maps each sub-query shard index to a node.
5. Coordinator dispatches sub-queries in parallel over gRPC.
6. Workers execute SQL locally and stream `PartialResult` chunks.
7. Coordinator merges rows (ORDER BY, aggregates, DISTINCT, LIMIT/OFFSET).
8. API returns HTTP 200, or HTTP 206 when the result is degraded.

## Execution modes

| Mode | Trigger | Transport | Notes |
|---|---|---|---|
| Synchronous | Default (`async: false`) | gRPC streaming | Primary interactive path |
| Streaming | `POST /queries/stream` | Server-Sent Events | Incremental, ordered, or buffered merge modes |
| Async | `async: true` | RabbitMQ + Redis | Client polls status and result endpoints |
| Plan inspection | `POST /queries/plan` | HTTP JSON | Planning only; no worker execution |

## Sharding model

Shard routing is configuration-driven through `ShardMap`:

- Tables declare a shard key, shard count, and strategy (`ConsistentHash` or `RangePartition`).
- Equality predicates on the shard key route to a single shard.
- Queries without a shard key predicate broadcast to all shards.
- Cross-shard JOINs are not supported.
- Shard assignment across workers is static in the current release.

For operator steps to connect shard databases, see [connecting-databases.md](connecting-databases.md).

## Resilience

- Polly retry and circuit breaker policies wrap synchronous worker calls.
- Default partial failure policy is `BestEffort`: return available data with `degraded: true`.
- `StrictAll` fails the query when any shard fails.
- Worker shutdown uses a configurable drain period before deregistration.

## Observability

All hosts register OpenTelemetry tracing and Prometheus metrics:

- W3C trace context propagates from API through Coordinator to Workers via gRPC metadata.
- Structured logs include `traceId` and `spanId` correlation fields.
- Prometheus scrapes `/metrics` on API, Coordinator, and Worker hosts.
- OTLP export is enabled when `OpenTelemetry:OtlpEndpoint` is configured.

## Supported SQL (current release)

- `SELECT` statements only
- `WHERE` with equality, range, and `IN` predicates
- `ORDER BY`, `TOP`/`LIMIT`, `GROUP BY`, `DISTINCT`
- Single-table queries within a shard
- Aggregates: `SUM`, `COUNT`, `AVG`, `MIN`, `MAX`, `COUNT(DISTINCT)`

Not supported: writes, cross-shard JOINs, subqueries, CTEs, window functions, and non-T-SQL dialects.
