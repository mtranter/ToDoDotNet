SOLUTION      := ToDoDotNet.sln
WEB_PROJECT   := src/ToDo.Web
DATA_PROJECT  := src/ToDo.Repository
CONFIGURATION ?= Debug
PUBLISH_DIR   := artifacts/publish
COVERAGE_DIR  := artifacts/coverage

.DEFAULT_GOAL := help

.PHONY: help
help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-16s\033[0m %s\n", $$1, $$2}'

## ---- Build -------------------------------------------------------------

.PHONY: restore
restore: ## Restore NuGet packages
	dotnet restore $(SOLUTION)

.PHONY: build
build: ## Build the solution
	dotnet build $(SOLUTION) -c $(CONFIGURATION)

.PHONY: clean
clean: ## Remove build output and test results
	dotnet clean $(SOLUTION) -c $(CONFIGURATION)
	rm -rf artifacts TestResults

.PHONY: rebuild
rebuild: clean build ## Clean then build

.PHONY: format
format: ## Apply code style fixes
	dotnet format $(SOLUTION)

.PHONY: lint
lint: ## Check code style without changing files
	dotnet format $(SOLUTION) --verify-no-changes

.PHONY: outdated
outdated: ## List outdated NuGet packages
	dotnet list $(SOLUTION) package --outdated

## ---- Run ---------------------------------------------------------------

.PHONY: run
run: db-up ## Start Postgres and run the API (Scalar UI at /scalar)
	dotnet run --project $(WEB_PROJECT)

.PHONY: watch
watch: db-up ## Start Postgres and run the API with hot reload
	dotnet watch --project $(WEB_PROJECT)

.PHONY: publish
publish: ## Publish the API in Release to artifacts/publish
	dotnet publish $(WEB_PROJECT) -c Release -o $(PUBLISH_DIR)

## ---- Test --------------------------------------------------------------

.PHONY: test
test: ## Run tests (needs Docker running for Testcontainers)
	@docker info > /dev/null 2>&1 || (echo "Docker is not running; Testcontainers needs it." && exit 1)
	dotnet test

.PHONY: coverage
coverage: ## Run tests with coverage and build an HTML report in artifacts/coverage/report
	@docker info > /dev/null 2>&1 || (echo "Docker is not running; Testcontainers needs it." && exit 1)
	rm -rf $(COVERAGE_DIR)
	dotnet tool restore
	dotnet test --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml --results-directory $(abspath $(COVERAGE_DIR))
	dotnet reportgenerator \
		-reports:$(COVERAGE_DIR)/coverage.cobertura.xml \
		-targetdir:$(COVERAGE_DIR)/report \
		-reporttypes:"Html;TextSummary" \
		-filefilters:"-*/obj/*;-*/Migrations/*"
	@cat $(COVERAGE_DIR)/report/Summary.txt | head -20
	@echo "Report: $(COVERAGE_DIR)/report/index.html"

.PHONY: coverage-open
coverage-open: coverage ## Generate the coverage report and open it in the browser
	open $(COVERAGE_DIR)/report/index.html

## ---- Database ----------------------------------------------------------

.PHONY: db-up
db-up: ## Start Postgres and wait until healthy
	docker compose up -d --wait

.PHONY: db-down
db-down: ## Stop Postgres
	docker compose down

.PHONY: db-reset
db-reset: ## Stop Postgres and delete its data, then start fresh
	docker compose down -v
	docker compose up -d --wait

.PHONY: db-logs
db-logs: ## Tail Postgres logs
	docker compose logs -f postgres

.PHONY: db-shell
db-shell: ## Open psql in the Postgres container
	docker compose exec postgres psql -U todo -d todo

## ---- EF Core migrations (needs: dotnet tool install -g dotnet-ef) ------

.PHONY: migration
migration: ## Add a migration: make migration name=AddSomething
	@test -n "$(name)" || (echo "Usage: make migration name=<MigrationName>" && exit 1)
	dotnet ef migrations add $(name) --project $(DATA_PROJECT) --startup-project $(WEB_PROJECT) --output-dir Persistence/Migrations

.PHONY: migration-remove
migration-remove: ## Remove the last migration
	dotnet ef migrations remove --project $(DATA_PROJECT) --startup-project $(WEB_PROJECT)

.PHONY: migrate
migrate: db-up ## Apply migrations to the database
	dotnet ef database update --project $(DATA_PROJECT) --startup-project $(WEB_PROJECT)
