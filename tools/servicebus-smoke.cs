#:package Azure.Messaging.ServiceBus@7.20.2
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false

// Manual smoke test for the local Azure Service Bus emulator started by the Aspire AppHost.
//
//   dotnet run tools/servicebus-smoke.cs -- send    <topic> <subject>
//   dotnet run tools/servicebus-smoke.cs -- peek    <topic> <subscription>
//   dotnet run tools/servicebus-smoke.cs -- receive <topic> <subscription>
//   dotnet run tools/servicebus-smoke.cs -- dlq     <topic> <subscription>
//
// The connection string defaults to the emulator; override it with SERVICEBUS_CONNECTION.

using System.Text.Json;
using Azure.Messaging.ServiceBus;

const string EmulatorConnection =
    "Endpoint=sb://localhost:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

if (args.Length < 3)
{
    Console.WriteLine("Usage: send <topic> <subject> | peek|receive|dlq <topic> <subscription>");
    return 1;
}

var (command, topic, target) = (args[0], args[1], args[2]);
var connectionString = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION") ?? EmulatorConnection;

await using var client = new ServiceBusClient(connectionString);

switch (command)
{
    case "send":
        await SendAsync(client, topic, subject: target);
        break;
    case "peek":
        await ReadAsync(client, topic, target, deadLetter: false, consume: false);
        break;
    case "receive":
        await ReadAsync(client, topic, target, deadLetter: false, consume: true);
        break;
    case "dlq":
        await ReadAsync(client, topic, target, deadLetter: true, consume: false);
        break;
    default:
        Console.WriteLine($"Unknown command '{command}'.");
        return 1;
}

return 0;

static async Task SendAsync(ServiceBusClient client, string topic, string subject)
{
    await using var sender = client.CreateSender(topic);

    var payload = new { orderId = Guid.NewGuid(), note = "smoke test", sentAt = DateTimeOffset.UtcNow };
    var message = new ServiceBusMessage(JsonSerializer.Serialize(payload))
    {
        MessageId = Guid.CreateVersion7().ToString(),
        Subject = subject,
        ContentType = "application/json",
    };

    await sender.SendMessageAsync(message);
    Console.WriteLine($"Sent {subject} to '{topic}' (MessageId {message.MessageId})");
}

static async Task ReadAsync(ServiceBusClient client, string topic, string subscription, bool deadLetter, bool consume)
{
    await using var receiver = client.CreateReceiver(topic, subscription, new ServiceBusReceiverOptions
    {
        SubQueue = deadLetter ? SubQueue.DeadLetter : SubQueue.None,
    });

    var messages = consume
        ? await receiver.ReceiveMessagesAsync(maxMessages: 20, maxWaitTime: TimeSpan.FromSeconds(3))
        : await receiver.PeekMessagesAsync(maxMessages: 20);

    var source = deadLetter ? $"{topic}/{subscription} (dead-letter)" : $"{topic}/{subscription}";
    Console.WriteLine($"{messages.Count} message(s) in {source}");

    foreach (var message in messages)
    {
        Console.WriteLine($"  [{message.Subject}] {message.MessageId} {message.Body}");

        if (consume)
        {
            await receiver.CompleteMessageAsync(message);
        }
    }
}
