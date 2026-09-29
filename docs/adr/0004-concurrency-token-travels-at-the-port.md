# The concurrency token travels at the port, not on the aggregate

**Flavour:** experimental — this one is here because it tickled Mark's fancy, and it turned out to be right.

Optimistic locking needs a version, and a version is a fact about a row, not about a todo list. Rather than hang a `Version` property on the aggregate, the port returns `Versioned<TodoList>` (a record struct that deconstructs, so `var (list, token) = await repo.GetAsync(id)` reads naturally) and takes the expected `ConcurrencyToken` as a separate argument on write. `AddAsync` and `UpdateAsync` are split, because an insert has no expected token and a nullable parameter would be an unenforced precondition.

## Considered options

Putting `Version` on the aggregate was the obvious move and was very nearly taken. Postgres `xmin` via `UseXminAsConcurrencyToken()` looked attractive and is a trap in this design: because `UpdateAsync` reloads the row (see ADR 0002), it reads the *current* version, so the check compares a value against itself and silently degrades to last-write-wins.

## Consequences

The token is not eliminated from the codebase, only kept out of the Domain — it still appears in Application contracts, Web contracts, and on the wire as `ETag`/`If-Match`. Three of five projects see it either way; this decision is about whether the fourth does.

Carrying an explicit expected token is also what rehabilitates the version check after the reload: the repository writes the client's token into `entry.Property(r => r.Version).OriginalValue` before saving, so the `WHERE` clause tests the client's value rather than the one it just read. Two tokens are in scope at that line and the ergonomic one is the wrong one. The code says so in a comment and a test asserts a stale token yields 412.

The token lives on the root only, so completing an item bumps the list's version and clients send the *list's* ETag when mutating an item. Surprising on first contact, and exactly what "the aggregate is the consistency boundary" means.
