# CLAUDE.md

Guidance for AI coding assistants working in this repository.

## Project

B2B beer ordering platform (inspired by the BEES model) built as a .NET microservices MVP.
The full plan, scope and phase list live in [docs/implementation-plan.md](docs/implementation-plan.md) — read it before making changes.

## Conventions

- **Language:** all code, comments, commit messages and documentation are in English. The only exceptions are the README translations: `README_pt.md` (Portuguese) and `README_es.md` (Spanish), and the Spanish interview practice guide `docs/faq_es.md`. When code quoted in `docs/faq_es.md` changes, update the snippet (and its `#L` line link) in the same commit.
- **Commits:** Conventional Commits (`feat(ordering): ...`, `test: ...`, `docs: ...`), small and focused.
- **Workflow:** implement one phase at a time (one session per phase); stop at the end of each phase for human review before committing. When a phase is approved, update its Status and Phase notes in `docs/implementation-plan.md` in the same commit.
- **Catalog data is fictional.** Do not use real beer brands.
- **Postman:** every service phase adds or updates `docs/postman/<service>.postman_collection.json` (requests with test scripts, happy and error paths), its base URL in `local.postman_environment.json` and the table in `docs/postman/README.md`.
- **Secrets are never committed.** Use .NET user-secrets (Aspire) or a git-ignored `.env` (docker-compose).

## Build rules

- Target `net10.0`; SDK pinned in `global.json`.
- Shared MSBuild settings in `Directory.Build.props`: nullable enabled, warnings as errors, code style enforced on build.
- **Central Package Management:** add NuGet versions only to `Directory.Packages.props`; `PackageReference` items in projects must not specify `Version`.
- Style rules are defined in `.editorconfig` (file-scoped namespaces, `_camelCase` private fields, `I` prefix for interfaces).

## Architecture rules

- **Database per service.** A service never reads another service's database.
- Services integrate **asynchronously** through Azure Service Bus integration events (`BuildingBlocks.Contracts`) and publish them **only through the transactional outbox**.
- Consumers must be **idempotent** (inbox table keyed by `MessageId`).
- **Ordering** follows Clean Architecture: `Domain` has no dependencies; `Application` depends only on `Domain`; `Infrastructure` and `Api` are outer layers. Enforced by `tests/Architecture.Tests`.
- **Catalog** uses Vertical Slice Architecture (one folder per feature: endpoint + request + handler + validator).
- **Inventory** uses a service layer (ADR 0008): `StockController` → `IStockService` → `StockService` (internal, sealed, scoped). Every stock operation goes through `IStockService`, from the HTTP API and from the `OrderPlaced` consumer; the controller never touches the `DbContext`. Methods that save say so (`Update...`); methods called inside the consumer pipeline only stage tracked changes (`Stage...`) and never call `SaveChangesAsync`. Controller unit tests mock `IStockService`; the service itself is covered by `Inventory.IntegrationTests`.
- **HTTP style (ADR 0007):** Catalog and the Gateway use minimal APIs (`IEndpoint`); Ordering and Inventory use MVC controllers deriving from `ApiControllerBase`, registered with `AddApiControllers()`. Never mix styles inside a service. Failed `Result`s become `ToProblem()` / `Problem(error)`; FluentValidation runs through `WithRequestValidation<T>()` / the global `ValidationActionFilter`.
- **Notifications** is an Azure Function (isolated worker). The Functions host owns the receive loop, so it has no outbox and no `IEventBus`; it writes its own inbox check and commits the notification and the inbox row together. E-mail is sent **after** that commit and never fails the message.
- Synchronous HTTP calls between services use explicit **Polly** resilience pipelines (retry with backoff + jitter, circuit breaker, timeout) via `AddResilienceHandler`.
- Application code returns `Result`/`Result<T>` instead of throwing for expected failures; APIs map errors to RFC 9457 `ProblemDetails`.
- **Auth:** only the Gateway issues JWTs (`POST /auth/token`); every service validates them itself through `AddJwtAuthentication()` from `BuildingBlocks.Web`. Endpoints are authenticated by default (fallback policy) — mark public ones `AllowAnonymous` and back-office ones `RequireAdmin()` (minimal APIs) or `[RequireAdmin]` (controllers). Caller identity comes from the `sub`/`email` claims, never from a header or a request body.
- **Mapping:** Ordering and Inventory use **Mapperly** (source generator, ADR 0006): one `static partial` mapper per area with `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]`, extension methods (`ToResponse()`, `ProjectToResponse()` for EF queries), no DI and no `IMapper`. Computed members are expression-bodied helpers so Mapperly can inline them into projections. Mappings only go entity/aggregate → DTO; aggregates are always created through their factory methods. **Catalog and Notifications map by hand on purpose** — do not add a mapper to them.
- **Web** (`src/Web`, React + Vite + TypeScript, plain CSS) talks **only to the Gateway** and holds no business rules: totals, discounts and authorization come from the services, and the order status is followed by polling until it is terminal. Keep the dev server on port 5173 — it is in the Gateway's CORS allow-list.

## Commands

```bash
dotnet build microservices-demo.slnx
dotnet test microservices-demo.slnx
dotnet run --project src/AppHost
```

Frontend (`src/Web`, also started by the AppHost):

```bash
npm --prefix src/Web install
npm --prefix src/Web run lint
npm --prefix src/Web run build
```

## Testing

- Unit tests: xUnit + Shouldly + NSubstitute; name tests `Method_State_ExpectedResult`.
- Integration tests use Testcontainers (Docker must be running) — never mock the database.
