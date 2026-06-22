# Deployment

## Docker Compose (recommended for local and demo environments)

The repository includes a full stack in `docker-compose.yml`:

- Application services: `api`, `coordinator`, `worker`
- Infrastructure: `redis`, `rabbitmq`, `consul`
- Observability: `jaeger`, `prometheus`, `grafana`

### Quick start

From the repository root:

```bash
./setup.sh
make up-detached
make wait-api
make ui
```

Or use a single command to start backend and web UI:

```bash
make dev
```

Manual equivalent:

```bash
docker compose up -d --build
curl http://localhost:5281/health/ready
cd frontend && npm install && npm run dev
```

### Service endpoints

| Service | URL |
|---|---|
| API | http://localhost:5281 |
| Coordinator | http://localhost:5200 |
| Worker health | http://localhost:5101/health/ready |
| RabbitMQ management | http://localhost:15672 |
| Consul UI | http://localhost:8500 |
| Jaeger UI | http://localhost:16686 |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3000 |

Swagger UI is available at `http://localhost:5281/swagger` when `ASPNETCORE_ENVIRONMENT=Development`.

### Demo accounts

The API seeds two development accounts on startup when running in Development (Docker Compose sets this by default):

| Role | Email | Password |
|---|---|---|
| Admin | `admin@example.com` | `ChangeMe-Admin-12` |
| Standard user | `user@example.com` | `ChangeMe-User-12` |

Sign in at http://localhost:5173/login/. See [configuration.md](configuration.md) for seed settings.

### Health checks

Compose uses service readiness endpoints:

- API: `GET /health/ready`
- Coordinator: `GET /health/ready`
- Worker: `GET /health/ready` on port 5101

The worker service sets `stop_grace_period: 35s` to allow drain and Consul deregistration before shutdown.

### Container images

Each host has a multi-stage Dockerfile:

- `src/DistributedQuery.Api/Dockerfile`
- `src/DistributedQuery.Coordinator/Dockerfile`
- `src/DistributedQuery.Worker/Dockerfile`

Images publish Release builds, run as the non-root `app` user, and expose only required ports.

## Running services individually

For local development without Compose, start infrastructure first:

```bash
docker compose up -d redis rabbitmq consul jaeger
```

Then run each host:

```bash
dotnet run --project src/DistributedQuery.Coordinator
dotnet run --project src/DistributedQuery.Worker
dotnet run --project src/DistributedQuery.Api
```

Ensure `CoordinatorClient:BaseUrl`, Redis, Consul, and RabbitMQ settings match your environment.

## Web UI

The React frontend in `frontend/` is not included in `docker-compose.yml`. Use `make dev` or `make ui` from the repository root (see [README](../README.md)).

For production, build static assets and serve them from a CDN or web server:

```bash
cd frontend
npm ci
npm run build
```

Deploy the `frontend/dist/` output as a static SPA with fallback routing to `index.html`. Place the UI on the same origin as the API or behind a reverse proxy that forwards API paths to the backend. The backend does not configure CORS for browser cross-origin requests.

See [frontend.md](frontend.md) for environment variables and authentication setup.

## Troubleshooting

| Problem | What to check |
|---|---|
| `docker compose` fails | Docker Desktop is running |
| API not ready | Run `docker compose ps` and wait for `api`, `coordinator`, and `worker` to be healthy |
| Frontend loads but login fails | Backend is up at http://localhost:5281/health/live; `frontend/.env` has `VITE_API_PROXY_TARGET=http://localhost:5281` |
| Redirect to login on every page | Sign in at `/login/` with a seeded account (see Demo accounts above) or register at `/signup/` |
| Port already in use | Stop other services on ports 5281 (API) or 5173 (frontend), or change the frontend port: `npm run dev -- --port 5174` |

## Production considerations

- Replace Compose defaults with environment-specific secrets and connection strings.
- Set stable RSA JWT keys for the API (`Authentication__JwtSigning__*`).
- Disable development account seeding (`Authentication__Seed__Enabled=false`) and use real identity management.
- Connect shard databases as described in [connecting-databases.md](connecting-databases.md).
- Run Redis, RabbitMQ, and Consul as managed or clustered services for high availability.
- Enable OTLP export and Prometheus scraping in your observability platform.
- Terminate TLS at a load balancer or ingress in front of the API.

Kubernetes manifests are not included in this repository. Add them when you need cluster orchestration, autoscaling, and managed secret distribution.

## Observability stack

Compose provisions:

- OTLP trace export to Jaeger (`OpenTelemetry:OtlpEndpoint=http://jaeger:4317`)
- Prometheus scraping via `infra/prometheus.yml`
- Grafana datasource and dashboard provisioning under `infra/grafana/`

Do not log SQL parameter values, credentials, or raw result rows.
