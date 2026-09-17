# Postman collections

One collection per service, added in the phase that builds the service.
Each request has test scripts, so a collection doubles as a quick smoke test.

| Collection | Service | Base URL variable |
|---|---|---|
| [gateway.postman_collection.json](gateway.postman_collection.json) | Gateway | `gatewayBaseUrl` |
| [catalog.postman_collection.json](catalog.postman_collection.json) | Catalog | `catalogBaseUrl` (also uses `gatewayBaseUrl`) |
| [ordering.postman_collection.json](ordering.postman_collection.json) | Ordering | `orderingBaseUrl` (also uses `gatewayBaseUrl`) |
| [inventory.postman_collection.json](inventory.postman_collection.json) | Inventory | `inventoryBaseUrl` (also uses `orderingBaseUrl` and `gatewayBaseUrl`) |
| [notifications.postman_collection.json](notifications.postman_collection.json) | Notifications | `notificationsBaseUrl` (also uses `orderingBaseUrl` and `gatewayBaseUrl`) |

[local.postman_environment.json](local.postman_environment.json) holds the base URL of every service when running
through Aspire. Collections also define their base URL as a collection variable, so they work without the environment.

## Authentication

Every endpoint requires a JWT. Only the Gateway issues one, so every collection starts with a **Sign in** folder
that calls `POST {{gatewayBaseUrl}}/auth/token` and stores the token in a collection variable; the collection then
sends it as a bearer token. Demo accounts, all with password `demo`:

| User | Role | Can |
|---|---|---|
| `bar`, `market` | `PointOfSale` | Read the catalog and the stock, place and read **their own** orders |
| `admin` | `Admin` | Everything above, plus `POST`/`PUT /api/products` and `PUT /api/stock/{sku}` |

The service collections call the services directly on purpose: a token issued by the Gateway is validated by each
service on its own, so the same request works with or without the proxy in between.

`POST /auth/token` is rate limited (10 requests per minute per client), so running several collections back to back
within a minute can answer `429`.

## Use

1. Start the system: `dotnet run --project src/AppHost`.
2. In Postman: **Import** the collection(s) and the environment, then select the environment.
3. Run requests one by one, or run the whole collection with the **Collection Runner** (requests are ordered so
   values such as `productId` flow from one request to the next).

From the command line with [newman](https://github.com/postmanlabs/newman):

```bash
npx newman run docs/postman/gateway.postman_collection.json -e docs/postman/local.postman_environment.json
npx newman run docs/postman/catalog.postman_collection.json -e docs/postman/local.postman_environment.json
npx newman run docs/postman/ordering.postman_collection.json -e docs/postman/local.postman_environment.json
npx newman run docs/postman/inventory.postman_collection.json -e docs/postman/local.postman_environment.json
npx newman run docs/postman/notifications.postman_collection.json -e docs/postman/local.postman_environment.json
```

Command requests create their own test data (e.g. `POSTMAN-TEST-*` products) and never modify seed data.
The Ordering collection signs in as `bar` and asserts that the order it just placed leads the list; a second sign-in
as `market` shows that one point of sale gets a `404`, not a `403`, for another one's order.

The Inventory collection is the one that exercises the **whole order flow**: its *Order flow (end to end)* folder
places orders in Ordering, polls until the choreography saga answers (`Confirmed` / `Rejected`) and then checks the
stock that moved. It changes stock levels on purpose, but stays repeatable: it tops the SKU up before reserving and
the rejected path asks for more packs than the warehouse could ever hold.

The Notifications collection follows the same order one hop further: it places an order and polls the panel until the
Function has consumed the outcome, for both the confirmed and the rejected path, and then shows that another point of
sale does not see it. The e-mails those orders produce are captured by **Mailpit** — read them at
`http://localhost:8025`. Notifications is an Azure Function, so it also needs the Azure Functions Core Tools
(`npm i -g azure-functions-core-tools@4`) to be running under Aspire.
