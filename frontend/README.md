# Distributed Query Execution Engine Frontend

React web UI for the Distributed Query Execution Engine.

## Quick start

From the repository root:

```bash
./setup.sh
make dev
```

Or manually:

```bash
npm install
cp .env.example .env
npm run dev
```

Start the backend API first. See the repository [README](../README.md) and [frontend documentation](../docs/frontend.md).

Sign in at `/login/` with `admin@example.com` / `ChangeMe-Admin-12` or `user@example.com` / `ChangeMe-User-12` (seeded automatically in Development).

## Documentation

| Topic | Location |
|---|---|
| Frontend architecture, routes, and configuration | [docs/frontend.md](../docs/frontend.md) |
| Backend HTTP API | [docs/api.md](../docs/api.md) |
| Full system overview | [docs/architecture.md](../docs/architecture.md) |
| Environment variables | [`.env.example`](../.env.example) |
| Testing | [docs/testing.md](../docs/testing.md) |

## License

MIT. See [LICENSE](../LICENSE) in the repository root.
