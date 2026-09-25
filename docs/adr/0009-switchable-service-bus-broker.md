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
  - Any other value fails at startup.
  - The launch profile `https-azure` sets `Messaging__Broker=Azure`, so switching is choosing a profile.
- **Same topology in both.** The loop over `Topology.SubscriptionDefinitions` (topics, subscriptions,
  `MaxDeliveryCount = 5`, one correlation rule per `Subject`) runs for either broker; the generated Bicep contains
  exactly what the emulator gets.
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
  - Compose in Azure mode depends on Aspire having provisioned the namespace first (or on the same topology created
    by other means).
  - `docker/servicebus/config.json` still mirrors `Topology.cs` by hand, for the emulator under compose only.
- **Neutral**
  - Azurite keeps emulating the Functions host storage in both modes: it is host bookkeeping, not messaging.
  - Integration tests are unaffected: they replace `IEventBus` with a fake and never touch a broker.

## Alternatives considered

| Option | Why not (here) |
|---|---|
| `if (emulator)` in the services (e.g. two `IEventBus` implementations) | The SDK already talks to both; a branch in the code would be the only thing that could make them differ |
| `RunAsExisting` on a namespace created in the portal | Topology and roles would have to be kept in step outside the code; still possible later by adding it to `AddMessaging` |
| A plain `AddConnectionString("messaging")` with a SAS key in user-secrets | Needs a separate provisioning script (a second source of truth) and puts a key in every Aspire run |
| Entra ID for compose too (service principal secret in `.env`) | Still a secret in `.env`, with broader scope and more setup than a send/listen policy |

Revisit when the services are deployed to Azure (Container Apps or AKS): they would use managed identities, and
local auth on the namespace could be disabled.
