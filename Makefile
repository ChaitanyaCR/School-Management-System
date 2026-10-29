.DEFAULT_GOAL := help
.PHONY: help setup deps db db-reset up down api web test check format openapi module migration

help: ## List available commands
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*## "}; {printf "  \033[36m%-10s\033[0m %s\n", $$1, $$2}'

setup: ## One-time setup after cloning: git hooks, packages
	git config core.hooksPath .githooks
	cd backend && dotnet restore
	cd frontend && npm ci

deps: ## Start PostgreSQL and Mailpit (for running API/web natively)
	docker compose up -d --wait postgres mailpit

db: deps ## Apply migrations and seed the demo organisation (demo.localhost)
	cd backend && dotnet run --project src/SchoolMs.Api --launch-profile http -- migrate seed-demo

db-reset: ## Delete the local database and recreate it from scratch
	docker compose down -v
	$(MAKE) db

up: ## Run the full stack in Docker -> http://demo.localhost:8080
	docker compose up --build -d

down: ## Stop all containers (data is kept)
	docker compose down

api: ## Run the API with hot reload -> http://localhost:5080 (docs: /api/docs)
	cd backend && dotnet watch run --project src/SchoolMs.Api --launch-profile http

web: ## Run the web app with hot reload -> http://demo.localhost:5173
	cd frontend && npm run dev

test: ## Run all backend and frontend tests (backend needs Docker)
	cd backend && dotnet test
	cd frontend && npm test

check: ## Run the static checks CI runs
	cd backend && dotnet format --verify-no-changes && dotnet build
	cd frontend && npm run format:check && npm run lint && npm run typecheck

format: ## Auto-format all code
	cd backend && dotnet format
	cd frontend && npm run format

openapi: ## Regenerate the OpenAPI document and the frontend API client
	cd backend && dotnet build src/SchoolMs.Api
	cd frontend && npm run api:generate

module: ## Scaffold a backend module: make module NAME=Students
	backend/scripts/new-module.sh $(NAME)

migration: ## Add an EF Core migration: make migration MODULE=Tenancy NAME=AddBranchAddress
	cd backend && dotnet ef migrations add $(NAME) \
	  --project src/Modules/$(MODULE)/SchoolMs.Modules.$(MODULE) --startup-project src/SchoolMs.Api \
	  --context $(MODULE)DbContext --output-dir Infrastructure/Migrations
