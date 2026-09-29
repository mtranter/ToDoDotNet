# Every test is blackbox, over real HTTP and real Postgres

**Flavour:** deliberate deviation — not overengineering, an actual conviction.

There are no per-layer unit test projects. `ToDo.Tests` is the only test project, it drives the API through `HttpClient` against `WebApplicationFactory<Program>`, and it talks to a genuine Postgres in a Testcontainers container with the real migrations applied. Tests assert on JSON paths (lightweight contract coverage for browser clients) plus one Verify snapshot per endpoint to catch fields accidentally added.

## Consequences

The architecture in ADRs 0002-0005 is never tested directly, only through its effects. That is on purpose: a suite that mocks `ITodoListRepository` would pass whether or not the reconciliation in ADR 0002 is correct, and the reconciliation is the part most likely to be wrong.

The cost is granularity. A failing test tells you the API misbehaved, not which layer did it. For a codebase this size that is an acceptable trade; at ten times the size it would not be.
