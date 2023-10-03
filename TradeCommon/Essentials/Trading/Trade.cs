using Common;
using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Trading;

/// <summary>
/// The activity which represents maker and taker forms a deal by matching a price and quantity.
/// It can also be called as a Deal.
/// One order object may result in zero or more trades immediately or in a period of time.
/// </summary>
[Unique(nameof(Id))]
[Unique(nameof(ExternalTradeId))]
[Index(nameof(SecurityId))]
[Storage("trades", "execution")]
public record Trade : SecurityRelatedEntry, IComparable<Trade>, ITimeBasedUniqueIdEntry
{
    /// <summary>
    /// Unique trade id.
    /// </summary>
    [NotNull]
    public long Id { get; set; } = 0;

    /// <summary>
    /// The order id associated with this trade.
    /// </summary>
    [NotNull]
    public long OrderId { get; set; } = 0;

    /// <summary>
    /// The trade id associated with this trade provided by the broker.
    /// </summary>
    [NotNull]
    public long ExternalTradeId { get; set; } = 0;

    /// <summary>
    /// The order id associated with this trade provided by the broker.
    /// </summary>
    [NotNull]
    public long ExternalOrderId { get; set; } = 0;

    /// <summary>
    /// The related position's Id.
    /// </summary>
    [NotNull]
    public long PositionId { get; set; } = 0;

    /// <summary>
    /// Trade execution time.
    /// </summary>
    [NotNull]
    public DateTime Time { get; set; }

    /// <summary>
    /// Side of this trade.
    /// </summary>
    [NotNull]
    public Side Side { get; set; }

    /// <summary>
    /// The execution price of this trade.
    /// </summary>
    [NotNull]
    public decimal Price { get; set; }

    /// <summary>
    /// The executed quantity in this trade.
    /// </summary>
    [NotNull]
    public decimal Quantity { get; set; }

    /// <summary>
    /// The fee incurred in this trade.
    /// </summary>
    [NotNull]
    public decimal Fee { get; set; }

    /// <summary>
    /// The asset Id of the fee.
    /// </summary>
    [NotNull]
    public int FeeAssetId { get; set; } = 0;

    /// <summary>
    /// The asset/currency of the fee.
    /// </summary>
    [DatabaseIgnore]
    public string? FeeAssetCode { get; set; }

    /// <summary>
    /// The account ID.
    /// </summary>
    [NotNull]
    public int AccountId { get; set; } = 0;

    /// <summary>
    /// If it is best match, returns 1, if unknown, returns 0, otherwise returns -1;
    /// </summary>
    [DatabaseIgnore]
    public int BestMatch { get; set; } = 0;

    public int CompareTo(Trade? trade)
    {
        var r = SecurityId.CompareTo(trade?.SecurityId);
        if (r != 0) r = OrderId.CompareTo(trade?.OrderId);
        if (r != 0) r = ExternalTradeId.CompareTo(trade?.ExternalTradeId);
        if (r != 0) r = ExternalOrderId.CompareTo(trade?.ExternalOrderId);
        if (r != 0) r = Time.CompareTo(trade?.Time);
        if (r != 0) r = Side.CompareTo(trade?.Side);
        if (r != 0) r = Price.CompareTo(trade?.Price);
        if (r != 0) r = Quantity.CompareTo(trade?.Quantity);
        if (r != 0) r = Fee.CompareTo(trade?.Fee);
        if (r != 0) r = FeeAssetCode.SafeCompareTo(trade?.FeeAssetCode);
        if (r != 0) r = AccountId.CompareTo(trade?.AccountId);
        if (r != 0) r = BestMatch.CompareTo(trade?.BestMatch);
        return r;
    }

    public bool EqualsIgnoreId(ITimeBasedUniqueIdEntry other)
    {
        if (other is not Trade trade) return false;
        return CompareTo(trade) == 0;
    }

    public override string ToString()
    {
        return $"[{Id}][{ExternalTradeId}][{Time:yyMMdd-HHmmss}] secId:{SecurityId}, p:{Price}, q:{Quantity}, side:{Side}, oid:{OrderId}, eoid:{ExternalOrderId}";
    }
}
