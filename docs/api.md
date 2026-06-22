# API Reference

Base URL (Docker Compose default): `http://localhost:5281`

All query and admin endpoints require a valid JWT unless noted otherwise. Send the token in the `Authorization` header:

```
Authorization: Bearer <access_token>
```

Swagger is available in Development at `/swagger`.

## Authentication

### Register

`POST /auth/register`

```json
{
  "email": "user@example.com",
  "password": "secure-password-here",
  "displayName": "Example User"
}
```

Returns an access token. The API also sets an HTTP-only access token cookie.

### Login

`POST /auth/login`

```json
{
  "email": "user@example.com",
  "password": "secure-password-here"
}
```

### OAuth

- `GET /auth/google/login?returnTo=/query`
- `GET /auth/github/login?returnTo=/query`

OAuth callbacks redirect to the configured frontend URL with an exchange code. Exchange it at:

`POST /auth/token/exchange`

```json
{
  "exchangeCode": "<code>"
}
```

### JWKS and discovery

- `GET /.well-known/jwks`

### Account management (requires `query:read`)

| Method | Path | Description |
|---|---|---|
| GET | `/auth/account` | Get profile |
| PATCH | `/auth/account` | Update profile |
| POST | `/auth/account/change-password` | Change password |
| DELETE | `/auth/account` | Soft delete account |

## Query endpoints (requires `query:read`)

### Submit query

`POST /queries`

```json
{
  "sql": "SELECT id, amount FROM orders WHERE customer_id = @customerId",
  "parameters": [
    { "name": "@customerId", "type": "int", "value": "42" }
  ],
  "timeoutSeconds": 30,
  "async": false,
  "failurePolicy": "BestEffort"
}
```

| Field | Required | Description |
|---|---|---|
| `sql` | yes | T-SQL `SELECT` statement |
| `parameters` | no | Bound query parameters |
| `queryId` | no | Client-supplied id for idempotent retries |
| `timeoutSeconds` | no | Query timeout (1 to 120 seconds) |
| `async` | no | Use async execution path when `true` |
| `failurePolicy` | no | `BestEffort` or `StrictAll` |

Responses:

- `200 OK`: completed result
- `202 Accepted`: async query accepted (`statusUrl` in body)
- `206 Partial Content`: degraded result (`degraded: true`)

### Query status (async)

`GET /queries/{id}/status`

### Query result (async)

`GET /queries/{id}/result`

### Plan inspection

`POST /queries/plan`

Same request body as `POST /queries`. Returns shard targeting and merge instructions without executing the query.

### Streaming results

`POST /queries/stream`

Same request body as synchronous `POST /queries`. Returns `text/event-stream` with events:

| Event | Description |
|---|---|
| `metadata` | Query id, shard count, stream mode |
| `columns` | Column names |
| `row` | One result row |
| `complete` | Final counters and degradation info |

Streaming is not supported when `async: true`.

## Admin endpoints (requires `query:admin`)

| Method | Path | Description |
|---|---|---|
| GET | `/admin/stats` | Dashboard statistics |
| GET | `/admin/cache/stats` | Redis plan cache counts |
| POST | `/admin/cache/flush` | Flush plan cache (optional `planHash` in body) |
| GET | `/admin/queries/active` | List in-flight queries |
| POST | `/admin/queries/{id}/cancel` | Cancel a running query |
| GET | `/admin/workers` | Worker health and probe status |

## Health endpoints (no authentication)

| Method | Path | Description |
|---|---|---|
| GET | `/health/live` | Liveness |
| GET | `/health/ready` | Readiness |

## Result envelope

```json
{
  "queryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "columns": ["id", "amount"],
  "rows": [["1", "10"], ["2", "20"]],
  "rowCount": 2,
  "totalShards": 4,
  "successfulShards": 4,
  "failedShards": [],
  "degraded": false,
  "executionMs": 87,
  "fromCache": false
}
```

When shards fail under `BestEffort`, the API returns HTTP 206 with `degraded: true` and `failedShards` populated.

## Error responses

Domain errors map to HTTP status codes through middleware:

| Status | Typical cause |
|---|---|
| 400 | Invalid SQL, validation failure, unknown table |
| 401 | Missing or invalid JWT |
| 403 | Missing required scope |
| 404 | Async result not found |
| 429 | Rate limit exceeded |
| 503 | Insufficient healthy workers |

Errors use a JSON body with `type` and `message` fields.
