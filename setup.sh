#!/usr/bin/env bash
set -e

echo "Distributed Query Execution Engine setup"

if [ ! -f .env ]; then
  cp .env.example .env
  echo "Created .env from .env.example"
else
  echo ".env already exists"
fi

if [ ! -f frontend/.env ]; then
  cp frontend/.env.example frontend/.env
  echo "Created frontend/.env from frontend/.env.example"
else
  echo "frontend/.env already exists"
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is not installed or not on PATH."
  echo "Install Docker Desktop, then run ./setup.sh again."
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker is installed but not running."
  echo "Start Docker Desktop, then run ./setup.sh again."
  exit 1
fi

echo "Docker is running"
echo ""
echo "Setup complete. Next steps:"
echo "  make up-detached   # build and start the backend stack"
echo "  make dev           # start backend (if needed) and the web UI"
echo ""
echo "Then open:"
echo "  http://localhost:5173"
echo ""
echo "Sign in at /login/ with a pre-seeded development account:"
echo "  Admin:    admin@example.com / ChangeMe-Admin-12"
echo "  User:     user@example.com / ChangeMe-User-12"
echo ""
echo "Accounts are created automatically when the API starts in Development."