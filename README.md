# ToDoDotNet

A todo API in ASP.NET Core, written to be **read** by developers arriving from Java/Kotlin and Spring Boot.

It is deliberately more elaborate than five endpoints need. Every place it is heavier than necessary is recorded in [`docs/adr`](./docs/adr/) as either pedagogical overengineering or an experiment, so nothing looks like an accident. Start with [ADR 0001](./docs/adr/0001-this-repo-is-a-teaching-artifact.md).

## Running it

```bash
docker compose up -d --wait          # Postgres on :5432
dotnet run --project src/ToDo.Web    # API on :5141, Scalar UI at /scalar
```

## Running the tests

```bash
docker info > /dev/null              # Testcontainers needs a running Docker
dotnet test
```

The suite starts its own throwaway Postgres, so the compose stack above is not needed for it. 32 tests, about ten seconds.

The API under test runs **in-process**: `WebApplicationFactory<Program>` boots the host inside the test process and `HttpClient` reaches it over an in-memory transport. There is no port and no second process — but every test still goes through real HTTP handling, so the suite stays blackbox.

> The `global.json` at the root opts `dotnet test` into Microsoft.Testing.Platform mode. The .NET 10 SDK still defaults to VSTest, which no longer supports MTP projects, so without that file `dotnet test` fails.

## The shape

```
ToDo.Domain      ->  (nothing)                    the model, and no framework at all
ToDo.Application ->  Domain                       use cases, ports, Result
ToDo.Repository  ->  Application, Domain          the EF Core adapter behind the port
ToDo.Web         ->  Application, Repository      HTTP, and the composition root
ToDo.Tests       ->  Web                          blackbox, over real HTTP and real Postgres
```

`ToDo.Web` references `ToDo.Repository` only so `Program.cs` can call `AddTodoRepository(...)`. One line in the whole API project knows EF Core exists. Nothing is wired by reflection or assembly scanning.

Vocabulary is fixed in [`CONTEXT.md`](./CONTEXT.md) and the full design is in [`docs/SPEC.md`](./docs/SPEC.md).

## Coming from Spring Boot

| Spring | Here |
|---|---|
| `@RestController`, `@RequestMapping` | Minimal API endpoint group — `TodoListEndpoints.MapTodoLists()` |
| `@Service` | `ITodoListService` / `TodoListService` |
| `@Repository`, Spring Data | `ITodoListRepository` (port, in Application) + `TodoListRepository` (adapter) |
| `@Autowired`, `@Bean`, `@Configuration` | Constructor injection + `IServiceCollection` extensions |
| `@Component` scanning | Nothing. Registrations are written out by hand. |
| JPA / Hibernate | EF Core |
| `EntityManager` | `DbContext` |
| `merge()` on a detached entity | `TodoListRepository.UpdateAsync` — the reconciliation, done by hand |
| `@Version` | `ConcurrencyToken`, carried at the port rather than on the entity |
| `@OneToMany(orphanRemoval = true)` | A required relationship — removing from the tracked collection deletes the row |
| Flyway / Liquibase | EF Core migrations, in `src/ToDo.Repository/Persistence/Migrations` |
| `@ControllerAdvice` | `IExceptionHandler` — `DomainExceptionHandler` |
| Bean Validation (`@NotNull`, `@Size`) | DataAnnotations on request records + `AddValidation()` |
| `@SpringBootTest(webEnvironment = RANDOM_PORT)` | `WebApplicationFactory<Program>` |
| `@Testcontainers`, `@Container` | `Testcontainers.PostgreSql` in a collection fixture |
| `TestRestTemplate` | `TodoApiClient` over `HttpClient` |
| `application.yml` | `appsettings.json` |
| Maven / Gradle | `.csproj` + NuGet |
| springdoc / Swagger UI | `Microsoft.AspNetCore.OpenApi` + Scalar |
| DAO | `TodoListRecord` / `TodoItemRecord` — "DAO" here would name the repository, so the data types are called records |

One term to unlearn immediately: a todo item is never called a **Task**. `Task` is the .NET async type and appears in every signature in the codebase.

## Concurrency, because it is the interesting part

Every write is conditional. Reads hand back an `ETag`; writes require it back as `If-Match`.

```bash
curl -i -X POST localhost:5141/lists -d '{"title":"Groceries"}' -H 'Content-Type: application/json'
# 201, ETag: "1"

curl -i -X POST localhost:5141/lists/$ID/items -H 'If-Match: "1"' \
     -H 'Content-Type: application/json' -d '{"title":"Milk","dueDate":null}'
# 200, ETag: "2"

curl -i -X POST localhost:5141/lists/$ID/items -H 'If-Match: "1"' ...
# 412 -- that token is stale
```

Two things surprise people:

- **The token belongs to the list, not the item.** Completing an item bumps the list's version, because the aggregate is the consistency boundary. Clients send the *list's* ETag when mutating an item.
- **A missing `If-Match` is a 428, not a silent success.** Letting an unconditional write through would quietly defeat the whole mechanism.

| Situation | Status |
|---|---|
| Malformed request, bad title, due date moved into the past | 400 |
| List or item not found | 404 |
| Completing something already complete, reopening something already open | 409 |
| `If-Match` stale | 412 |
| `If-Match` absent | 428 |

All failures are RFC 9457 problem documents.

## Things this repo does on purpose that you may want to argue with

1. **`UpdateAsync` issues an extra `SELECT`.** The aggregate arrives detached and free of persistence concerns, so the tracked graph has to be fetched before it can be diffed. This is the same tax as detached-entity `merge()` in JPA, and it is what keeps EF Core out of `ToDo.Domain`. ([0002](./docs/adr/0002-domain-is-free-of-persistence-concerns.md))
2. **Contracts are restated three times** between HTTP and Domain. Overkill at this size; it makes the boundary a compile-time fact rather than a convention. ([0005](./docs/adr/0005-contracts-are-restated-at-every-boundary.md))
3. **Two aggregates where one entity would do**, so the aggregate boundary is visible. ([0003](./docs/adr/0003-todolist-is-the-only-aggregate-root.md))
4. **Validation appears twice.** Web validates shape and answers an HTTP caller; the Domain enforces invariants and answers every caller.
5. **Two error mechanisms.** `Result<T>` for expected outcomes (not found, stale token), exceptions for broken invariants. The Application layer never catches a domain exception.
6. **Mapping is hand-written.** Not AutoMapper, not Mapperly — in a teaching repo the mapping is the lesson. ([0002](./docs/adr/0002-domain-is-free-of-persistence-concerns.md))
7. **There are no unit tests.** Every test drives real HTTP against real Postgres. ([0006](./docs/adr/0006-every-test-is-blackbox.md))
8. **Migrations run at startup in Development.** A demo convenience. A real deployment migrates as a separate, gated step.

## Snapshot tests

Seven endpoints have an approved response snapshot in `tests/ToDo.Tests/Snapshots`, alongside the JSONPath assertions. Path assertions prove the fields we named are right; snapshots catch a field somebody accidentally *added*.

After an intentional contract change the run writes a `.received.txt` next to the `.verified.txt`. Read the diff, and if it is what you meant, replace the verified file with the received one and commit it.

## Not included

No Dockerfile for the API, no CI, no authentication. Each would be a worthwhile next exercise; none of them teaches anything about the architecture above.
