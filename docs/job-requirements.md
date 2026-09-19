# Job requirements → evidence

The project exists to demonstrate, with working code, the requirements of a **Senior Backend Developer (.NET)**
role. Each row points to where the requirement is met, so a reviewer can check it instead of taking it on trust.

## Requirements map

| Requirement | Where it is demonstrated | Notes |
|---|---|---|
| **.NET / C#** | Whole backend on **.NET 10 / C# 14**; SDK pinned in [`global.json`](../global.json) | Primary constructors, records, collection expressions, `[LoggerMessage]` source-generated logging, `TimeProvider` for testable time |
| **Microservices** | Catalog, Ordering, Inventory, Notifications + Gateway ([`src`](../src)) | Database per service; the SKU is the only shared identity; no service reads another's database |
| **REST APIs** | Minimal APIs (Catalog, Gateway) and MVC controllers (Ordering, Inventory, [ADR 0007](adr/0007-controllers-for-inventory-and-ordering.md)); [`BuildingBlocks.Web`](../src/BuildingBlocks/BuildingBlocks.Web) | Proper status codes (`202 Accepted` + `Location` for an asynchronous command, `404` for someone else's order, `409`, `503`), RFC 9457 `ProblemDetails` with `code` and `traceId`, OpenAPI + Scalar, FluentValidation filters (endpoint and action) |
| **PostgreSQL + EF Core** | Four databases; EF Core 10 + Npgsql; migrations applied at startup | Complex types for value objects, `xmin` optimistic concurrency, check constraints, `FOR UPDATE SKIP LOCKED` in the outbox, `AsNoTracking` projections, execution strategy for retries |
| **Messaging (Azure Service Bus)** | [`BuildingBlocks.Messaging`](../src/BuildingBlocks/BuildingBlocks.Messaging), [`Topology.cs`](../src/BuildingBlocks/BuildingBlocks.Contracts/Topology.cs) | Topics + filtered subscriptions, transactional outbox, idempotent consumers (inbox), dead-lettering, trace context in messages ([ADR 0001](adr/0001-azure-service-bus-without-messaging-framework.md)) |
| **Azure Functions** | [`src/Functions/Notifications`](../src/Functions/Notifications) | Isolated worker on .NET 10, Service Bus trigger + HTTP trigger, worker middleware for JWT validation, idempotent storage, e-mail after commit |
| **Docker** | One multi-stage Dockerfile per deployable, [`docker-compose.yml`](../docker-compose.yml) | Restore layer cached, non-root runtime images, emulator topology and database init scripts, standalone Aspire dashboard as OTLP endpoint |
| **CI/CD** | [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) | Build with warnings as errors, unit/architecture/functional tests, Testcontainers integration tests, frontend lint + build, image builds with cache. CD (push images, deploy) is a documented next step |
| **Unit testing** | `Ordering.Domain.UnitTests`, `Ordering.Application.UnitTests`, `Inventory.UnitTests`, `Notifications.UnitTests` | xUnit v3, Shouldly, NSubstitute; `Method_State_ExpectedResult` naming; Test Data Builder |
| **Integration testing** | `Ordering.IntegrationTests`, `Inventory.IntegrationTests`, `Gateway.Tests` | `WebApplicationFactory` + Testcontainers PostgreSQL (the database is never mocked); Inventory's suite was written before a refactor to pin the HTTP and messaging contract; the Gateway tested in memory |
| **Design patterns** | [README — patterns table](../README.md#design-patterns--where-and-why) | Outbox, Inbox, Saga, Strategy, Decorator, Adapter, Null Object, Repository/UoW, Result, Factory Method, Options… each with its location and reason |
| **Clean Code / Clean Architecture** | Ordering's four projects; [`tests/Architecture.Tests`](../tests/Architecture.Tests); vertical slices in Catalog and a service layer in Inventory | Dependency rule enforced by tests, small handlers, intention-revealing names, guard clauses vs `Result` errors, `.editorconfig` enforced on build ([ADR 0002](adr/0002-architecture-style-per-service.md), [ADR 0008](adr/0008-service-layer-for-inventory.md)) |
| **AI-assisted development** | [`CLAUDE.md`](../CLAUDE.md), [`ai-workflow.md`](ai-workflow.md), [`implementation-plan.md`](implementation-plan.md) | Plan-first, one session per phase, human review before every commit, conventions written for the assistant |

## Also covered (commonly asked for in senior roles)

| Topic | Where |
|---|---|
| Resilience | Polly v8 pipeline on the Catalog client: total timeout → retry with exponential backoff + jitter → circuit breaker → attempt timeout ([`CatalogClientRegistration.cs`](../src/Services/Ordering/Ordering.Infrastructure/Catalog/CatalogClientRegistration.cs)) |
| Security | JWT validated in every service, secure-by-default fallback policy, role-based admin routes, token relay service-to-service, rate limiting, CORS allow-list, no secrets in the repository (user-secrets / `.env`) |
| Observability | OpenTelemetry traces, metrics and logs → Aspire dashboard; one distributed trace per order across HTTP and Service Bus; health checks |
| Distributed data consistency | Choreography saga with an all-or-nothing reservation instead of distributed transactions ([ADR 0003](adr/0003-choreography-over-orchestration.md)) |
| API Gateway | YARP with per-route authorization, CORS and rate limiting ([ADR 0004](adr/0004-yarp-as-api-gateway.md)) |
| Local developer experience | .NET Aspire: one command starts every service, database, broker emulator, mail catcher and the frontend |
| Build hygiene | Central Package Management, nullable, warnings as errors, code style on build |
| Documentation | README, ADRs, phase notes, Postman collections with tests |
| Frontend collaboration | React + TypeScript client that holds no business rules and talks only to the Gateway |

## Honest gaps

What the MVP does not show, and what would come next:

- **CD and cloud deployment**: no image push or deployment yet; the next step is pushing to Azure Container Registry
  and deploying to Azure Container Apps (Service Bus, PostgreSQL Flexible Server, Functions) with Bicep, promoted per
  environment from the same images.
- **Kubernetes/Helm/KEDA**, **SonarQube** and a **Datadog/Azure Monitor** exporter: out of scope, listed in the
  README's next steps.
- **Load and contract tests**: none; the event contracts are shared code, which is acceptable in a monorepo but
  would need consumer-driven contract tests across repositories.
