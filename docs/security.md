# Security

## Authentication

The API uses JWT Bearer tokens (RS256). Browser clients receive tokens as HttpOnly cookies set by the backend on login, registration, and OAuth exchange.

Sign-in options:

| Method | Endpoints |
|---|---|
| Email and password | `POST /auth/register`, `POST /auth/login` |
| Google OAuth 2.0 / OIDC | `GET /auth/google/login`, `GET /auth/google/callback` |
| GitHub OAuth 2.0 / OIDC | `GET /auth/github/login`, `GET /auth/github/callback` |

OAuth flows issue a short-lived exchange code. Clients exchange it at `POST /auth/token/exchange` for a JWT.

Token validation:

- Algorithm: RS256
- Audience: `dqee-api` (configurable)
- Issuer: local issuer or external authority depending on configuration
- JWKS: `GET /.well-known/jwks`

In Development, ephemeral RSA keys are generated when signing keys are not configured. Production must configure stable keys through environment variables.

## Authorization

JWT scopes map to ASP.NET Core policies:

| Scope | Policy | Access |
|---|---|---|
| `query:read` | `QueryRead` | Query and account endpoints |
| `query:admin` | `QueryAdmin` | Admin endpoints |

Health and authentication endpoints are anonymous.

## Input validation

- Only `SELECT` statements are accepted.
- Parameterized queries are required; values are never concatenated into SQL.
- Query length, parameter count, and AST complexity limits are enforced.
- Blocked SQL tokens are rejected with word-boundary checks.

## Transport security

- Enable HTTPS in production through a reverse proxy or ingress.
- Coordinator-to-worker gRPC uses HTTP/2 without mutual TLS in the current release. Restrict worker network access and place services in a private network until mTLS is added.
- Secrets must not appear in committed configuration files.

## Data protection

- SQL parameter values must not appear in logs or trace attributes.
- Only non-sensitive identifiers (query id, plan hash, shard index) belong in telemetry.
- Redis holds user accounts, OAuth state, exchange codes, plans, and results. Protect Redis with authentication and network isolation in production.

## Rate limiting

- Concurrent request limiting on the API (`RateLimiting:MaxConcurrentRequests`)
- Per-IP fixed-window rate limiting on login and registration endpoints

## Account security

- Passwords are hashed with PBKDF2-SHA256
- Failed login lockout is configurable
- Account deletion is a soft delete; deleted accounts cannot authenticate

## Development demo accounts

In Development, the API seeds two generic demo accounts on startup (see [configuration.md](configuration.md)). These credentials are documented for local evaluation only. Disable seeding (`Authentication:Seed:Enabled=false`) in production and shared deployments.

## Web UI

The React web UI in `frontend/` uses HttpOnly cookie sessions (`dqee_access_token`) set by the backend. The browser sends cookies with `credentials: 'include'`. Session claims for route guards are loaded from `GET /auth/account` and kept in memory only.

Client-side rules:

- No JWTs, credentials, or SQL parameter values in `localStorage`, `sessionStorage`, or client telemetry
- Query history stores metadata by default; full SQL is optional via user preference
- Use the Vite dev proxy or same-origin deployment because the backend does not configure CORS

See [frontend.md](frontend.md) for environment variables and local development setup.

## Reporting vulnerabilities

See [SECURITY.md](../SECURITY.md) for responsible disclosure instructions.
