# Configuration

## Environment files

| File | Purpose |
|---|---|
| [`.env.example`](../.env.example) | Full variable reference for backend and frontend |
| `.env` | Local overrides (created by `./setup.sh`; not committed) |
| [`frontend/.env.example`](../frontend/.env.example) | Frontend-only template |
| `frontend/.env` | Frontend local settings (created by `./setup.sh`; not committed) |

Run `./setup.sh` or `make setup` to create `.env` and `frontend/.env` from the templates.

Docker Compose embeds development defaults in `docker-compose.yml`. Use environment variables to override settings when running hosts with `dotnet run` or in production deployments.

## Configuration sources

.NET loads settings in this order (later sources override earlier ones):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables
4. `dotnet user-secrets` (local development only)
5. Command-line arguments

Never commit secrets. Use environment variables or a secret store in production.

Environment variable names use `__` as the section separator. For example, `Redis__Password` maps to `Redis:Password`.

## Shared infrastructure

| Setting | Default (local) | Description |
|---|---|---|
| `Redis:ConnectionString` | `localhost:6379,abortConnect=false` | Redis connection string |
| `Redis:InstanceName` | `dqee:` | Key prefix for cache entries |
| `Redis:Password` | unset | Redis password (secret) |
| `Consul:Address` | `http://localhost:8500` | Consul HTTP API |
| `Consul:Token` | unset | Consul ACL token (secret) |
| `RabbitMq:Host` | `localhost` | RabbitMQ host |
| `RabbitMq:VirtualHost` | `/` | RabbitMQ virtual host |
| `RabbitMq:Username` | `guest` | RabbitMQ username |
| `RabbitMq:Password` | unset | RabbitMQ password (secret) |
| `OpenTelemetry:ServiceName` | per host | Service name in traces and metrics |
| `OpenTelemetry:OtlpEndpoint` | empty | OTLP exporter endpoint (for example `http://jaeger:4317`) |

## API settings

| Setting | Default | Description |
|---|---|---|
| `Authentication:Enabled` | `true` | Enable JWT authentication |
| `Authentication:UseLocalJwtIssuer` | `true` | Issue and validate tokens locally |
| `Authentication:JwtSigning:Issuer` | `http://localhost:5281/` | JWT issuer URL |
| `Authentication:JwtSigning:Audience` | `dqee-api` | JWT audience |
| `Authentication:JwtSigning:PrivateKeyPem` | unset | RSA private key PEM (required outside Development for token issuance) |
| `Authentication:JwtSigning:PublicKeyPem` | unset | RSA public key PEM (optional when private key is set) |
| `Authentication:Email:Enabled` | `true` | Enable email and password auth |
| `Authentication:Email:MinPasswordLength` | `12` | Minimum password length |
| `Authentication:Email:LockoutThreshold` | `5` | Failed login attempts before lockout |
| `Authentication:Email:LockoutDurationMinutes` | `15` | Lockout duration |
| `Authentication:Google:Enabled` | `false` | Enable Google OAuth |
| `Authentication:GitHub:Enabled` | `false` | Enable GitHub OAuth |
| `Authentication:Frontend:CallbackUrl` | `http://localhost:5173/auth/callback` | Frontend OAuth callback URL |
| `Authentication:Seed:Enabled` | `true` in Development only | Create demo accounts on API startup |
| `Authentication:Seed:Admin:*` | see below | Admin demo account (Development only) |
| `Authentication:Seed:User:*` | see below | Standard demo account (Development only) |
| `Api:MaxSqlLengthChars` | `10000` | Maximum SQL length |
| `Api:MaxParameters` | `50` | Maximum bound parameters per query |
| `Api:MinTimeoutSeconds` | `1` | Minimum query timeout |
| `Api:MaxTimeoutSeconds` | `120` | Maximum query timeout |
| `CoordinatorClient:BaseUrl` | `http://localhost:5200` | Coordinator HTTP base URL |
| `CoordinatorClient:RequestTimeoutMs` | `120000` | Coordinator client timeout |
| `RateLimiting:MaxConcurrentRequests` | `200` | Concurrent request limit |
| `RateLimiting:QueueLimit` | `50` | Request queue limit |

In Development, the API generates an ephemeral RSA key pair when JWT signing keys are not configured. Production deployments must set `Authentication__JwtSigning__PrivateKeyPem` and `Authentication__JwtSigning__PublicKeyPem`.

### Development demo accounts

When `ASPNETCORE_ENVIRONMENT=Development` and `Authentication:Seed:Enabled=true`, the API creates two email accounts on startup if they do not already exist:

| Role | Email | Password | Scopes |
|---|---|---|---|
| Admin | `admin@example.com` | `ChangeMe-Admin-12` | `query:read`, `query:admin` |
| Standard user | `user@example.com` | `ChangeMe-User-12` | `query:read` |

Defaults are defined in `src/DistributedQuery.Api/appsettings.Development.json`. Override with environment variables such as `Authentication__Seed__Admin__Email`.

Seeding is disabled outside Development and when `Authentication:Seed:Enabled=false`. Do not enable seeding in production or shared environments.

## Coordinator settings

| Setting | Default | Description |
|---|---|---|
| `Coordinator:DefaultQueryTimeoutMs` | `30000` | Default query timeout |
| `Coordinator:MaxQueryTimeoutMs` | `120000` | Maximum allowed timeout |
| `Coordinator:PlanCacheTtlSeconds` | `3600` | Plan cache TTL |
| `Coordinator:ResultCacheTtlSeconds` | `300` | Result cache TTL |
| `Coordinator:PartialFailurePolicy` | `BestEffort` | `BestEffort` or `StrictAll` |
| `Coordinator:MinimumShardCoverage` | `0.8` | Minimum shard success ratio |
| `Coordinator:FanOut:MaxConcurrentWorkerCalls` | `50` | Fan-out concurrency limit |
| `Coordinator:FanOut:PerWorkerTimeoutMs` | `25000` | Per-worker call timeout |
| `Coordinator:Resilience:RetryCount` | `3` | gRPC retry attempts |
| `Coordinator:Resilience:RetryBaseDelayMs` | `100` | Retry base delay |
| `Coordinator:Resilience:CircuitBreakerFailureThreshold` | `5` | Breaker failure threshold |
| `Coordinator:Resilience:CircuitBreakerSamplingDurationSeconds` | `30` | Breaker sampling window |
| `Coordinator:Resilience:CircuitBreakerBreakDurationSeconds` | `15` | Breaker open duration |
| `ShardMap:Tables:{table}:ShardKey` | per table | Column used for shard routing |
| `ShardMap:Tables:{table}:ShardCount` | per table | Number of shards |
| `ShardMap:Tables:{table}:Strategy` | `ConsistentHash` | `ConsistentHash` or `RangePartition` |
| `Messaging:EnableCoordinatorConsumer` | `true` | Enable coordinator MassTransit consumer |
| `Messaging:EnableWorkerConsumer` | `false` | Enable worker MassTransit consumer |
| `WorkerRegistration:Enabled` | `false` | Register coordinator itself with Consul |

## Worker settings

| Setting | Default | Description |
|---|---|---|
| `Worker:NodeId` | `worker-node-01` | Stable worker identifier |
| `Worker:Address` | `127.0.0.1` | Address registered in Consul |
| `Worker:GrpcPort` | `5100` | gRPC listen port |
| `Worker:HealthPort` | `5101` | HTTP health and metrics port |
| `Worker:ShardIndices` | `[0]` | Shard indices owned by this worker |
| `Worker:Shards:{index}` | per shard | Database connection string (secret) |
| `Worker:Consul:Enabled` | `false` | Register worker with Consul |

See [connecting-databases.md](connecting-databases.md) for a step-by-step operator guide.
| `Worker:Execution:StreamChunkSize` | `500` | Rows per gRPC chunk |
| `Worker:Execution:CommandTimeoutSeconds` | `25` | SQL command timeout |
| `Worker:Execution:MaxConcurrentQueries` | `10` | Concurrent sub-query limit |
| `Worker:Execution:DrainTimeoutSeconds` | `15` | Graceful shutdown drain period |

## Local development secrets

```bash
# API JWT signing (optional in Development; auto-generated if omitted)
cd src/DistributedQuery.Api
dotnet user-secrets set "Authentication:JwtSigning:PrivateKeyPem" "$(openssl genrsa 2048)"
```

For worker shard connection strings, see [connecting-databases.md](connecting-databases.md).

## Validation

Options classes use data annotations and `ValidateOnStart()`. Missing or invalid configuration fails at startup rather than on the first request.
