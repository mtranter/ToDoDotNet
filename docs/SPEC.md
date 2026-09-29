# ToDoDotNet — Implementation Spec

Vocabulary is defined in [CONTEXT.md](../CONTEXT.md) and is binding: `Title` (never Name), `Todo Item` (never Task), `Remove` an item, `Delete` a list.

## 1. Purpose

A worked example teaching .NET to developers arriving from Java/Kotlin and Spring Boot. Optimised for **legibility of the architecture**, not for the smallest thing that works. Several decisions below are deliberately more elaborate than a five-endpoint API warrants; each is called out in §9 so a reader knows it was chosen, not stumbled into.

## 2. Solution layout

`net10.0` throughout. Root `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`.

```
ToDoDotNet.sln
Directory.Build.props
docker-compose.yml
CONTEXT.md
README.md
docs/
src/
  ToDo.Domain/        -> (no project references)
  ToDo.Application/   -> Domain
  ToDo.Repository/    -> Application, Domain
  ToDo.Web/           -> Application, Repository
tests/
  ToDo.Tests/         -> Web
```

`ToDo.Web` references `ToDo.Repository` **only** so `Program.cs` can call `AddTodoRepository(...)`. No other file in `ToDo.Web` may reference a Repository type. No reflection, no assembly scanning, no service locator.

## 3. ToDo.Domain

Zero dependencies, zero persistence awareness. No EF attributes, no parameterless constructors for a mapper's benefit, no `Version` property.

### Identity

```csharp
public readonly record struct TodoListId(Guid Value)
{
    public static TodoListId New() => new(Guid.CreateVersion7());
}
public readonly record struct TodoItemId(Guid Value)
{
    public static TodoItemId New() => new(Guid.CreateVersion7());
}
```

UUIDv7 is time-ordered, so Postgres index locality is preserved, and IDs exist before insert — which is what makes §5's reconciliation a clean ID join.

### Value object

`TodoTitle` — non-empty after trimming, max 200 characters. Private constructor; `TodoTitle.Create(string)` throws `InvalidTodoTitleException` on violation. Used by **both** `TodoList` and `TodoItem`.

### Aggregate

`TodoList` is the sole aggregate root.

```csharp
public sealed class TodoList
{
    private readonly List<TodoItem> _items = [];
    public TodoListId Id { get; }
    public TodoTitle Title { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyCollection<TodoItem> Items => _items.AsReadOnly();

    public static TodoList Create(TodoTitle title, DateTimeOffset now);
    public void Rename(TodoTitle title);
    public TodoItemId AddItem(TodoTitle title, DateTimeOffset? dueDate, DateTimeOffset now);
    public void RenameItem(TodoItemId id, TodoTitle title);
    public void SetItemDueDate(TodoItemId id, DateTimeOffset? dueDate, DateTimeOffset now);
    public void CompleteItem(TodoItemId id, DateTimeOffset now);
    public void ReopenItem(TodoItemId id);
    public void RemoveItem(TodoItemId id);
}
```

`TodoList.Rehydrate` and `TodoItem.Rehydrate` are public static factories that rebuild an aggregate from already-valid stored state, skipping creation-time rules. They exist so the Repository can reconstitute without the Domain needing an `InternalsVisibleTo` naming a layer above it. `TodoItem` has private setters, every mutator is `internal` — it cannot be created or mutated from outside the assembly, let alone outside its root. Fields: `Id`, `Title`, `DueDate` (nullable), `IsComplete`, `CreatedAt`, `CompletedAt` (nullable).

### Invariants

| Rule | Violation |
|---|---|
| Title non-empty, <= 200 chars | `InvalidTodoTitleException` |
| A Due Date being **assigned** must be in the future | `InvalidDueDateException` |
| Complete only an incomplete item | `InvalidStateTransitionException` |
| Reopen only a complete item | `InvalidStateTransitionException` |
| Item must belong to this list | `TodoItemNotFoundException` |

The due-date rule applies **at assignment only** — and "assignment" means the value actually changing. `SetDueDate` returns early when the supplied value equals the current one. Without that, a full-replacement `PATCH` resends the existing due date, which is now in the past, and an item becomes uneditable the moment it falls Overdue. There is a regression test for exactly this.

`now` is passed in rather than read from `DateTimeOffset.UtcNow` inside the domain, so time is testable and the domain has no ambient dependency. Web supplies it via `TimeProvider`.

All exceptions derive from `TodoDomainException`.

## 4. ToDo.Application

### Result

Hand-rolled, no library:

```csharp
public enum ErrorKind { NotFound, ConcurrencyConflict }
public sealed record Error(ErrorKind Kind, string Message);
public readonly record struct Result<T> { /* IsSuccess, Value, Error */ }
```

Only *expected flow outcomes* are Results. Invariant violations stay as domain exceptions and are mapped by Web (§6). This split is deliberate — see §9.

### Concurrency carrier

```csharp
public readonly record struct ConcurrencyToken(int Value);
public readonly record struct Versioned<T>(T Value, ConcurrencyToken Token);
```

`Versioned<T>` deconstructs, so `var (list, token) = ...` reads naturally, and it composes as `Result<Versioned<T>>`. The Domain never sees either type.

### Port

```csharp
public interface ITodoListRepository
{
    Task<Versioned<TodoList>?> GetAsync(TodoListId id, CancellationToken ct);
    Task<ConcurrencyToken> AddAsync(TodoList list, CancellationToken ct);
    Task<ConcurrencyToken?> UpdateAsync(TodoList list, ConcurrencyToken expected, CancellationToken ct);
    Task<bool> DeleteAsync(TodoListId id, ConcurrencyToken expected, CancellationToken ct);
    Task<IReadOnlyList<TodoListSummary>> GetPageAsync(int page, int pageSize, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
}
```

`Add` and `Update` are separate because an insert has no expected token; a nullable parameter would be an unenforced precondition. `Update` returns the list's **new** token, or `null` on a mismatch; `Delete` returns `false`. Returning the new token keeps the rule for how a version advances inside the adapter, rather than making the service reconstruct it.

`TodoListSummary` (`Id`, `Title`, `ItemCount`, `CompletedCount`) is a read model defined here and projected directly by the adapter — it never materialises an aggregate. That contrast between the write path and the read path is a deliberate teaching point.

### Service

One `ITodoListService`, registered `Scoped`. Every method is `async`, takes a `CancellationToken`, and returns `Result<T>`. Commands and responses are Application-owned records.

```
CreateListAsync(CreateListCommand)                          -> Result<Versioned<TodoListResponse>>
GetListAsync(Guid)                                          -> Result<Versioned<TodoListResponse>>
GetListPageAsync(int page, int pageSize)                    -> Result<PagedResponse<TodoListSummaryResponse>>
RenameListAsync(Guid, RenameCommand, ConcurrencyToken)      -> Result<Versioned<TodoListResponse>>
DeleteListAsync(Guid, ConcurrencyToken)                     -> Result<Unit>
AddItemAsync(Guid, AddItemCommand, ConcurrencyToken)        -> Result<Versioned<TodoListResponse>>
UpdateItemAsync(Guid, Guid, UpdateItemCommand, Concurrency) -> Result<Versioned<TodoListResponse>>
CompleteItemAsync(Guid, Guid, ConcurrencyToken)             -> Result<Versioned<TodoListResponse>>
ReopenItemAsync(Guid, Guid, ConcurrencyToken)               -> Result<Versioned<TodoListResponse>>
RemoveItemAsync(Guid, Guid, ConcurrencyToken)               -> Result<Versioned<TodoListResponse>>
```

Every write returns the full updated list plus its new token, so the caller always leaves holding a fresh ETag.

Service methods follow one shape: load via port -> `NotFound` if absent -> mutate the domain object -> `UpdateAsync` -> `ConcurrencyConflict` if it returns false -> map to response.

## 5. ToDo.Repository

### Persistence records

`TodoListRecord` and `TodoItemRecord` in `Persistence/`, mirroring the schema not the domain. `TodoListRecord` carries `public int Version { get; set; }` (`uint` has no natural Postgres column type). Named `Record` rather than `Dao` because "DAO" already names the access object in the Java tradition this repo is written for; the README concept map states the equivalence.

### EF configuration

One `IEntityTypeConfiguration<T>` per record — never a monolithic `OnModelCreating`.

- `UseSnakeCaseNamingConvention()` (`EFCore.NamingConventions`) so the schema looks Postgres-native
- `DateTimeOffset` -> `timestamptz`; Npgsql requires UTC — call this out in the README, it is the single most common .NET/Postgres date bug
- `Version` configured `.IsConcurrencyToken()`
- Index on `todo_item.todo_list_id`; FK with cascade delete
- Titles `.HasMaxLength(200).IsRequired()`

### Mapping

Hand-written static extension methods, both directions. No AutoMapper (reflection-based, and commercially licensed since 2025), no Mapperly. In a teaching repo the mapping *is* the lesson, and the domain-to-record direction is not mechanical anyway.

### Reconciliation — the hard part

`UpdateAsync(TodoList list, ConcurrencyToken expected, ct)`:

1. Load the tracked record graph: `Include(r => r.Items).AsSplitQuery().FirstOrDefaultAsync(...)`.
2. Copy the root's scalars from the domain object.
3. Match items by ID — domain-generated, so this is an exact join, no heuristics:
   - in domain, not in records -> `Add`
   - in both -> copy scalars
   - in records, not in domain -> `Remove`
4. **Override the version explicitly:**
   ```csharp
   var entry = db.Entry(record);
   db.Entry(record).Property(r => r.Version).OriginalValue = expected.Value;  // client's token
   record.Version = expected.Value + 1;
   ```
   EF then emits `UPDATE ... SET version = @expected+1 WHERE id = @id AND version = @expected`.
5. `SaveChangesAsync`, catching `DbUpdateConcurrencyException` -> return `false`.

**Step 4 is the whole point.** The reload in step 1 read the *current* version; using it would make the check a no-op and silently give you last-write-wins. Two tokens are in scope there and the ergonomic one is the wrong one — the code carries a comment saying so, and a test asserts a stale token yields 412.

The concurrency token lives on the **root only**. Completing an item bumps the list's version, because the aggregate is the consistency boundary. Clients therefore send the *list's* ETag when mutating an item.

### Reads

- `GetAsync`: `Include` + `AsSplitQuery`, map record -> domain, return `Versioned<TodoList>`
- `GetPageAsync`: `Select` straight into `TodoListSummary` — no aggregate materialised
- `DeleteAsync`: guarded by the same version check, then `ExecuteDeleteAsync`

### Migrations

EF Core migrations committed to this project. Applied by the test fixture and, in `Development` only, at startup. The README flags migrate-on-startup as a demo convenience.

### Registration

One public extension: `IServiceCollection.AddTodoRepository(string connectionString)`.

## 6. ToDo.Web

Minimal APIs. `Program.cs` is a composition root and nothing else; endpoints live in `TodoListEndpoints.MapTodoLists(this IEndpointRouteBuilder)`. Handlers take dependencies as parameters, resolved from the container.

`Program.cs` ends with `public partial class Program;` so `WebApplicationFactory<Program>` can reach it.

### Routes

```
GET    /lists                                        200
POST   /lists                                        201 + Location + ETag
GET    /lists/{listId}                               200 + ETag
PATCH  /lists/{listId}                               200 + ETag   [If-Match]
DELETE /lists/{listId}                               204          [If-Match]
POST   /lists/{listId}/items                         201 + ETag   [If-Match]
PATCH  /lists/{listId}/items/{itemId}                200 + ETag   [If-Match]
DELETE /lists/{listId}/items/{itemId}                200 + ETag   [If-Match]
POST   /lists/{listId}/items/{itemId}/complete       200 + ETag   [If-Match]
POST   /lists/{listId}/items/{itemId}/reopen         200 + ETag   [If-Match]
```

Complete and Reopen are action sub-resources, not `PATCH {"isComplete": true}` — they are guarded state transitions mapping 1:1 onto domain methods, and they get their own error semantics.

`GET /lists` takes `?page=1&pageSize=20`, `pageSize` capped at 100, and returns summaries without items. Full items come from `GET /lists/{listId}`. Collection responses carry no ETag.

### Contracts

Web owns its own request/response records; Application's types never serialise directly. Three hops total (Web record <-> Application command/response <-> Domain). Deliberate — see §9.

Validation attributes (`[Required]`, `[StringLength(200)]`) live on Web request records, wired through the built-in Minimal API validation filter. Web validates *shape*; Domain enforces *invariants*. The overlap is intentional: they answer different callers.

### Status codes

| Condition | Status |
|---|---|
| Validation failure on request shape | 400 |
| `InvalidTodoTitleException`, `InvalidDueDateException` | 400 |
| List or item not found (`ErrorKind.NotFound`) | 404 |
| `InvalidStateTransitionException` | 409 |
| `If-Match` absent on a guarded route | 428 |
| `If-Match` stale (`ErrorKind.ConcurrencyConflict`) | 412 |

Everything is RFC 9457 `ProblemDetails`. Domain exceptions are mapped by an `IExceptionHandler` — the analogue of `@ControllerAdvice`; Result failures are matched explicitly in the handler. `ETag` is the token as a quoted string: `ETag: "3"`.

### OpenAPI

Built-in `Microsoft.AspNetCore.OpenApi` (`AddOpenApi()` / `MapOpenApi()`) plus a Scalar UI. No Swashbuckle.

## 7. ToDo.Tests

Blackbox only. There are no unit-test projects per layer — every test drives real HTTP against a real Postgres.

- **xUnit v3**, **Shouldly**, **Testcontainers.PostgreSql**, **JsonPath.Net**, **Verify.XunitV3** + **Verify.Http**, **Npgsql** (for truncation)
- Runs on **Microsoft.Testing.Platform**, not VSTest, which the .NET 10 SDK no longer supports for MTP projects. No `Microsoft.NET.Test.Sdk`, no `xunit.runner.visualstudio`. `dotnet test` is the command; a root `global.json` carrying `"test": { "runner": "Microsoft.Testing.Platform" }` opts into MTP mode. The legacy `TestingPlatformDotnetTestSupport` MSBuild property is the VSTest-bridge opt-in and is deliberately **not** set.
- The API under test is hosted **in-process** by `WebApplicationFactory<Program>` over an in-memory transport: no port, no second process, but real HTTP handling on every call.
- The container connection string reaches the host as the `ConnectionStrings__Todo` **environment variable**. `WebApplication.CreateBuilder` adds `appsettings.json` after the factory's configuration callbacks run, so an in-memory source loses to the file; environment variables are added last and win.
- `WebApplicationFactory<Program>` hosts the API in-process; one Postgres container per run via an `ICollectionFixture`; migrations applied once at fixture startup
- Isolation: `TRUNCATE todo_list, todo_item RESTART IDENTITY CASCADE` between tests — hand-rolled, no Respawn. Transaction rollback is not viable: the API and the test use different connections.
- A typed `TodoApiClient` wraps `HttpClient` so tests read as scenarios

### Assertions

Two complementary layers:

```csharp
var json = await response.ShouldBeOkAndReadJson();
json.ShouldHaveValueAt("$.title", "Groceries");
json.ShouldHaveCountAt("$.items", 3);
json.ShouldHaveNoValueAt("$.items[0].internalNotes");
```

Path assertions document the fields that matter and carry all error-path checks. One Verify snapshot per endpoint happy path catches fields accidentally *added* — the contract break a browser client actually feels. A `ModuleInitializer` scrubs GUIDs, timestamps and `ETag` to stable placeholders (without which UUIDv7 and real clocks fail every run) and disables the diff-tool launcher outside interactive runs. `.verified.json` files are committed; the README explains the approve-on-change workflow, which will be new to Spring readers.

### Coverage

Happy paths for all ten routes, plus: empty title -> 400; past due date -> 400; unknown list -> 404; unknown item -> 404; complete an already-complete item -> 409; reopen an incomplete item -> 409; missing `If-Match` -> 428; stale `If-Match` -> 412; rename an *overdue* item -> 200 (the regression guard for the assignment-only due-date rule); delete a list -> its items go too.

## 8. Ops and docs

- `docker-compose.yml`: Postgres only
- Scalar UI for the API surface
- `README.md`: run instructions, the tradeoffs in §9, and a Spring <-> .NET concept map — `@RestController` -> endpoint group, `@Service` -> `ITodoListService`, `@Repository`/Spring Data -> port + adapter, `@Autowired` -> constructor injection + `IServiceCollection`, JPA/Hibernate -> EF Core, JPA `merge()` -> the reconciliation in §5, `@Version` -> `ConcurrencyToken`, `@SpringBootTest` -> `WebApplicationFactory`, `@ControllerAdvice` -> `IExceptionHandler`, `application.yml` -> `appsettings.json`, Maven/Gradle -> `.csproj`/NuGet, DAO -> `Record`

**Out of scope:** Dockerfile for the API, CI, authentication/authorization.

## 9. Tradeoffs named rather than hidden

The README states each of these plainly, so no reader mistakes them for accidents. The reasoning behind each is recorded in [docs/adr](./adr/).

1. **`UpdateAsync` issues an extra `SELECT`** that a directly-EF-mapped domain would not need. That is what the pure-domain/persistence-record split costs at the persistence seam — roughly the same tax as detached-entity `merge()` in JPA.
2. **Three mapping hops** between HTTP and Domain. Overengineered for ten endpoints; chosen so the boundary is visible rather than implied.
3. **Two aggregates** where one entity would do, so the aggregate boundary is demonstrable.
4. **Validation appears twice** — shape in Web, invariants in Domain. Different jobs that happen to overlap.
5. **Two error mechanisms** — Results for expected outcomes, exceptions for invariant violations. Some teams would pick one; the split is defended above.
6. **Migrate-on-startup** is a demo convenience, not a pattern to copy.

## 10. Build order

1. Solution, projects, `Directory.Build.props`, reference graph. Verify the graph compiles and Domain has no references.
2. `ToDo.Domain` in full.
3. `ToDo.Application`: Result, `Versioned<T>`, port, commands/responses, service.
4. `ToDo.Repository`: records, configurations, mappers, adapter, migrations, DI extension.
5. `ToDo.Web`: contracts, endpoints, exception handler, ETag plumbing, OpenAPI, `Program.cs`.
6. `docker-compose.yml`, verify the API runs against it by hand.
7. `ToDo.Tests`: fixture, `TodoApiClient`, assertion helpers, then happy paths, then error paths, then snapshots.
8. `README.md` with the concept map and §9.

## 11. Verified against the installed SDK

Built and tested on `net10.0` (SDK 10.0.101). The five API surfaces written from knowledge rather than checking were all confirmed:

| Surface | Result |
|---|---|
| Built-in Minimal API validation | `builder.Services.AddValidation()` — confirmed |
| `Scalar.AspNetCore` 2.17.1 | `MapScalarApiReference()` — confirmed |
| `Verify.XunitV3` 31.28.0 / `Verify.Http` 7.5.1 | `VerifyHttp.Initialize()`, `ScrubInlineGuids()`, `ScrubInlineDateTimes("O")` — confirmed |
| `JsonPath.Net` 3.0.2 | `JsonPath.Parse(...).Evaluate(node).Matches` returns `NodeList`, not `IReadOnlyList<Node>` |
| `EFCore.NamingConventions` 10.0.1 | Ships for EF Core 10 — confirmed |

Two things did not match expectations and are corrected above: `PostgreSqlBuilder`'s parameterless constructor is obsolete (pass the image to the constructor), and `dotnet test` needs the `global.json` opt-in described in §7, because the .NET 10 SDK still defaults to VSTest.

EF Core packages are pinned to 10.0.11 across the board; Npgsql 10.0.3 otherwise drags in EF 10.0.3 and the build warns about the skew.
