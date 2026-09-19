# ADR 0008 — Service layer (controller → service interface → implementation) for Inventory

- **Status:** Accepted (amends [ADR 0002](0002-architecture-style-per-service.md) for Inventory)
- **Date:** 2026-09-19

## Context

After [ADR 0007](0007-controllers-for-inventory-and-ordering.md) Inventory was vertical slices behind MVC
controllers: `GetStockController` → `GetStockHandler`, `UpdateStockController` → `UpdateStockHandler`, each handler
using the `DbContext` directly. The `OrderPlaced` consumer had its own copy of the data access: it loaded the rows,
called `StockReservation.Reserve` and staged the outcome.

Two things made that worth changing:

- **Stock has two entry points.** An operator sets quantities over HTTP and orders reserve them from the bus. With
  slices, "how stock is read and changed" was spread over two handlers and a message handler. A service that owns
  every stock operation gives both entry points one place to call.
- **The most common style is missing.** Catalog shows vertical slices and Ordering shows CQRS handlers behind Clean
  Architecture. Most existing .NET services a developer joins are *controller → service interface → implementation*.
  Inventory is small enough to show it without ceremony, and it is the one service where the style also solves the
  problem above.

The constraint, as in ADR 0007: **no client can notice.** Same routes, status codes, bodies, OpenAPI operations,
and the same `StockReserved` / `StockRejected` messages in the outbox.

## Decision

- **`IStockService`** (public, `Inventory.Api/Services`) owns every stock operation. It has one implementation,
  `StockService` (internal, sealed), registered with `AddScoped<IStockService, StockService>()`:
  - `GetStockAsync(skus)` — the list, sorted by SKU, through the Mapperly projection (`ProjectToResponse()`).
  - `UpdateQuantityAvailableAsync(sku, UpdateStockRequest)` → `Result<StockResponse>`. **Commits**: an HTTP request
    is its own unit of work. An unknown SKU is `StockErrors.NotFound`. The SKU comes from the route; the body is
    passed as is, so a new field in it does not change the signature.
  - The list takes the SKUs, not `GetStockRequest`: that record is an MVC binding model (`[FromQuery(Name = "sku")]`)
    and would tie the service to query-string binding.
  - `StageReservationAsync(lines)` → `ReservationOutcome`. **Does not commit**: it changes tracked rows and the
    consumer pipeline saves them together with the outcome in the outbox and the inbox row, in one transaction.
  - The saving policy is in the method names ("Update" vs "Stage") and in the XML docs, because a caller that gets
    it wrong either commits half a saga step or loses a change.
- **One `StockController`** (`Inventory.Api/Controllers`) replaces the two slice controllers. It depends only on
  `IStockService` (constructor injection) and translates HTTP to service calls and `Result`s to HTTP.
- **The `OrderPlaced` handler** depends on `IStockService` and `IOutbox`: it builds the reservation lines, stages
  the outcome event and logs. Because the service and the `DbContext` are both scoped, the handler, the service and
  the pipeline share one context, which is what makes "stage, then the pipeline commits" work.
- **Unchanged:** `StockReservation.Reserve` stays a pure function (the service calls it), and so do the domain,
  `StockErrors`, `StockMapper`, the validators and the request/response records. The folders are now
  technical: `Controllers`, `Services`, `Contracts`, `Validators`, `Mapping`, `Domain`, `Persistence`, `Messaging`.
- **Tests:** controller unit tests mock `IStockService` with NSubstitute (the service is the controller's only
  dependency). The service is **not** tested against a mocked `DbContext`: `Inventory.IntegrationTests`
  (WebApplicationFactory + Testcontainers PostgreSQL) covers it through the HTTP API and the registered
  `OrderPlaced` handler. Those integration tests were written and passing **before** the refactor, to pin the
  contract it had to keep.

## Consequences

- **Positive**
  - Every way stock changes is listed in one interface; the HTTP API and the consumer share it.
  - Controllers are trivial to unit test by mocking one interface, which is how most teams test this style.
  - The repository now shows three styles, each in the service it suits: vertical slices (Catalog), Clean
    Architecture + CQRS (Ordering) and a service layer (Inventory).
- **Negative**
  - An interface with one implementation. Here it earns its keep as the seam for the controller tests and as the
    contract both entry points share; in a service with a single caller and no tests at that level it would be
    ceremony.
  - A service class grows by accretion: every new stock operation lands in `StockService`. Slices kept each use case
    apart. If it grows past a handful of operations, split it by responsibility (e.g. queries and reservations).
  - Mixed saving policies in one class are a trap the names and docs have to guard against (see above).
- **Verified:** the Inventory OpenAPI document has the same content before and after (only the order of the two paths
  changed); the 13 integration tests pass unchanged.
- **When to revisit**: if reservations grow release, expiry and shipping, the ADR 0002 note still applies — the
  domain moves into its own project, and `StockService` becomes the application layer in front of it.

## Alternatives considered

| Option | Why not (here) |
|---|---|
| Keep vertical slices | Works, but the consumer keeps its own copy of the stock access, and the most common style stays missing |
| Service without an interface (inject `StockService`) | Fewer files, but the controller tests would need a real database or a mocked `DbContext` |
| Repository (`IStockRepository`) under the service | EF Core's `DbContext` is already the unit of work and repository; another layer would pass calls through |
| Service commits in every method | The reservation would be saved without its outbox and inbox rows: a lost or duplicated saga step |
| Mock the `DbContext` in service unit tests | A fake agrees with whatever the test expects; PostgreSQL does not (see `CLAUDE.md`) |
