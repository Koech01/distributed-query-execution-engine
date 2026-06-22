# Backend Architecture

This document describes the backend solution: project boundaries, responsibilities, data flow, and integration points. For the HTTP API surface, see [api.md](api.md). For settings and environment variables, see [configuration.md](configuration.md).

## Overview

The backend is a .NET 8 solution that accepts SQL over HTTP, plans shard-targeted sub-queries, executes them on worker nodes over gRPC, and merges partial results. Three host processes run in production:

| Host | Port (Compose) | Responsibility |
|---|---|---|
| `DistributedQuery.Api` | 5281 | Public HTTP API, authentication, request validation |
| `DistributedQuery.Coordinator` | 5200 | Query planning, fan-out, result aggregation |
| `DistributedQuery.Worker` | 5100 (gRPC), 5101 (health) | Sub-query execution against shard databases |

Supporting libraries (`Core`, `QueryParser`, `Infrastructure`) contain no HTTP listeners.

## Layer diagram

```
                    +------------------+
                    | DistributedQuery |
                    |       .Api       |
                    +--------+---------+
                             | HTTP (internal)
                    +--------v---------+
                    | DistributedQuery |
                    |   .Coordinator   |
                    +--------+---------+
                             | gRPC
              +--------------+--------------+
              |                             |
     +--------v--------+           +--------v--------+
     | DistributedQuery|           | DistributedQuery|
     |     .Worker     |    ...    |     .Worker     |
     +--------+--------+           +--------+--------+
              |                             |
              +--------------+--------------+
                             |
        +--------------------+--------------------+
        |                    |                    |
   +----v----+          +-----v-----+       +------v------+
   |  Redis  |          |   Consul  |       |  RabbitMQ   |
   |  cache  |          | discovery |       | async path  |
   +---------+          +-----------+       +-------------+

   Shared libraries (referenced by all hosts):
   +------------------+     +------------------------+
   | QueryParser      |     | Infrastructure         |
   | (pure planning)  |     | Redis, gRPC, Consul,   |
   +--------+---------+     | MassTransit, auth I/O  |
            |               +------------+-----------+
            |                            |
   +--------v---------------------------v-----------+
   |              DistributedQuery.Core             |
   |         models, interfaces, exceptions         |
   +------------------------------------------------+
```

## DistributedQuery.Core

**Purpose:** Domain kernel and dependency inversion anchor. Zero NuGet dependencies.

**Key models:**

| Model | Role |
|---|---|
| `QueryRequest` | Inbound query with SQL, parameters, timeout, async flag |
| `QueryPlan` | Cached execution plan with sub-queries and merge instructions |
| `SubQuery` | Unit of work sent to one worker shard |
| `PartialResult` | Row chunks from a single shard |
| `QueryResult` | Merged result returned to clients |
| `NodeInfo` | Worker registration metadata from Consul |

**Key interfaces:**

| Interface | Implemented by |
|---|---|
| `IQueryPlanner` | `SqlQueryParser` (QueryParser) |
| `IQueryCache` | `RedisQueryCache` (Infrastructure) |
| `IWorkerClient` | `WorkerGrpcClient` (Infrastructure) |
| `IResultMerger` | `ResultAggregator` (Coordinator) |
| `INodeRegistry` | `ConsulNodeRegistry` (Infrastructure) |
| `IUserRepository` | `RedisUserRepository` (Infrastructure) |

**Messages:** `SubQueryDispatched`, `PartialResultReady`, and `QueryCompleted` support the async RabbitMQ path.

## DistributedQuery.QueryParser

**Purpose:** Transform SQL into a `QueryPlan` without I/O.

**Pipeline:**

```
SqlScriptParser -> QueryValidator -> AstVisitor
    -> ShardTargetResolver -> QuerySplitter -> SqlQueryParser
```

| Stage | Responsibility |
|---|---|
| `SqlScriptParser` | Parse T-SQL AST via ScriptDom |
| `QueryValidator` | Reject non-SELECT, blocked tokens, complexity limits |
| `AstVisitor` | Extract tables, predicates, aggregates, ORDER BY |
| `ShardTargetResolver` | Map predicates to shard indices using `ShardMap` config |
| `QuerySplitter` | Produce per-shard SQL and merge instructions |
| `PlanHashComputer` | Deterministic cache key shared with Redis cache layer |

**Output:** `QueryPlan` with `SubQueries[]`, `MergeInstructions` (ORDER BY, aggregates, LIMIT, DISTINCT), and `PlanHash`.

## DistributedQuery.Infrastructure

**Purpose:** All external system integrations. No query planning or merge logic.

| Area | Key types | External system |
|---|---|---|
| Caching | `RedisQueryCache`, `CacheKeyBuilder` | Redis (MessagePack serialization) |
| gRPC | `WorkerGrpcClient`, `QueryExecutionService` | Workers (streaming proto) |
| Discovery | `ConsulNodeRegistry`, `ConsulRegistration` | Consul |
| Messaging | `SubQueryPublisher`, `SubQueryConsumer`, `PartialResultConsumer` | RabbitMQ via MassTransit |
| Auth | `RedisUserRepository`, `RsaAuthTokenIssuer`, OAuth clients | Redis, external IdPs |
| Observability | `DqeeMetrics`, tracing interceptors | OpenTelemetry, Prometheus |
| Coordinator client | `CoordinatorHttpClient` | HTTP from Api to Coordinator |

## DistributedQuery.Api

**Purpose:** Thin HTTP host. Validates requests, authenticates callers, proxies to Coordinator.

**Controllers:**

| Controller | Routes | Notes |
|---|---|---|
| `QueryController` | `/queries`, `/queries/stream`, `/queries/plan` | Requires `query:read` |
| `AuthController` | `/auth/*` | Register, login, OAuth, token exchange |
| `AccountController` | `/auth/account/*` | Profile management |
| `AdminController` | `/admin/*` | Requires `query:admin` |
| `HealthController` | `/health/*` | Anonymous liveness and readiness |

**Middleware pipeline:** security headers, exception mapping, rate limiting, request validation, JWT authentication.

**Does not:** execute SQL, plan queries, or call workers directly.

## DistributedQuery.Coordinator

**Purpose:** Orchestrate query execution end to end.

| Service | Responsibility |
|---|---|
| `QueryPlanningService` | Redis plan cache check, invoke `IQueryPlanner`, store plan |
| `WorkerRouter` | Map shard indices to healthy `NodeInfo` from Consul |
| `FanOutService` | Parallel gRPC dispatch with concurrency limits and cancellation |
| `ResultAggregator` | Merge ORDER BY, aggregates, DISTINCT, LIMIT across shards |
| `CoordinatorService` | Wire planning, fan-out, aggregation for sync, stream, and async paths |
| `ActiveQueryRegistry` | Track in-flight queries for admin cancel |
| `CoordinatorAdminService` | Aggregate stats, worker health probes, dashboard data |

**Internal HTTP endpoints** under `/internal/v1/*` are consumed by `CoordinatorHttpClient` in the Api host.

**Resilience:** Polly retry and circuit breaker wrap synchronous worker gRPC calls.

## DistributedQuery.Worker

**Purpose:** Execute sub-queries against local shard databases.

| Component | Responsibility |
|---|---|
| `ShardExecutor` | Run SQL via ADO.NET, stream rows in configurable chunks |
| `WorkerGrpcService` | gRPC server adapter, maps domain exceptions to gRPC status |
| `WorkerRegistration` | Consul register/deregister with drain period on shutdown |
| `WorkerHealthService` | `/health/live`, `/health/ready`, shard DB ping |

Workers receive `SubQuery` objects over gRPC and return streaming `PartialResult` chunks. They never contact other workers or the Coordinator except via the inbound gRPC call.

## Query lifecycle (synchronous)

```
1. POST /queries -> Api validates JWT and request body
2. Api -> CoordinatorHttpClient -> CoordinatorService.ExecuteQueryAsync
3. QueryPlanningService: Redis plan cache hit or SqlQueryParser plan + cache store
4. WorkerRouter: Consul GetHealthyNodes, map SubQuery.ShardIndex -> NodeInfo
5. FanOutService: parallel IWorkerClient.ExecuteAsync per sub-query
6. Worker: ShardExecutor streams rows -> PartialResult chunks over gRPC
7. ResultAggregator.Merge: apply merge instructions
8. Optional: Redis result cache store
9. Api returns 200 (or 206 if degraded)
```

## Async execution path

When `async: true`:

1. Api returns HTTP 202 with `statusUrl`.
2. Coordinator publishes `SubQueryDispatched` messages via MassTransit.
3. Workers consume and execute; publish `PartialResultReady`.
4. Coordinator `PartialResultConsumer` collects results in Redis.
5. Client polls `GET /queries/{id}/status` and fetches `GET /queries/{id}/result`.

## Caching

| Cache | Key pattern | TTL (default) | Content |
|---|---|---|---|
| Plan | `plan::{hash}` | 3600s | Serialized `QueryPlan` |
| Result | `result::{queryId}` | 300s | Serialized `QueryResult` |

Plan keys use parameter type signatures, not values. Result keys use query id for idempotent retries.

## Sharding

Configured in `ShardMap` (see [configuration.md](configuration.md)):

- `ConsistentHash` or `RangePartition` strategy per table
- Equality on shard key routes to one shard
- Missing shard key predicate broadcasts to all shards
- Compose demo uses table `orders` with shard key `customer_id` and 4 shards backed by SQLite files in the worker container

## Observability

Each host exposes `/metrics` for Prometheus and optional OTLP export to Jaeger. W3C trace context propagates from Api through Coordinator to Workers via gRPC metadata and MassTransit message headers.

## Testing structure

| Project | Scope |
|---|---|
| `DistributedQuery.UnitTests` | Pure logic, mocked interfaces, in-process gRPC |
| `DistributedQuery.IntegrationTests` | Api-Coordinator-Worker E2E with SQLite shards, optional Redis |

See [testing.md](testing.md).
