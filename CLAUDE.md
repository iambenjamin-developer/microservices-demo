# CLAUDE.md

Guidance for AI coding assistants working in this repository.

## Project

B2B beer ordering platform (inspired by the BEES model) built as a .NET microservices MVP.
The full plan, scope and phase list live in [docs/implementation-plan.md](docs/implementation-plan.md) — read it before making changes.

## Conventions

- **Language:** all code, comments, commit messages and documentation are in English. `README_pt.md` is the only Portuguese file.
- **Commits:** Conventional Commits (`feat(ordering): ...`, `test: ...`, `docs: ...`), small and focused.
- **Workflow:** implement one phase at a time (one session per phase); stop at the end of each phase for human review before committing. When a phase is approved, update its Status and Phase notes in `docs/implementation-plan.md` in the same commit.
- **Catalog data is fictional.** Do not use real beer brands.
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
- **Catalog** and **Inventory** use Vertical Slice Architecture (one folder per feature: endpoint + request + handler + validator).
- Synchronous HTTP calls between services use explicit **Polly** resilience pipelines (retry with backoff + jitter, circuit breaker, timeout) via `AddResilienceHandler`.
- Application code returns `Result`/`Result<T>` instead of throwing for expected failures; APIs map errors to RFC 9457 `ProblemDetails`.

## Commands

```bash
dotnet build microservices-demo.slnx
dotnet test microservices-demo.slnx
dotnet run --project src/AppHost
```

## Testing

- Unit tests: xUnit + Shouldly + NSubstitute; name tests `Method_State_ExpectedResult`.
- Integration tests use Testcontainers (Docker must be running) — never mock the database.
