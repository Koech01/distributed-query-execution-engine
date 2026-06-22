# Features

Complete feature overview for the Distributed Query Execution Engine. Use this page to understand what the system does before reading detailed docs.

For setup steps, see the [README](../README.md). For HTTP endpoints, see [api.md](api.md). For UI routes and behavior, see [frontend.md](frontend.md).

## At a glance

- Run **read-only SQL** across **sharded databases** from a web UI or HTTP API.
- The backend **splits one query** into shard-specific sub-queries, runs them **in parallel**, and **merges** the results.
- Users **sign in**, compose SQL, view results, and (with admin access) monitor the cluster.
- Operators **configure** shard databases and deployment settings. End users do not connect databases through the UI.

## Web UI

### Authentication

| Feature | Description |
|---|---|
| Email sign-up and login | Register or sign in with email and password |
| Google OAuth | Sign in with Google when enabled |
| GitHub OAuth | Sign in with GitHub when enabled |
| Route guards | Protected pages require a signed-in session |
| Pre-seeded demo accounts | Admin and standard user accounts created automatically in Development |

### Query console (`/query`)

| Feature | Description |
|---|---|
| SQL editor | CodeMirror editor for composing queries |
| Parameterized queries | Bind typed parameters to SQL |
| Synchronous execution | Run a query and wait for the full result |
| Async execution | Submit long-running queries and poll for completion |
| Streaming results | Receive rows incrementally via server-sent events |
| Query plan view | Inspect shard targeting without executing SQL |
| Result table | View columns and rows with degradation metadata when shards fail |
| Async deep links | Open `/query/:queryId` to return to an async result |

### Query history (`/history`)

| Feature | Description |
|---|---|
| Local history | Past queries stored in the browser (IndexedDB) |
| Metadata by default | Full SQL storage is optional via user preference |

### Operations (`/operations`)

| Feature | Description |
|---|---|
| API health cards | Quick view of backend health status |
| Observability links | Shortcuts to Grafana and Jaeger when deployed |

### Settings (`/settings`)

| Feature | Description |
|---|---|
| Profile management | Update display name and email |
| Change password | Update account password |
| Local preferences | UI preferences such as query history behavior |

### Admin (`/admin`, requires admin account)

| Feature | Description |
|---|---|
| Dashboard | Cluster statistics overview |
| Cache management | View plan cache stats and flush cache |
| Active queries | List and cancel in-flight queries |
| Worker status | View worker health and probe results |

## Backend and API

### Distributed query execution

| Feature | Description |
|---|---|
| Query planning | Parse SQL and build a shard-aware execution plan |
| Parallel fan-out | Dispatch sub-queries to workers over gRPC |
| Result merging | Combine rows, aggregates, ordering, and limits across shards |
| Plan caching | Reuse plans for identical SQL and parameter types (Redis) |
| Result caching | Cache completed results for idempotent retries (Redis) |

### Execution modes

| Mode | Description |
|---|---|
| Synchronous | Default interactive path (`POST /queries`) |
| Streaming | Incremental rows over SSE (`POST /queries/stream`) |
| Async | Background execution with status polling (`async: true`) |
| Plan only | Inspect targeting without running SQL (`POST /queries/plan`) |

### SQL support

| Supported | Not supported |
|---|---|
| `SELECT` only | Inserts, updates, deletes |
| `WHERE`, `ORDER BY`, `TOP`/`LIMIT`, `GROUP BY`, `DISTINCT` | Cross-shard joins |
| Aggregates (`SUM`, `COUNT`, `AVG`, `MIN`, `MAX`, `COUNT(DISTINCT)`) | Subqueries, CTEs, window functions |
| Parameterized queries | Non-T-SQL dialects |

See [architecture.md](architecture.md#supported-sql-current-release) for details.

### Sharding

| Feature | Description |
|---|---|
| Configurable shard map | Route queries by table, shard key, and shard count |
| Consistent hash routing | Hash-based shard selection (default demo strategy) |
| Range partition routing | Range-based shard selection when configured |
| Targeted or broadcast queries | Filter on shard key hits one shard; no filter hits all shards |
| SQLite and SQL Server shards | Workers connect to configured shard databases |

Operators connect databases through configuration. See [connecting-databases.md](connecting-databases.md).

### Resilience

| Feature | Description |
|---|---|
| Best-effort partial results | Return available data when some shards fail (HTTP 206) |
| Strict-all policy | Fail the query when any shard fails |
| Retry and circuit breaker | Polly policies on worker gRPC calls |
| Graceful worker drain | Configurable shutdown period before deregistration |

### Authentication and authorization

| Feature | Description |
|---|---|
| JWT sessions (RS256) | HttpOnly cookie sessions for browser clients |
| Email and password auth | Register, login, profile, password change, account deletion |
| OAuth (Google, GitHub) | Optional social login when configured |
| Scope-based access | `query:read` for queries; `query:admin` for admin endpoints |
| Rate limiting | Per-IP limits on login and registration; concurrent request limits |
| Input validation | SQL length, parameter count, and complexity limits |

See [security.md](security.md).

### Admin and health API

| Feature | Description |
|---|---|
| Admin stats and cache control | Dashboard data, cache stats, flush, active queries, cancel |
| Worker probes | Worker health status for operators |
| Liveness and readiness | `/health/live` and `/health/ready` on API, Coordinator, and Worker |
| JWKS | `GET /.well-known/jwks` for token verification |
| Swagger | Interactive API docs in Development |

See [api.md](api.md).

### Observability

| Feature | Description |
|---|---|
| OpenTelemetry tracing | W3C trace context across API, Coordinator, and Workers |
| Prometheus metrics | `/metrics` on all backend hosts |
| OTLP export | Optional export to Jaeger or other collectors |
| Structured logging | Trace and span correlation in logs |
| Grafana and Prometheus stack | Included in Docker Compose for local demos |

See [deployment.md](deployment.md).

## Setup and deployment

| Feature | Description |
|---|---|
| One-command local start | `./setup.sh` and `make dev` |
| Docker Compose stack | API, Coordinator, Worker, Redis, RabbitMQ, Consul, Jaeger, Prometheus, Grafana |
| Environment templates | `.env.example` and `frontend/.env.example` |
| Makefile shortcuts | `up`, `down`, `test`, `reset`, and related commands |

## Not included

The following are outside the current scope:

- Connecting a database from the web UI at runtime
- Write operations (insert, update, delete)
- Cross-shard joins
- Arbitrary multi-database ad hoc connections (only configured shards)
- Kubernetes manifests in this repository
- Built-in sample data loading on first startup (operators load shard data separately)

## Learn more

| Topic | Document |
|---|---|
| System design | [architecture.md](architecture.md) |
| Backend layers | [backend-architecture.md](backend-architecture.md) |
| Web UI detail | [frontend.md](frontend.md) |
| HTTP API | [api.md](api.md) |
| Connect shard databases | [connecting-databases.md](connecting-databases.md) |
| Settings and env vars | [configuration.md](configuration.md) |
| Hosting | [deployment.md](deployment.md) |
| Security | [security.md](security.md) |
| Tests | [testing.md](testing.md) |
