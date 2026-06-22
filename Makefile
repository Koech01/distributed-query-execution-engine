.PHONY: setup setup-dev check-docker up up-detached wait-api dev ui logs ps stop down reset test test-backend test-frontend clean help

help:
	@echo "Distributed Query Execution Engine"
	@echo ""
	@echo "  make setup          Create .env and frontend/.env from templates"
	@echo "  make setup-dev      setup + npm install + dotnet restore"
	@echo "  make up             Start backend stack (foreground, with logs)"
	@echo "  make up-detached    Start backend stack in the background"
	@echo "  make dev            Start backend (detached) and web UI dev server"
	@echo "  make ui             Start web UI only (backend must already be running)"
	@echo "  make wait-api       Block until API health check passes"
	@echo "  make logs           Follow Docker Compose logs"
	@echo "  make ps             Show container status"
	@echo "  make stop down      Stop all containers"
	@echo "  make reset          Stop containers and remove volumes"
	@echo "  make test           Run backend and frontend tests"
	@echo "  make clean          Remove frontend node_modules and build output"

setup:
	@if [ -f .env ]; then echo ".env already exists"; else cp .env.example .env && echo "Created .env"; fi
	@if [ -f frontend/.env ]; then echo "frontend/.env already exists"; else cp frontend/.env.example frontend/.env && echo "Created frontend/.env"; fi

setup-dev: setup
	cd frontend && npm install
	dotnet restore

check-docker:
	@docker info >/dev/null 2>&1 || (echo "Docker is not running. Start Docker Desktop, then retry."; exit 1)

up: setup check-docker
	docker compose up --build

up-detached: setup check-docker
	docker compose up -d --build
	@echo "Backend stack starting. Run 'make wait-api' or 'make dev'."

wait-api:
	@echo "Waiting for API at http://localhost:5281/health/ready ..."
	@until curl -sf http://localhost:5281/health/ready >/dev/null 2>&1; do printf "."; sleep 2; done
	@echo " ready"

dev: setup-dev check-docker up-detached wait-api
	cd frontend && npm run dev

ui: setup
	cd frontend && npm run dev

logs:
	docker compose logs -f

ps:
	docker compose ps

stop down: check-docker
	docker compose down

reset: check-docker
	docker compose down -v

test: test-backend test-frontend

test-backend:
	dotnet test -c Release

test-frontend:
	cd frontend && npm test

clean:
	cd frontend && rm -rf node_modules dist test-results playwright-report
	dotnet clean -c Release