# Distributed Query Execution Engine

A distributed SQL query execution engine for .NET 8 with a React web UI. The backend splits `SELECT` queries across data shards, executes sub-queries in parallel on worker nodes, and merges partial results. The frontend provides query execution, operations visibility, and admin tools.

See [docs/features.md](docs/features.md) for a complete feature list.

## Prerequisites

| Tool | Version | Required for |
|---|---|---|
| Docker Desktop | recent | Backend stack (recommended path) |
| Node.js | 20+ | Web UI development |
| .NET SDK | 8.0 | Backend development and tests |

Docker alone is enough to run the backend. Node.js and .NET are needed for active development and tests.

## Quick start

> **Important:** Start Docker Desktop before running setup. If Docker is not running, builds and health checks will fail.

```bash
git clone <repository-url>
cd distributed-query-execution-engine

# Create .env files and verify Docker
./setup.sh

# Start backend stack and web UI
make dev
```

Open http://localhost:5173 and sign in at `/login/` with one of the pre-seeded development accounts:

| Role | Email | Password |
|---|---|---|
| Admin | `admin@example.com` | `ChangeMe-Admin-12` |
| Standard user | `user@example.com` | `ChangeMe-User-12` |

These accounts are created automatically when the API starts in Development (for example via `make dev` or Docker Compose). They are for local evaluation only. You can still register additional accounts at `/signup/`.

Use `Ctrl+C` to stop the web UI. Stop the backend with `make down`.

### Alternative commands

```bash
make up-detached    # backend only, runs in background
make ui             # web UI only (backend must already be running)
make wait-api       # block until http://localhost:5281/health/ready returns 200
```

## Project structure

```
distributed-query-execution-engine/
├── src/                  Backend services (.NET 8)
├── tests/                Backend unit and integration tests
├── frontend/             React web UI (Vite)
├── docs/                 Documentation
├── docker-compose.yml    Backend and infrastructure stack
├── .env.example          Environment variable reference
├── setup.sh              First-time bootstrap
└── Makefile              Common development commands
```

## Service URLs

| Service | URL |
|---|---|
| Web UI (dev) | http://localhost:5173 |
| API | http://localhost:5281 |
| Swagger (Development) | http://localhost:5281/swagger |
| Coordinator | http://localhost:5200 |
| Grafana | http://localhost:3000 |
| Jaeger | http://localhost:16686 |
| RabbitMQ management | http://localhost:15672 |
| Consul UI | http://localhost:8500 |

## Development workflow

The Vite dev server proxies `/queries`, `/health`, `/auth`, and `/admin` to the API at `http://localhost:5281`.

```bash
make setup-dev   # install npm and dotnet dependencies
make up-detached # start backend stack in Docker
make ui          # start web UI dev server
make logs        # follow container logs
make ps          # show container status
make down        # stop containers
make reset       # stop containers and remove volumes
```

For local backend development without full Compose:

```bash
docker compose up -d redis rabbitmq consul jaeger
dotnet run --project src/DistributedQuery.Coordinator
dotnet run --project src/DistributedQuery.Worker
dotnet run --project src/DistributedQuery.Api
```

## Configuration

`./setup.sh` creates `.env` and `frontend/.env` from templates if they do not exist. Defaults are intended for local Docker evaluation.

| Variable | Description |
|---|---|
| `VITE_API_PROXY_TARGET` | Backend URL for the Vite dev proxy |
| `VITE_AUTH_ENABLED` | Set `false` to bypass UI auth guards locally |
| `Redis__ConnectionString` | Redis connection (local `dotnet run`) |
| `CoordinatorClient__BaseUrl` | Coordinator URL (local `dotnet run`) |
| `Authentication__JwtSigning__PrivateKeyPem` | RSA key for production JWT signing |

See [`.env.example`](.env.example) for the full list and [docs/configuration.md](docs/configuration.md) for backend settings detail. Operators connecting real shard databases should follow [docs/connecting-databases.md](docs/connecting-databases.md).

## Docker resources

| Resource | Name / value |
|---|---|
| Compose project | `distributed-query-execution-engine` |
| API container port | `5281` (host) -> `8080` (container) |
| Worker gRPC | `5100`, health `5101` |
| Redis volume | `redis-data` |
| RabbitMQ volume | `rabbitmq-data` |

```bash
make up-detached   # start stack
make logs          # follow logs
make down          # stop stack
make reset         # stop and remove volumes
```

## Testing

```bash
make test              # backend + frontend unit tests
make test-backend      # dotnet test
make test-frontend     # vitest in frontend/
cd frontend && npm run test:e2e   # Playwright
```

See [docs/testing.md](docs/testing.md) for details.

## Architecture

```
Web UI -> API -> Coordinator -> Workers (gRPC)
           |         |              |
           +-> Redis +-> Consul     +-> shard databases
           +-> RabbitMQ (async)
```

| Document | Description |
|---|---|
| [docs/features.md](docs/features.md) | Complete feature list |
| [docs/architecture.md](docs/architecture.md) | System design and query lifecycle |
| [docs/backend-architecture.md](docs/backend-architecture.md) | Backend layers and data flow |
| [docs/frontend.md](docs/frontend.md) | Web UI structure and auth |
| [docs/api.md](docs/api.md) | HTTP API reference |
| [docs/deployment.md](docs/deployment.md) | Docker Compose and hosting |
| [docs/configuration.md](docs/configuration.md) | Settings and secrets |
| [docs/security.md](docs/security.md) | Security model |
| [docs/testing.md](docs/testing.md) | Test suites |

Full index: [docs/README.md](docs/README.md)

## Security

Report vulnerabilities as described in [SECURITY.md](SECURITY.md).

## License

MIT. See [LICENSE](LICENSE).
