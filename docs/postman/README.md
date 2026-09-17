# Postman collections

One collection per service, added in the phase that builds the service.
Each request has test scripts, so a collection doubles as a quick smoke test.

| Collection | Service | Base URL variable |
|---|---|---|
| [catalog.postman_collection.json](catalog.postman_collection.json) | Catalog | `catalogBaseUrl` |
| [ordering.postman_collection.json](ordering.postman_collection.json) | Ordering | `orderingBaseUrl` |
| [inventory.postman_collection.json](inventory.postman_collection.json) | Inventory | `inventoryBaseUrl` (also uses `orderingBaseUrl`) |

[local.postman_environment.json](local.postman_environment.json) holds the base URL of every service when running
through Aspire. Collections also define their base URL as a collection variable, so they work without the environment.
Once the Gateway exists (phase 6), point the base URLs to it in a separate environment.

## Use

1. Start the system: `dotnet run --project src/AppHost`.
2. In Postman: **Import** the collection(s) and the environment, then select the environment.
3. Run requests one by one, or run the whole collection with the **Collection Runner** (requests are ordered so
   values such as `productId` flow from one request to the next).

From the command line with [newman](https://github.com/postmanlabs/newman):

```bash
npx newman run docs/postman/catalog.postman_collection.json -e docs/postman/local.postman_environment.json
npx newman run docs/postman/ordering.postman_collection.json -e docs/postman/local.postman_environment.json
npx newman run docs/postman/inventory.postman_collection.json -e docs/postman/local.postman_environment.json
```

Command requests create their own test data (e.g. `POSTMAN-TEST-*` products) and never modify seed data.
The Ordering collection places orders for a new `postman-<timestamp>` customer per run (sent in the temporary
`X-Customer-Id` header until JWT arrives in phase 6), so its list assertions only see that run's orders.

The Inventory collection is the one that exercises the **whole order flow**: its *Order flow (end to end)* folder
places orders in Ordering, polls until the choreography saga answers (`Confirmed` / `Rejected`) and then checks the
stock that moved. It changes stock levels on purpose, but stays repeatable: it tops the SKU up before reserving and
the rejected path asks for more packs than the warehouse could ever hold.
