# Testing

## Backend tests

### Prerequisites

- .NET 8 SDK
- Docker (optional, for Redis-backed integration tests and full stack verification)

### Run all backend tests

```bash
dotnet test
```

### Run by project

```bash
dotnet test tests/DistributedQuery.UnitTests
dotnet test tests/DistributedQuery.IntegrationTests
```

### Unit tests

`tests/DistributedQuery.UnitTests` covers pure logic with no external dependencies:

- Query parsing, validation, and shard routing
- Coordinator fan-out, routing, and result aggregation
- Infrastructure helpers (cache keys, gRPC adapters, auth utilities)
- API middleware, controllers, and authentication flows

Unit tests use xUnit, FluentAssertions, and NSubstitute.

### Integration tests

`tests/DistributedQuery.IntegrationTests` validates component interactions:

- In-process gRPC execution with SQLite shard databases
- End-to-end API to Coordinator to Worker query flows
- Redis cache round-trip when a local Redis instance is available on `localhost:6379`

Redis integration tests skip automatically when Redis is not reachable. Start Redis locally or via Compose to run them:

```bash
docker compose up -d redis
dotnet test tests/DistributedQuery.IntegrationTests
```

### Backend coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage reports are written under `TestResults/`.

## Frontend tests

### Prerequisites

- Node.js 20+
- Backend API (optional for E2E with `PLAYWRIGHT_MOCK_API=false`)

### Run unit and integration tests

```bash
cd frontend
npm install
npm test
```

Vitest covers API client logic (with MSW), Zod schemas, hooks, and component behavior.

### Run E2E tests

```bash
cd frontend
npm run test:e2e
```

By default, Playwright mocks `/queries` and `/health` so CI and local runs do not require the backend. To exercise the real API:

```bash
docker compose up -d api coordinator worker redis rabbitmq consul
cd frontend
PLAYWRIGHT_MOCK_API=false VITE_API_PROXY_TARGET=http://127.0.0.1:5281 npm run test:e2e
```

E2E runs with `VITE_AUTH_ENABLED=false` and starts the Vite dev server automatically.

## CI

No automated CI workflow is configured in this repository. Run tests locally using the commands above.

## Guidelines

- Place fast, isolated backend tests in `DistributedQuery.UnitTests`.
- Place multi-component backend tests in `DistributedQuery.IntegrationTests`.
- Use SQLite in-memory or file databases for worker execution tests.
- Do not depend on production credentials or external services in committed tests.
- Frontend API integration tests should use MSW unless explicitly testing against a live backend.
