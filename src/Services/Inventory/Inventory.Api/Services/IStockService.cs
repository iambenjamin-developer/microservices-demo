using BuildingBlocks.Common.Results;
using Inventory.Api.Contracts;
using Inventory.Api.Domain;

namespace Inventory.Api.Services;

/// <summary>
/// Every stock operation of the service, behind one interface: the HTTP API (<c>StockController</c>) and the
/// <c>OrderPlaced</c> consumer both change stock, and both go through here.
/// </summary>
/// <remarks>
/// The operations do not share one saving policy, and the names say which is which: an HTTP request is its own
/// unit of work, so <see cref="UpdateQuantityAvailableAsync"/> commits; a consumed message is committed by the
/// consumer pipeline together with its outbox and inbox rows, so <see cref="StageReservationAsync"/> only changes
/// tracked entities and leaves the commit to the caller.
/// </remarks>
public interface IStockService
{
    /// <summary>
    /// Lists the stock sorted by SKU, all of it or only <paramref name="skus"/> (normalized; unknown ones are
    /// skipped). Read-only: projected in SQL, nothing is tracked.
    /// </summary>
    Task<IReadOnlyList<StockResponse>> GetStockAsync(IReadOnlyCollection<string>? skus, CancellationToken cancellationToken);

    /// <summary>
    /// Replenishment: sets how many packs of <paramref name="sku"/> are available (the request body, already
    /// validated) and <b>saves the change</b>.
    /// Reserved packs are untouched. Fails with <see cref="StockErrors.NotFound"/> for a SKU that has no stock row.
    /// </summary>
    Task<Result<StockResponse>> UpdateQuantityAvailableAsync(string sku, UpdateStockRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Reserves stock for an order, all or nothing (<see cref="StockReservation.Reserve"/>), and <b>does not save</b>:
    /// the reserved quantities stay tracked in the scope's <c>DbContext</c> so the caller commits them in the same
    /// transaction as the outcome event. A rejection is a normal outcome, not a failure.
    /// </summary>
    Task<ReservationOutcome> StageReservationAsync(IReadOnlyCollection<ReservationLine> lines, CancellationToken cancellationToken);
}
