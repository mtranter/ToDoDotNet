# TodoList is the only aggregate root

**Flavour:** overengineered for pedagogical purposes.

A todo app needs one entity. This one has two — `TodoList` owning `TodoItem` — so that the aggregate boundary is something a reader can watch being enforced rather than something they read about. `TodoItem` has an internal constructor and private setters; every mutation goes through the root. There is exactly one repository, `ITodoListRepository`, keyed by list ID, and no way to look up an item on its own.

## Consequences

Items are not independently addressable: routes nest as `/lists/{listId}/items/{itemId}`, and mutating an item means loading, mutating and saving its entire list. That is the correct DDD behaviour and it is also visibly more expensive than a flat `/items/{id}` would be — which is the point.
