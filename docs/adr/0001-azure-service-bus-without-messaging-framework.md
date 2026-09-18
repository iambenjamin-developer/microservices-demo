# ADR 0001 — Azure Service Bus SDK with a hand-written outbox and inbox

- **Status:** Accepted
- **Date:** 2026-09-16

## Context

Ordering, Inventory and Notifications integrate asynchronously. Two problems have to be solved no matter which
library is used:

1. **Dual write.** Saving an order and publishing `OrderPlaced` are two operations against two systems. If the
   process dies between them, either the order exists and nobody hears about it, or the event announces an order
   that was rolled back.
2. **At-least-once delivery.** Service Bus redelivers a message whose lock expired or whose handler failed, and the
   outbox itself can publish a row twice (sent, then crashed before marking it processed). Consumers see duplicates.

The usual answer in .NET is a messaging framework — MassTransit, NServiceBus, Wolverine, Rebus — which ships an
outbox, an inbox, retries and topology management. The broker is **Azure Service Bus**, run locally through the
official **emulator**.

## Decision

Use the plain `Azure.Messaging.ServiceBus` SDK and write the two patterns by hand in `BuildingBlocks.Messaging`:

- **Transactional outbox.** Integration events are stored in an `outbox_messages` table in the same transaction as the
  business change (Ordering does it with a `SaveChangesInterceptor` that translates domain events; Inventory stages
  them through `IOutbox`). `OutboxProcessor<TDbContext>` polls the table and publishes with
  `SELECT ... FOR UPDATE SKIP LOCKED`, so several replicas can run it without publishing a row twice, and gives up on
  a row after `MaxAttempts`, leaving it for inspection.
- **Idempotent consumer (inbox).** `ServiceBusSubscriptionProcessor<TDbContext>` checks `inbox_messages`
  (`MessageId` + consumer), invokes the handler and commits **business change + outgoing outbox rows + inbox row** in
  one `SaveChangesAsync`. Handlers never save. A unique violation on the inbox key is treated as a concurrent
  redelivery; anything else is rethrown so the message is retried and, after 5 deliveries, dead-lettered.
- **Topology as code.** `Topology.cs` in `BuildingBlocks.Contracts` maps events to topics and declares the
  subscriptions with their `Subject` correlation filters. The AppHost provisions the emulator from it; docker-compose
  uses an equivalent `config.json` kept in sync by hand.
- **Tracing.** The publisher writes the W3C `traceparent` into the message and the consumer restarts the activity from
  it, so one order is one distributed trace.

## Consequences

- **Positive**
  - The patterns are visible: a reviewer can read the outbox query and the inbox check in two files, which is the
    point of a demo meant to be explained.
  - No framework license. MassTransit moved to a commercial license from v9; the SDK is MIT.
  - No conflict with the emulator. The emulator does not support management operations, so frameworks that create
    topics, subscriptions and rules at startup need their auto-provisioning disabled and their naming conventions
    matched by hand. Here the topology is ours from the start.
  - Nothing hidden between the code and the broker: `MessageId`, `Subject`, `CorrelationId` are set explicitly.
- **Negative**
  - We own the code: polling interval, batch size, retries, poison-row handling, cleanup of processed rows (not done
    yet — a periodic delete of old `processed_on_utc` rows is the next step).
  - Features a framework gives for free are missing: sagas with timeouts, scheduled redelivery with backoff,
    message versioning, a test harness.
  - Polling adds up to one interval (1 s) of latency; PostgreSQL `LISTEN/NOTIFY` or CDC (Debezium) would remove it.
- **Neutral**
  - The Notifications function does not use the consumer pipeline: the Functions host owns the receive loop. It
    repeats the inbox idea by hand (notification + inbox row in one commit) and has no outbox because it publishes
    nothing.

## Alternatives considered

| Option | Why not (here) |
|---|---|
| MassTransit | Commercial license from v9; emulator needs topology workarounds; hides the pattern the demo wants to show |
| NServiceBus | Commercial; heavier than a three-event flow needs |
| Wolverine | Good fit and MIT, but another framework to explain; its outbox needs its own storage integration |
| Publish directly after `SaveChanges` | The dual-write problem this ADR exists for |
| Distributed transaction (2PC) | Not supported by Service Bus with PostgreSQL, and couples availability of both |

For a larger system with many flows, adopting Wolverine or a licensed MassTransit is the reasonable move; the
contracts, the topology and the idempotent handlers would carry over.
