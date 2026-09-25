# ADR 0009 — Switchable Service Bus broker: local emulator or a real Azure namespace

- **Status:** Accepted (complements [ADR 0001](0001-azure-service-bus-without-messaging-framework.md))
- **Date:** 2026-09-25

## Context

Until now the broker was always the Service Bus **emulator**: `RunAsEmulator` in the AppHost and a container in
`docker-compose.yml`. The emulator is what makes the repository run for anyone who clones it, but it is not Azure: it
has no management plane, no Entra ID, no quotas or throttling, and its behaviour can drift from the real service.
Showing the system against a real namespace, and being able to go back to the emulator in seconds, is the goal.

The code did not have to change for it. Ordering and Inventory (`AddServiceBusMessaging()` →
`AddAzureServiceBusClient("messaging")`) and the Notifications function (`[ServiceBusTrigger(Connection =
"messaging")]`) only read the `messaging` connection. Nothing below the AppHost knows the emulator exists.

## Decision

**The broker is chosen at the infrastructure edge, never in the services.**

- **Aspire.** `builder.AddMessaging()` (`src/AppHost/MessagingResourceExtensions.cs`) reads `Messaging:Broker`:
  - `Emulator` (default, when unset): the emulator container and its SQL Server sidecar, as before.
  - `Azure`: no `RunAsEmulator`, so Aspire **provisions** a Standard namespace (Basic has no topics) through Bicep
    in the subscription set in the AppHost user-secrets (`Azure:SubscriptionId`, `Azure:Location`,
    `Azure:CredentialSource`). It assigns *Azure Service Bus Data Owner* to the developer running it, and injects
    the namespace endpoint instead of a connection string: the services and the function authenticate with Entra ID
    (`DefaultAzureCredential`, i.e. `az login`) and hold no key.
  - `ConnectionString`: an **existing** namespace (e.g. created by hand in the portal) reached through
    `ConnectionStrings:messaging` in the AppHost user-secrets (`AddConnectionString`, a secret parameter). Aspire
    creates nothing in Azure; every consumer, the function included, gets the string as `ConnectionStrings__messaging`.
  - Any other value fails at startup.
  - The launch profiles `https-azure` and `https-connectionstring` set `Messaging__Broker`, so switching is choosing a
    profile.
  - `AddMessaging()` returns `IResourceBuilder<IResourceWithConnectionString>`, so the function is wired with
    `WithMessagingReference()`: for a Service Bus resource it calls the Functions-specific `WithReference` (which
    injects `messaging__fullyQualifiedNamespace`); the generic overload would compile and inject an endpoint the
    trigger cannot use.
- **One topology for every broker.** `Topology.cs` is the only definition. The AppHost applies it to the emulator
  and to the namespaces it provisions (topics, subscriptions, `Topology.MaxDeliveryCount`, one correlation rule per
  `Subject`); `tools/servicebus-provision.cs` applies the same model to an existing namespace. The tool is
  idempotent and reconciles: it creates what is missing, aligns the subscription settings and leaves exactly the
  declared rules (the catch-all `$Default` rule is removed). It needs Manage rights, which the services never get.
- **docker-compose.** The emulator and its SQL Server are in the `emulator` compose profile, the services depend on
  it with `required: false`, and `ConnectionStrings__messaging` is `${SERVICEBUS_CONNECTION:-<emulator>}`. Switching
  is two `.env` lines: `COMPOSE_PROFILES=emulator` or `COMPOSE_PROFILES=` plus `SERVICEBUS_CONNECTION`.
- **SAS only where there is no identity.** Containers started by compose have no Entra ID identity, so in Azure mode
  the namespace keeps local auth enabled (`DisableLocalAuth = false`, Aspire's default is `true`) and gets a
  namespace policy named `compose` with **Send + Listen only**. Compose uses that policy; the root key is never used.

## Consequences

- **Positive**
  - One switch per way of running, no code branch: the services cannot behave differently per broker because they
    cannot tell which one they have.
  - `Topology.cs` stays the single source of truth for Azure as well; nothing is created by hand in the portal.
  - Aspire runs are passwordless against Azure.
  - The emulator remains the default, so cloning and running still needs no Azure account.
- **Negative**
  - **Cost.** A Standard namespace is billed while it exists (a base monthly fee plus operations), even when nothing
    runs. Delete the resource group Aspire created when done (`az group delete --name <rg>`).
  - Local auth is enabled on the namespace for compose's sake, which is a weaker posture than Entra ID only. It is
    limited to one send/listen policy; a namespace used only through Aspire could turn it off again.
  - Compose against Azure needs a namespace that already has the topology: provisioned by Aspire (`Azure`) or by
    `tools/servicebus-provision.cs` (`ConnectionString`).
  - With an existing namespace the topology is applied by hand, once and after every change to `Topology.cs`;
    nothing warns if it drifts (running the tool with `--dry-run` shows it).
  - `docker/servicebus/config.json` still mirrors `Topology.cs` by hand, for the emulator under compose only.
- **Neutral**
  - Azurite keeps emulating the Functions host storage in both modes: it is host bookkeeping, not messaging.
  - Integration tests are unaffected: they replace `IEventBus` with a fake and never touch a broker.

## Alternatives considered

| Option | Why not (here) |
|---|---|
| `if (emulator)` in the services (e.g. two `IEventBus` implementations) | The SDK already talks to both; a branch in the code would be the only thing that could make them differ |
| `RunAsExisting` on a namespace created in the portal | Needs Azure provisioning rights and the resource group at every run, where a connection string is all the services need; still possible later by adding it to `AddMessaging` |
| Provisioning the topology from the AppHost at startup in `ConnectionString` mode | Every run would need a Manage key; a one-off tool keeps that key out of the running system |
| Entra ID for compose too (service principal secret in `.env`) | Still a secret in `.env`, with broader scope and more setup than a send/listen policy |

Revisit when the services are deployed to Azure (Container Apps or AKS): they would use managed identities, and
local auth on the namespace could be disabled.
