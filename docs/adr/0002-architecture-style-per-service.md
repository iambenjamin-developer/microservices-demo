# ADR 0002 — Architecture style chosen per service

- **Status:** Accepted
- **Date:** 2026-09-16

## Context

The services differ a lot in how much business logic they hold:

- **Ordering** has real rules: an order is placed from a Catalog price snapshot, volume discount tiers apply, lines
  are validated (no duplicates, at most 50, one currency), and the order moves through a guarded state machine
  (`Pending → Confirmed | Rejected`) driven by events from another service.
- **Catalog** is create/read/update of products with validation.
- **Inventory** has one rule worth isolating (all-or-nothing reservation) and otherwise reads and sets stock levels.
- **Notifications** stores what happened and sends an e-mail.

Applying the same layered structure to all of them would either under-structure Ordering or bury three small
services under ceremony (four projects, interfaces with one implementation, mappers between identical types).

## Decision

Choose the style per service, and make the choice explicit:

- **Ordering → Clean Architecture + DDD**, four projects:
  - `Ordering.Domain` — the `Order` aggregate, value objects (`Money`, `Sku`, `Quantity`), domain events, discount
    strategies. Depends only on `BuildingBlocks.Common` (`Result`/`Error`), no frameworks.
  - `Ordering.Application` — commands and their handlers, validators, decorators, ports (`ICatalogClient`,
    `IUnitOfWork`, `IAccessTokenProvider`). Depends only on the domain and on abstractions packages.
  - `Ordering.Infrastructure` — EF Core, outbox interceptor, Catalog HTTP client with Polly, Service Bus consumers
    and the **query handlers** (reads project straight to DTOs and never load the aggregate).
  - `Ordering.Api` — endpoints and the composition root.
  - The dependency rule is **enforced by tests** (`tests/Architecture.Tests`, NetArchTest): the domain cannot see
    Application, Infrastructure, EF Core or ASP.NET Core; Application cannot see Infrastructure, EF Core, ASP.NET
    Core, Service Bus or HTTP; command handlers are internal and sealed; endpoints do not use Infrastructure.
- **Catalog and Inventory → Vertical Slice Architecture**, one project each: `Features/<Area>/<Feature>/` holds the
  endpoint, request, handler and validator of one use case. Handlers use the `DbContext` directly (no repository).
  Inventory still keeps its one real rule in a pure function (`StockReservation.Reserve`) so it is unit tested
  without a database.
- **Notifications → Azure Function**, one project organized by technical concern (`Api`, `Messaging`, `Domain`,
  `Email`, `Persistence`), because the triggers are the entry points.

Everything shared across styles lives in building blocks: `Result`, `ProblemDetails` mapping, the validation filter,
endpoint discovery, JWT validation, messaging.

## Consequences

- **Positive**
  - Structure is proportional to complexity. Ordering's domain is testable without infrastructure (fast unit tests
    with a Test Data Builder); Catalog adds a feature by adding a folder.
  - Changes stay local: in a slice, a use case's request, validation and query change together in one place.
  - The repository shows both styles side by side, which is exactly the trade-off an interview asks about.
- **Negative**
  - Two conventions to learn. `CLAUDE.md` and this ADR say which one applies where.
  - Slices can duplicate small bits of logic; shared rules are pulled into a small file (`ProductRules`) only when a
    second slice needs them.
- **When to revisit**: if Inventory grows reservation release, expiry and shipping, its domain earns the same
  treatment as Ordering (an aggregate and its own project), and that move is mechanical because the rule already
  lives outside the handlers.

## Alternatives considered

| Option | Why not |
|---|---|
| Clean Architecture everywhere | Four projects and pass-through layers for CRUD; slows every change for no protection |
| Vertical Slice everywhere | Ordering's invariants and state machine would leak into handlers and be tested only through HTTP |
| MediatR for commands | Decorators are hand-written in ~30 lines each; one less dependency (and MediatR is now commercially licensed) |
