#:package Azure.Messaging.ServiceBus@7.20.2
#:package Azure.Identity@1.21.0
#:project ../src/BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false

// Applies the topology from BuildingBlocks.Contracts/Topology.cs to an existing Service Bus namespace (one created by
// hand in the portal, used with the AppHost "ConnectionString" broker or with docker-compose). Aspire does this on its
// own for the emulator and for the namespaces it provisions; this is the same model for everything else.
//
//   dotnet run tools/servicebus-provision.cs             apply the changes
//   dotnet run tools/servicebus-provision.cs -- --dry-run   only print them
//
// It is idempotent: it creates the missing topics and subscriptions, aligns their settings, and makes each
// subscription's rules exactly one correlation rule per Subject it consumes. That removes the catch-all "$Default"
// rule and any rule Topology.cs does not declare.
//
// Managing entities needs more than the services do. Connect with one of:
//   SERVICEBUS_CONNECTION  a connection string with Manage rights (e.g. RootManageSharedAccessKey)
//   SERVICEBUS_NAMESPACE   <namespace>.servicebus.windows.net — Entra ID (az login), needs "Azure Service Bus Data Owner"

using Azure.Identity;
using Azure.Messaging.ServiceBus.Administration;
using BuildingBlocks.Contracts;

var dryRun = args.Contains("--dry-run");
var fullyQualifiedNamespace = Environment.GetEnvironmentVariable("SERVICEBUS_NAMESPACE");
var connectionString = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION");

ServiceBusAdministrationClient client;

if (!string.IsNullOrWhiteSpace(fullyQualifiedNamespace))
{
    client = new ServiceBusAdministrationClient(fullyQualifiedNamespace, new DefaultAzureCredential());
}
else if (!string.IsNullOrWhiteSpace(connectionString))
{
    client = new ServiceBusAdministrationClient(connectionString);
}
else
{
    Console.WriteLine("Set SERVICEBUS_CONNECTION (with Manage rights) or SERVICEBUS_NAMESPACE.");
    return 1;
}

var changes = 0;

foreach (var topicSubscriptions in Topology.SubscriptionDefinitions.GroupBy(s => s.Topic))
{
    var topic = topicSubscriptions.Key;

    if (!await client.TopicExistsAsync(topic))
    {
        await ApplyAsync($"create topic '{topic}'", () => client.CreateTopicAsync(topic));
    }

    foreach (var definition in topicSubscriptions)
    {
        await ProvisionSubscriptionAsync(definition);
    }
}

Console.WriteLine(changes == 0
    ? "The namespace already matches Topology.cs."
    : dryRun ? $"{changes} change(s) pending (dry run, nothing applied)." : $"{changes} change(s) applied.");
return 0;

async Task ProvisionSubscriptionAsync(SubscriptionDefinition definition)
{
    var path = $"{definition.Topic}/{definition.Name}";

    if (!await client.SubscriptionExistsAsync(definition.Topic, definition.Name))
    {
        // Created together with its first rule, so it never exists with the catch-all "$Default" rule.
        var options = new CreateSubscriptionOptions(definition.Topic, definition.Name)
        {
            MaxDeliveryCount = Topology.MaxDeliveryCount,
            DeadLetteringOnMessageExpiration = true,
        };

        await ApplyAsync(
            $"create subscription '{path}' with rule '{definition.Subjects[0]}'",
            () => client.CreateSubscriptionAsync(options, CorrelationRule(definition.Subjects[0])));

        if (dryRun)
        {
            // The subscription does not exist yet, so its rules cannot be listed; report the rest and stop.
            foreach (var subject in definition.Subjects.Skip(1))
            {
                await ApplyAsync($"add rule '{subject}' to '{path}'", () => Task.CompletedTask);
            }

            return;
        }
    }
    else
    {
        var properties = (await client.GetSubscriptionAsync(definition.Topic, definition.Name)).Value;

        if (properties.MaxDeliveryCount != Topology.MaxDeliveryCount || !properties.DeadLetteringOnMessageExpiration)
        {
            properties.MaxDeliveryCount = Topology.MaxDeliveryCount;
            properties.DeadLetteringOnMessageExpiration = true;
            await ApplyAsync($"update settings of '{path}'", () => client.UpdateSubscriptionAsync(properties));
        }
    }

    var existing = new List<RuleProperties>();
    await foreach (var rule in client.GetRulesAsync(definition.Topic, definition.Name))
    {
        existing.Add(rule);
    }

    foreach (var rule in existing.Where(r => !IsExpectedRule(r, definition)))
    {
        await ApplyAsync(
            $"delete rule '{rule.Name}' from '{path}'",
            () => client.DeleteRuleAsync(definition.Topic, definition.Name, rule.Name));
    }

    foreach (var subject in definition.Subjects.Where(s => !existing.Any(r => r.Name == s && IsExpectedRule(r, definition))))
    {
        await ApplyAsync(
            $"add rule '{subject}' to '{path}'",
            () => client.CreateRuleAsync(definition.Topic, definition.Name, CorrelationRule(subject)));
    }
}

async Task ApplyAsync(string description, Func<Task> change)
{
    changes++;
    Console.WriteLine($"{(dryRun ? "[dry run] " : string.Empty)}{description}");

    if (!dryRun)
    {
        await change();
    }
}

// One correlation rule per event name, named after it: rules are OR-ed, so the subscription only receives the
// events its consumer handles.
static CreateRuleOptions CorrelationRule(string subject) =>
    new(subject, new CorrelationRuleFilter { Subject = subject });

static bool IsExpectedRule(RuleProperties rule, SubscriptionDefinition definition) =>
    definition.Subjects.Contains(rule.Name)
    && rule.Filter is CorrelationRuleFilter filter
    && filter.Subject == rule.Name;
