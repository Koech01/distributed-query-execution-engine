# Connecting Shard Databases

This guide is for **operators and developers** who deploy the backend. End users sign in through the web UI and query data that has already been connected. There is no UI flow to add a database at runtime.

## Overview

The system runs read-only SQL across **sharded** data:

- Each **worker** holds connection strings for one or more shard databases.
- The **coordinator** uses a **shard map** to decide which shards a query should hit.
- The same logical table (for example `orders`) should exist on every shard with a compatible schema.

Workers support **SQLite** and **SQL Server** connection strings. The worker detects SQL Server strings by common keywords such as `Server=` or `Initial Catalog=`. Other strings are treated as SQLite.

See [architecture.md](architecture.md) for how queries are split and merged.

## Who can connect databases

| Role | Can connect a database? |
|---|---|
| End user (web UI) | No |
| Operator / deployer | Yes, through configuration only |

## Before you start

1. Decide how your data is sharded (shard key column and shard count).
2. Create the same table schema on each shard database.
3. Load data onto each shard (each row should land on the shard that matches its shard key).
4. Match the coordinator **shard map** to your table name, shard key, and shard count.
5. Point each worker at the connection strings for the shard indices it owns.

The system accepts `SELECT` queries only. Cross-shard joins are not supported. SQL parsing follows T-SQL rules. See [architecture.md](architecture.md#supported-sql-current-release) for supported query features.

## Default demo setup

Docker Compose ships with four SQLite shard files on the worker volume:

| Setting | Default |
|---|---|
| Shard count | `4` |
| Table | `orders` |
| Shard key | `customer_id` |
| Strategy | `ConsistentHash` |
| Worker connections | `Worker__Shards__0` through `Worker__Shards__3` pointing at `/data/shard0.db` to `/data/shard3.db` |

These paths are defined in `docker-compose.yml`. The demo does not load sample rows automatically. Create tables and insert data on each shard before running queries against your own schema.

## Step-by-step: connect your databases

### 1. Configure the shard map (coordinator)

Tell the coordinator which tables are sharded and how. Example for an `orders` table split four ways on `customer_id`:

```bash
ShardMap__Tables__orders__ShardKey=customer_id
ShardMap__Tables__orders__ShardCount=4
ShardMap__Tables__orders__Strategy=ConsistentHash
```

In Docker Compose, set these on the `coordinator` service. For local `dotnet run`, add them to `.env` or `appsettings.json`.

Add one entry per sharded table. The table name in the shard map must match the table name used in SQL queries.

### 2. Configure worker shard indices

List which shard indices each worker owns:

```bash
Worker__ShardIndices__0=0
Worker__ShardIndices__1=1
Worker__ShardIndices__2=2
Worker__ShardIndices__3=3
```

A single worker can own all shards (as in the default Compose file). For larger deployments, split indices across multiple worker instances. Each index must be owned by exactly one worker.

### 3. Configure worker connection strings

Set one connection string per shard index the worker owns:

**SQLite example:**

```bash
Worker__Shards__0=Data Source=/data/shard0.db
Worker__Shards__1=Data Source=/data/shard1.db
```

**SQL Server example:**

```bash
Worker__Shards__0=Server=db-host;Database=orders_shard_0;User ID=app;Password=YOUR_PASSWORD;TrustServerCertificate=True
Worker__Shards__1=Server=db-host;Database=orders_shard_1;User ID=app;Password=YOUR_PASSWORD;TrustServerCertificate=True
```

Never commit real passwords. Use environment variables, Docker secrets, or a secret store in production.

### 4. Apply configuration and restart

| Deployment method | Where to edit |
|---|---|
| Docker Compose | `docker-compose.yml` environment blocks for `coordinator` and `worker` |
| Local `dotnet run` | `.env`, `appsettings.json`, or `dotnet user-secrets` on the Worker and Coordinator projects |

After changing connection strings or shard map settings, restart the affected services:

```bash
docker compose up -d --build worker coordinator
```

Or restart the Coordinator and Worker processes if you run them with `dotnet run`.

### 5. Verify connectivity

1. Check worker readiness: `curl http://localhost:5101/health/ready` (Compose default port).
2. Sign in to the web UI and open the Query page.
3. Run a plan-only request or a simple query, for example:

```sql
SELECT id, amount FROM orders WHERE customer_id = 42
```

Use `POST /queries/plan` from [api.md](api.md) to inspect shard targeting without executing.

## Multiple workers

To scale out, run additional worker containers or processes:

- Give each worker a unique `Worker__NodeId` and reachable `Worker__Address`.
- Assign non-overlapping `Worker__ShardIndices__*` values to each worker.
- Provide `Worker__Shards__{index}` only for indices that worker owns.
- Enable Consul registration (`Worker__Consul__Enabled=true`) so the coordinator can discover workers.

See [deployment.md](deployment.md) for hosting notes.

## Example: replace demo SQLite shards

1. Create four SQLite files (or SQL Server databases) with an `orders` table on each.
2. Update `Worker__Shards__0` through `Worker__Shards__3` in `docker-compose.yml`.
3. Confirm coordinator shard map settings match your table (`orders`, `customer_id`, count `4`).
4. Mount a volume or path so the worker can reach SQLite files, or use SQL Server hostnames reachable from the container network.
5. Restart the worker and coordinator services.
6. Sign in and run a `SELECT` against `orders`.

## Local development without Compose

When running the Worker project directly:

```bash
cd src/DistributedQuery.Worker
dotnet user-secrets set "Worker:ShardIndices:0" "0"
dotnet user-secrets set "Worker:Shards:0" "Data Source=./data/shard0.db"
```

Configure matching shard map settings on the Coordinator project. Full variable names and defaults are listed in [configuration.md](configuration.md).

## Related documentation

| Document | Purpose |
|---|---|
| [configuration.md](configuration.md) | Full environment variable reference |
| [deployment.md](deployment.md) | Docker Compose and production hosting |
| [architecture.md](architecture.md) | Sharding model and query lifecycle |
| [api.md](api.md) | Query and plan endpoints |
