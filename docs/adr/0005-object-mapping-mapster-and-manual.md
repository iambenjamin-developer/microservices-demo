# ADR 0005 — Object mapping: Mapster in Ordering and Inventory, by hand in Catalog and Notifications

- **Status:** Accepted
- **Date:** 2026-09-17

## Context

Read endpoints turn entities into response DTOs. Two things matter:

1. **Reads should be SQL projections.** Loading an aggregate with its items to return five columns wastes memory and
   round trips; `Select` into the DTO lets EF Core generate exactly the query needed.
2. **Mapping must not bypass the domain.** An aggregate created by a mapper skips its factory method and therefore
   its invariants.

Options in .NET: AutoMapper (commercial license since v15), Mapster (MIT), Mapperly (MIT, source generator), or
writing the mapping by hand.

## Decision

- **Ordering and Inventory use Mapster** in runtime mode: a `TypeAdapterConfig` registered in DI (`IMapper` through
  `Mapster.DependencyInjection`), one `IRegister` per area (`OrderMappingRegister`, `StockMappingRegister`) and
  `ProjectToType<T>()` for EF Core queries, so the same configuration serves in-memory `Map` and SQL projections.
- The configuration is **strict** (`RequireExplicitMapping`, `RequireDestinationMemberSource`) and unit tests call
  `Compile()` and `CompileProjection()`: a renamed property fails the build pipeline, not a request in production.
- Mappings go **only from entity/aggregate to DTO**. Aggregates are always created through their factory methods
  (`Order.Place(...)`, `StockItem.Create(...)`), never by a mapper.
- **Catalog and Notifications map by hand on purpose**: an expression projection (`Select(p => new ProductResponse(...))`)
  and a `FromProduct` factory in Catalog, a `Select` into `NotificationResponse` in Notifications. It is the
  reference to compare against, and those services have one or two DTOs.

## Consequences

- **Positive**
  - Reads in Ordering and Inventory are projections that translate to SQL (e.g. `quantityOnHand = available + reserved`
    computed in the query), without writing each `Select` twice.
  - Strict config + compile tests remove the classic mapper failure: silent `null`s or zeros for a member nobody
    mapped.
  - MIT license.
  - The repository shows both approaches, which is the honest answer to "should we use a mapper?": it depends on how
    many DTOs there are and how much they differ from the entities.
- **Negative**
  - Runtime mapping is reflection/expression based: errors surface in tests, not at compile time, and "find
    references" does not see a property used by a convention.
  - Another library and another convention for newcomers; `CLAUDE.md` states where each approach applies.
- **When to revisit**: **Mapperly** (source-generated, compile-time errors, no reflection) is a strong alternative if
  the mapping count grows or AOT matters.

## Alternatives considered

| Option | Why not (here) |
|---|---|
| AutoMapper | Commercial license since v15 |
| Mapperly | Very good; Mapster's `ProjectToType` with the same config for both paths was the deciding convenience |
| Hand-written everywhere | Fine for Catalog/Notifications; for Ordering's nested order + items it means maintaining the same shape twice |
