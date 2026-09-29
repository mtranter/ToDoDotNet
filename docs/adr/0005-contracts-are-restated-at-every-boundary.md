# Contracts are restated at every boundary

**Flavour:** overengineered for pedagogical purposes — knowingly, and with some reluctance.

An HTTP request crosses three mappings before it reaches the domain: Web request record -> Application command -> Domain, and back out as Domain -> Application response -> Web response record. Web could have serialised Application's types directly and saved a hop, and for a ten-endpoint API that would be the sensible call. It is not done here because the compile-time guarantee is worth showing: `ToDo.Web` cannot mention a `TodoList`, and `ToDo.Application` cannot mention an HTTP concern.

## Consequences

Adding a field means touching it in three places. If this were a real product that ratio would be an argument for collapsing the middle hop; here it is the demonstration.

Validation likewise appears twice — DataAnnotations on Web request records for *shape*, domain invariants for *rules*. The overlap is intentional, not an oversight: they answer different callers, and only one of them will still be there when a second entry point is added.
