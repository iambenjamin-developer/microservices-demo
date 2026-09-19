# ADR 0006 — Object mapping: Mapperly replaces Mapster in Ordering and Inventory

- **Status:** Accepted
- **Date:** 2026-09-19
- **Supersedes:** [0005](0005-object-mapping-mapster-and-manual.md)

## Context

ADR 0005 chose Mapster in runtime mode for Ordering and Inventory. The deciding convenience was `ProjectToType<T>()`:
one configuration for in-memory maps and SQL projections. Its costs were accepted at the time:

- mappings are built at runtime from reflection and expression trees, so a broken mapping is found by a unit test
  (`Compile()` / `CompileProjection()`) or, if that test is missing, by a request;
- strictness depends on two flags (`RequireExplicitMapping`, `RequireDestinationMemberSource`) and on remembering to
  write those tests;
- a `TypeAdapterConfig` singleton, `IMapper` / `ServiceMapper` and `Mapster.DependencyInjection` had to be wired in DI;
- "find references" does not see a property used by a mapping convention, and the mapping cannot be stepped through.

ADR 0005 named Mapperly as the option to revisit. Mapperly also generates `IQueryable` projections from the same
mapping definition, so the reason for choosing Mapster no longer holds.

## Decision

- **Ordering and Inventory use [Mapperly](https://mapperly.riok.app/)** (`Riok.Mapperly`, MIT), a source generator.
  One `static partial` mapper per area: `OrderMapper` (`Ordering.Application/Orders`) and `StockMapper`
  (`Inventory.Api/Features/Stock`). No DI registration and no `IMapper`: callers use extension methods
  (`order.ToResponse()`, `query.ProjectToResponse()`).
- The mappers are **strict at compile time**: `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]`
  makes a destination member without a source a warning (RMG012), and warnings are errors, so the build fails.
- **Projections** are `partial` methods from `IQueryable<TEntity>` to `IQueryable<TDto>`. They reuse the element
  mapping configuration (`[MapProperty]`, `[MapPropertyFromSource]`), so in-memory maps and SQL keep one definition.
  Computed members (line total, item count, quantity on hand, items sorted by SKU) are expression-bodied helpers that
  Mapperly inlines into the query expression, so PostgreSQL computes them. If a helper cannot be inlined, Mapperly
  reports RMG068 and the build fails.
- Unchanged from 0005: mappings go **only from entity/aggregate to DTO**, aggregates are created through their
  factory methods, and **Catalog and Notifications map by hand on purpose**.

## Consequences

- **Positive**
  - A renamed or added DTO member breaks the build, not a test or a request. The `Compile()` tests and the strict
    flags are gone because the compiler does their job.
  - The mapping is plain generated C#: readable, debuggable, visible to "find references", no reflection and no
    warm-up cost; compatible with trimming and Native AOT.
  - Less wiring: two packages and the DI registrations were removed.
- **Negative**
  - Configuration is attribute based and member paths are strings (`"Total.Amount"`), checked by the generator.
  - Projections carry the limits of expression trees: helpers must be single expressions, and Mapperly disables object
    factories, optional constructor parameters and null checks inside projections.
  - Whether EF Core can translate a projection is still only proven against a database: the Ordering integration
    tests (Testcontainers) cover it; Inventory's projection is simple and covered by the manual/Postman checks.
- **When to revisit**: if a DTO needs logic that does not fit an expression (the projection would then be written by
  hand for that case), or if the mapping count drops to where hand-written mapping is simpler everywhere.

## Alternatives considered

| Option | Why not (here) |
|---|---|
| Keep Mapster | Works, but errors surface in tests instead of the build, and it needs DI plumbing and reflection |
| AutoMapper | Commercial license since v15; runtime mapping with the same drawbacks as Mapster |
| Hand-written everywhere | Still the choice for Catalog/Notifications; for Ordering's nested order + items it means writing the same shape twice (in-memory and projection) |
