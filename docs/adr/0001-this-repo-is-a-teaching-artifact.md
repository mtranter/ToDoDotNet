# This repo is a teaching artifact, not a reference implementation

**Flavour:** governing constraint — read this before judging any other decision here.

ToDoDotNet exists to teach .NET and hexagonal architecture to developers arriving from Java/Kotlin and Spring Boot. Where a decision could be optimised either for *the smallest code that ships this feature* or for *the clearest demonstration of a concept*, this repo picks the second every time. Several of the ADRs that follow describe structures that are objectively too heavy for a ten-endpoint todo API; they are there because a boundary you can see is worth more to a learner than a boundary you have to infer.

## Consequences

Anyone extracting patterns from here for production work should read ADRs 0002-0006 and decide case by case. "It's done that way in ToDoDotNet" is not a justification on its own.
