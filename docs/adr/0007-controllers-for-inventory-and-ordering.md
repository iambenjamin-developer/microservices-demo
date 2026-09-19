# ADR 0007 — MVC controllers for Inventory and Ordering, minimal APIs for Catalog and the Gateway

- **Status:** Accepted
- **Date:** 2026-09-19

## Context

Until phase 11 every HTTP service used minimal APIs: `IEndpoint` classes discovered by `AddEndpoints()`, typed
`Results<...>`, an endpoint filter for FluentValidation and `Result.ToProblem()` returning a `ProblemHttpResult`.

Most existing .NET codebases a backend developer joins are built on MVC controllers, and both styles are first-class in
ASP.NET Core 10. A demo that only shows one of them leaves out the other half of the platform: filters, model binders,
`[ApiController]` conventions, attribute routing and action results. The mapping decision (ADR 0006) already shows two
approaches side by side (Mapperly and hand-written); the HTTP layer can do the same without affecting the design.

The constraint: the move must be **invisible to clients**. The Web app, the Gateway routes, the Postman collections
and the integration tests keep working unchanged.

## Decision

- **Inventory and Ordering use MVC controllers. Catalog and the Gateway stay on minimal APIs** (Catalog is the
  minimal API reference, the Gateway is mostly YARP). Notifications is an Azure Function and is not affected.
  Styles are never mixed inside one service.
- `BuildingBlocks.Web` gains the MVC half of what the minimal API services already share:
  - `AddApiControllers()`: `AddControllers()` with the global `ValidationActionFilter`, enums as strings (MVC does not
    read `ConfigureHttpJsonOptions`) and `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes`, so the
    validators decide what is required and keep their error codes.
  - `ApiControllerBase` (`[ApiController]` + `Problem(Error)`): a failed `Result` becomes the same RFC 9457 body. The
    `Error → status/title/type` table (`ErrorProblem`) is shared with `ToProblem()`, and the body is built through
    MVC's `ProblemDetailsFactory`, which applies the `AddApiProblemDetails()` customization (`instance`, `traceId`).
  - `ValidationActionFilter`: validates every action argument that has a registered `IValidator<T>` and answers with
    the same 400 `ValidationProblem` (field → messages) as the minimal API `ValidationFilter<T>`. Global, so an action
    cannot forget it.
  - `[RequireAdmin]`: the attribute form of `RequireAdmin()`.
- **Inventory keeps its vertical slices**: one controller per slice, in the slice folder
  (`Features/Stock/GetStock/GetStockController.cs`), where `*Endpoint.cs` used to be. MVC only discovers public
  controllers, so the slice handlers and `GetStockRequest` became public; validators and mappers stay internal.
- **Ordering has one `OrdersController`** in `Ordering.Api/Controllers`. Each action takes only its handler with
  `[FromServices]`. The caller is bound by `CurrentCustomerModelBinder` (`BindingSource.Special`, so it is not an
  OpenAPI parameter and is never inferred as the body) and still comes only from the `sub`/`email` claims.
- OpenAPI metadata is kept with `[EndpointName]`, `[EndpointSummary]`, `[Tags]` and `[ProducesResponseType]`; the
  operation ids, tags and parameters of the generated documents did not change.

## Consequences

- **Positive**
  - The repository shows both HTTP styles with one error and validation contract. Status codes, bodies, headers
    (`Location` of the 202) and OpenAPI operations are the same; the tests, the Postman collections and the Web app
    did not change. A new integration test checks on the raw body that enums are still written as strings.
  - Filters and model binding are the MVC extension points developers already know, and are reused by every action.
- **Negative**
  - Two sets of plumbing in `BuildingBlocks.Web` (endpoint filter and action filter, `ProblemHttpResult` and
    `ObjectResult`), kept consistent by sharing `ErrorProblem` and `ProblemDetailsFactory`.
  - The only visible difference: a **malformed JSON body** is now answered by `[ApiController]` with a 400
    `ValidationProblem` that names the JSON path (`errors: { "$.items[0].sku": [...] }`), where minimal APIs returned a
    generic 400 ProblemDetails. The status code and content type are the same, and the body gives more detail.
  - Controllers are public, and so are the types in their signatures. For an executable with no consumers this only
    affects the tidiness of the code.
- **When to revisit**: if the team standardizes on one style, move the remaining services and delete the other half
  of the plumbing in `BuildingBlocks.Web`.

## Alternatives considered

| Option | Why not (here) |
|---|---|
| Stay on minimal APIs everywhere | Simpler, but the demo would not show MVC, which most existing services use |
| Move every service to controllers | Loses the minimal API reference; the Gateway gains nothing from MVC |
| Controllers with `IResult` returns (reuse `ToProblem()` as is) | Works, but it is a minimal API idiom inside MVC and skips `ProblemDetailsFactory` and action results |
| Internal controllers via a custom `ControllerFeatureProvider` | Keeps slice types internal, at the price of non-standard discovery the reader has to find |
| Keep the MVC implicit `[Required]` for non-nullable strings | A missing SKU would be rejected by MVC with no `code`, instead of the application's `Validation.Failed` |
