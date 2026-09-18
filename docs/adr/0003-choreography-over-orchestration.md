# ADR 0003 — Choreography saga for the order flow

- **Status:** Accepted
- **Date:** 2026-09-16

## Context

Placing an order touches three services with three databases: Ordering creates the order, Inventory reserves stock,
Notifications records and e-mails the outcome. There is no transaction spanning them, and there should not be one: a
distributed transaction would make every service's availability depend on all the others.

The flow needs **eventual consistency** with a defined way to undo or refuse, i.e. a *saga*. A saga can be:

- **Choreographed** — each service reacts to events and emits its own; nobody holds the whole flow.
- **Orchestrated** — a coordinator (process manager, Durable Functions, a workflow engine) sends commands and keeps
  the state of each instance, including timeouts.

## Decision

Use **choreography**:

1. Ordering saves the order as `Pending` and publishes `OrderPlaced` (outbox), answering the client `202 Accepted`.
2. Inventory reserves **all or nothing** and publishes `StockReserved` or `StockRejected` (reason + SKUs).
3. Ordering moves the order to `Confirmed` or `Rejected` and publishes `OrderConfirmed` / `OrderRejected`.
4. Notifications records the outcome and sends the e-mail. It publishes nothing: it is the end of the flow.

The **compensation** is built into the design rather than added afterwards: because the reservation is all or
nothing, a rejected order never leaves partial reservations behind, so there is nothing to undo. A rejection is a
normal business outcome carried by an event, not an error.

State transitions are guarded by the aggregate (`Pending` is the only state that can move, and both outcomes are
terminal), so a duplicated or late event becomes a `Conflict` that the consumer completes without side effects.

## Consequences

- **Positive**
  - Services stay decoupled: Inventory does not know Ordering exists beyond the event contracts; adding a consumer
    (e.g. analytics on `OrderConfirmed`) touches no existing service.
  - No coordinator to deploy, scale or keep highly available.
  - With three steps, the flow is still readable from the sequence diagram in the README.
- **Negative**
  - The flow is implicit: to understand it you read several services. The sequence diagram and the distributed trace
    (the `traceparent` travels in every message) are the mitigation.
  - **No timeout.** If Inventory is down, an order stays `Pending` until it comes back (the message waits on the
    subscription, nothing is lost). A choreography has no natural owner for "give up after 5 minutes".
  - Cyclic event dependencies become easy to create as the flow grows.
- **When an orchestrated saga is preferable**: more than three or four steps, steps that need to be undone
  (payment captured, then shipping fails), business deadlines ("reject if not reserved in 10 minutes") or a need to
  answer "where is order X in the flow?" from one place. On Azure that would be **Durable Functions** or a process
  manager persisted with the order; the events already defined would become its inputs.

## Alternatives considered

| Option | Why not (here) |
|---|---|
| Orchestration (Durable Functions / process manager) | A coordinator and its state for a three-step, no-timeout flow |
| Synchronous calls (Ordering → Inventory over HTTP) | Temporal coupling: Inventory down means no orders; retries risk double reservations without idempotency keys |
| Distributed transaction | Not available across Service Bus + PostgreSQL, and it couples availability |
