namespace BuildingBlocks.Messaging.Consumers;

public sealed class ConsumerOptions
{
    public const string SectionName = "Messaging:Consumers";

    /// <summary>
    /// When <c>false</c> the subscription processors do not connect to the broker. Integration tests use it
    /// to host a service without consuming (or stealing) messages from a real subscription.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
