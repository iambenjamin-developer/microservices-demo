namespace Inventory.Api.Domain;

/// <summary>
/// The result of an all-or-nothing reservation. A rejection is a normal business outcome (it becomes a
/// <c>StockRejected</c> event), not an error, so it is not modelled as a <c>Result</c> failure.
/// </summary>
public sealed record ReservationOutcome(bool IsReserved, string? Reason, IReadOnlyList<string> UnavailableSkus)
{
    public static ReservationOutcome Reserved() => new(true, null, []);

    public static ReservationOutcome Rejected(string reason, IReadOnlyList<string> unavailableSkus) =>
        new(false, reason, unavailableSkus);
}
