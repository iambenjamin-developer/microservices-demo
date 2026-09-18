# ADR 0004 — YARP as the API Gateway

- **Status:** Accepted
- **Date:** 2026-09-17

## Context

The React app and any other client need one address, and some concerns belong at the edge: issuing demo tokens,
CORS for the browser, rate limiting per caller, and rejecting obviously unauthorized calls (a point of sale calling an
admin route) before they reach a service. The system runs locally under Aspire and in docker-compose, and must be
explainable in a few minutes.

## Decision

Build the Gateway as a small ASP.NET Core host with **YARP** (`Yarp.ReverseProxy`):

- **Routes and clusters are configuration** (`ReverseProxy` in `appsettings.json`). Destinations are service
  discovery names (`https+http://catalog`), resolved by `Microsoft.Extensions.ServiceDiscovery.Yarp` from what Aspire
  or docker-compose injects, so the same file works in both.
- **Routes are split by HTTP method** so the authorization policy is part of the route: `catalog-read` (authenticated)
  vs `catalog-admin` (`POST`/`PUT`, `Admin` policy); `notifications` accepts only `GET`, so a write method never reaches
  the Function.
- The Gateway is the **only issuer** of tokens (`POST /auth/token`, HS256), but it is **not the security boundary**:
  every service validates the token again with the same `AddJwtAuthentication()` (see the zero-trust note in the
  README). The Gateway's checks are an early, cheap rejection.
- Middleware order: exception handling → CORS → authentication → authorization → rate limiter → proxy. CORS runs
  first so a browser preflight (which carries no token) is answered instead of rejected.
- Rate limiting: fixed window partitioned by `sub` (or remote IP when anonymous); a stricter window on `/auth/token`.

## Consequences

- **Positive**
  - It is .NET: same language, same `ProblemDetails`, same OpenTelemetry and health checks as the services; the
    Gateway is tested in memory with `WebApplicationFactory` like any other API (`tests/Gateway.Tests`).
  - No extra infrastructure to run locally; it starts with the AppHost.
  - Anything YARP does not do out of the box is ordinary middleware (token endpoint, rate limiting).
- **Negative**
  - We operate it: scaling, TLS, WAF, developer portal and API keys are not included.
  - Token issuing in a gateway is a demo shortcut. In production an identity provider (Entra ID, Keycloak) issues
    tokens and the Gateway only validates and routes.

## How it would evolve in production

- Behind **Azure Front Door / Application Gateway** (TLS, WAF) and scaled horizontally; the rate limiter would move to
  a distributed store (Redis) or to the edge, because a fixed window in memory is per instance.
- If the company needs API products, subscription keys, a developer portal and policies managed by a platform team,
  **Azure API Management** in front of (or instead of) YARP. YARP remains useful as a backend-for-frontend.

## Alternatives considered

| Option | Why not (here) |
|---|---|
| Azure API Management | Cloud resource, no local emulator, cost; policies in XML outside the codebase |
| Kong / Envoy / Traefik | Another runtime and configuration language; auth plugins instead of the shared .NET code |
| Ocelot | Less active, configuration-only, weaker integration with service discovery and rate limiting |
| No gateway (clients call services) | The browser would need every service's address, CORS on every service, and no single place for edge policies |
