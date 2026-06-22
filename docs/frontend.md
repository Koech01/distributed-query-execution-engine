# Frontend

The web UI is a React single-page application in `frontend/`. It provides query execution, async polling, local query history, operations visibility, account settings, and admin tools against the backend API documented in [api.md](api.md).

## Purpose

The frontend is the primary operator interface for the Distributed Query Execution Engine. It lets authenticated users compose and run distributed SQL queries, inspect results and degradation metadata, manage async executions, and (with admin scope) monitor cluster health and cache state.

For a full feature list across the web UI and backend, see [features.md](features.md).

## Design principles

| Principle | Implication |
|---|---|
| Backend contracts are authoritative | TypeScript types and Zod schemas mirror backend DTOs |
| Centralized API access | All HTTP calls go through `src/lib/api.ts` |
| Cookie-based sessions | JWTs live in HttpOnly cookies; never in browser storage |
| Feature-oriented structure | Components grouped by user-facing capability |
| Accessibility first | WCAG 2.1 AA patterns on interactive surfaces |
| Minimal global state | React state and hooks; no Redux or Zustand |

## Technology stack

| Category | Technology |
|---|---|
| Framework | React 19, TypeScript (strict) |
| Build | Vite (rolldown-vite), React Compiler |
| UI | shadcn/ui, Radix UI, Tailwind CSS 4 |
| Routing | React Router 7 |
| Tables | TanStack Table and TanStack Virtual |
| Validation | Zod 4 |
| SQL editor | CodeMirror 6 |
| Unit tests | Vitest, Testing Library, MSW |
| E2E tests | Playwright |

## Application structure

```
frontend/src/
  App.tsx / AppRoutes.tsx     Route definitions and providers
  components/
    Auth/                     Login, signup, OAuth callback, guards
    QueryConsole/             SQL editor, execution, streaming, plan panel
    QueryHistory/             IndexedDB-backed local history
    Operations/               API health and observability links
    Settings/                 Account profile and local preferences
    Admin/                    Dashboard, cache flush, active queries, workers
    Home/                     Authenticated shell (sidebar and header)
    ui/                       Shared shadcn/ui primitives
  hooks/                      Auth, polling, history, preferences
  lib/
    api.ts                    Centralized API client
    auth.ts                   Session user state (memory only)
    schemas.ts                Zod validation
    errors.ts                 Typed HTTP error mapping
    observability.ts          Client telemetry helpers
```

## Domain boundaries

```
+------------------------------------------------------------------+
|                         Web UI                                   |
+----------+-------------+-------------+-------------+-------------+
|   Auth   |    Query    | Operations  |   Settings  |    Admin    |
+----------+-------------+-------------+-------------+-------------+
| login    | SQL editor  | health cards| profile     | dashboard   |
| signup   | sync/async  | Grafana link| preferences | cache flush |
| OAuth    | streaming   | Jaeger link |             | workers     |
| guards   | plan view   |             |             | active q    |
+----------+-------------+-------------+-------------+-------------+
                              |
                    lib/api.ts (single HTTP layer)
                              |
                    Backend API (see api.md)
```

Domains must not import each other's page components. Shared UI lives in `components/ui/`.

## Routing

| Route | Page | Guard |
|---|---|---|
| `/login/`, `/signup/` | Auth pages | Public |
| `/auth/callback` | OAuth exchange | Public |
| `/query` | Query console | `query:read` |
| `/query/:queryId` | Async result deep link | `query:read` |
| `/history` | Local query history | `query:read` |
| `/operations` | Health and observability | `query:read` |
| `/settings` | Account and preferences | `query:read` |
| `/admin`, `/admin/cache` | Admin tools | `query:admin` |

Unauthenticated users redirect to `/login/`. Non-admin users cannot access `/admin/*`.

## Authentication flow

The UI uses HttpOnly cookie sessions set by the backend (`dqee_access_token`).

```
Signup or login
  -> POST /auth/register or /auth/login
  -> backend Set-Cookie (HttpOnly)
  -> GET /auth/account (bootstrap session user in memory)
  -> redirect to /query

OAuth
  -> GET /auth/google/login or /auth/github/login
  -> provider callback -> /auth/callback?exchangeCode=...
  -> POST /auth/token/exchange
  -> GET /auth/account
  -> redirect to return URL
```

Session user claims (scopes, email, display name) are kept in memory for route guards. The frontend never reads the JWT from JavaScript.

When `VITE_AUTH_ENABLED=false`, guards are bypassed for local development.

## API client pipeline

```
Component
  -> lib/schemas.ts (Zod validate outbound request)
  -> lib/api.ts (fetch with credentials: 'include', traceparent header)
  -> Vite dev proxy -> backend API
  -> lib/errors.ts (map status to AppError)
  -> Component (render result or error UI)
```

| Module | Endpoints |
|---|---|
| `queryApi` | `/queries`, `/queries/stream`, `/queries/plan`, status, result |
| `healthApi` | `/health/live`, `/health/ready` |
| `authApi` | `/auth/login`, `/auth/register`, `/auth/token/exchange` |
| `accountApi` | `/auth/account/*` |
| `adminApi` | `/admin/*` |

The backend does not configure CORS. Development uses the Vite proxy (default). Production requires same-origin deployment or a reverse proxy.

## Query console data flow

**Synchronous execution:**

```
SqlEditor + ParameterEditor + QueryOptionsPanel
  -> validate-query-form (Zod)
  -> queryApi.submit({ async: false })
  -> 200/206: QueryResultTable + DegradationBanner if needed
  -> useLocalQueryHistory.add (metadata only by default)
```

**Async execution:**

```
queryApi.submit({ async: true })
  -> 202 Accepted
  -> useQueryPoll: GET /queries/{id}/status (bounded backoff)
  -> on completed: GET /queries/{id}/result
  -> QueryResultTable
```

**Streaming:**

```
queryApi.streamEvents
  -> SSE events: metadata, columns, row, complete
  -> incremental QueryResultTable updates
```

## State management

| Concern | Mechanism | Storage |
|---|---|---|
| Theme | `ThemeProvider` (next-themes) | localStorage |
| Session user | `AuthProvider` | Memory (from `/auth/account`) |
| Query form | `useState` in QueryConsolePage | Page scope |
| Async polling | `useQueryPoll` | Hook with cancellation on unmount |
| Query history | `useLocalQueryHistory` | IndexedDB (metadata; SQL optional) |
| Preferences | `usePreferences` | localStorage |

## Environment variables

Copy `frontend/.env.example` to `frontend/.env`, or run `./setup.sh` from the repository root.

| Variable | Default | Description |
|---|---|---|
| `VITE_API_BASE_URL` | `same-origin` | API base URL; use `same-origin` with dev proxy |
| `VITE_API_PROXY_TARGET` | `http://localhost:5281` | Vite proxy target |
| `VITE_DEV_USE_PROXY` | `true` | Route API calls through Vite dev server |
| `VITE_AUTH_ENABLED` | `true` | Set `false` to bypass auth guards locally |
| `VITE_OAUTH_GOOGLE_ENABLED` | `true` | Hide Google sign-in when backend disables it |
| `VITE_OAUTH_GITHUB_ENABLED` | `true` | Hide GitHub sign-in when backend disables it |
| `VITE_GRAFANA_URL` | empty | Grafana link on Operations page |
| `VITE_JAEGER_URL` | empty | Jaeger link on Operations page |

Full variable reference including backend settings: [configuration.md](configuration.md) and the repository root `.env.example`.

## Local development

```bash
# From repository root (recommended)
./setup.sh
make dev

# Or manually
docker compose up -d --build
cd frontend && npm install && npm run dev
```

Open http://localhost:5173 and sign in at `/login/` with a pre-seeded development account:

| Role | Email | Password |
|---|---|---|
| Admin | `admin@example.com` | `ChangeMe-Admin-12` |
| Standard user | `user@example.com` | `ChangeMe-User-12` |

Accounts are created when the API starts in Development. You can still register additional accounts at `/signup/`.

Configure backend `Authentication:Frontend:CallbackUrl` to `http://localhost:5173/auth/callback` for OAuth.

## Scripts

```bash
npm run dev          # Vite dev server on port 5173
npm run build        # Production build to dist/
npm run preview      # Preview production build
npm run lint         # ESLint
npm run test         # Vitest
npm run test:e2e     # Playwright (auth bypass, mocked API by default)
```

## Production build

```bash
cd frontend
npm ci
npm run build
```

Serve `dist/` as a static SPA with fallback routing to `index.html`. Place the UI on the same origin as the API or behind a reverse proxy.

## Security

- JWTs are not stored in `localStorage`, `sessionStorage`, or JavaScript-accessible cookies.
- SQL parameter values are not sent to client telemetry.
- Query history stores metadata by default; full SQL only when the user opts in via Settings.
- Admin API enum fields may arrive as JSON strings; Zod schemas normalize them before rendering.

See [security.md](security.md).

## Testing

See [testing.md](testing.md#frontend-tests).
