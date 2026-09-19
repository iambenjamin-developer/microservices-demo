# Architecture Decision Records

Short records of the decisions that shape the system: the context, what was decided, what it costs and when to
revisit it. Format: Status, Context, Decision, Consequences, Alternatives considered.

| # | Decision | Status |
|---|---|---|
| [0001](0001-azure-service-bus-without-messaging-framework.md) | Azure Service Bus SDK with a hand-written outbox and inbox, no messaging framework | Accepted |
| [0002](0002-architecture-style-per-service.md) | Clean Architecture for Ordering, Vertical Slice for Catalog and Inventory | Accepted |
| [0003](0003-choreography-over-orchestration.md) | Choreography saga for the order flow, and when orchestration would win | Accepted |
| [0004](0004-yarp-as-api-gateway.md) | YARP as the API Gateway, and how it would evolve in production | Accepted |
| [0005](0005-object-mapping-mapster-and-manual.md) | Mapster in Ordering and Inventory, hand-written mapping in Catalog and Notifications | Superseded by 0006 |
| [0006](0006-object-mapping-mapperly.md) | Mapperly (source-generated, compile-time checked) replaces Mapster in Ordering and Inventory | Accepted |

A new ADR takes the next number. A decision that changes is not edited away: the old record is marked
*Superseded by NNNN* and the new one explains why.
