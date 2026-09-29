# The Domain knows nothing about persistence

**Flavour:** overengineered for pedagogical purposes.

`ToDo.Domain` has no EF Core dependency, no mapping attributes, no parameterless constructors added for a framework's convenience, and no `Version` field. `ToDo.Repository` owns a parallel set of persistence records (`TodoListRecord`, `TodoItemRecord`) shaped like the schema, plus hand-written mappers in both directions. The alternative — mapping the aggregate to EF directly via backing-field conventions — works fine and is what most .NET examples do, but then "Domain depends on nothing" is a claim about the project file rather than a fact about the code.

## Consequences

`UpdateAsync` must reload the tracked record graph and reconcile it against a detached domain object, which costs an extra `SELECT` on every write. That is roughly the tax a Spring reader already pays for detached-entity `merge()` in JPA, and naming it is part of the lesson.

Mappers are hand-written on purpose. Not AutoMapper (reflection-based, and commercially licensed since 2025), not Mapperly (a source generator would hide the seam this repo exists to show), and the domain-to-record direction has to reconcile a child collection anyway, so a generator would not cover the hard half. If someone proposes a mapping library in six months, this paragraph is the answer.
