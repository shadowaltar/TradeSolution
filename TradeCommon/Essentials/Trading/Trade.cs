using Common;
using TradeCommon.Constants;
using Common.Attributes;
using System.Diagnostics.CodeAnalysis;

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
public class Trade : IComparable<Trade>
{
    public const long DefaultId = 0;

    /// <summary>
    /// Unique trade id.
    /// </summary>
    [NotNull]
    public long Id { get; set; }

    /// <summary>
    /// Security id.
    /// </summary>
    [NotNull]
    public int SecurityId { get; set; }

    /// <summary>
    /// Security code (will not be saved to database).
    /// </summary>
    [DatabaseIgnore]
    public string? SecurityCode { get; set; }

    /// <summary>
    /// The order id associated with this trade.
    /// </summary>
    [NotNull]
    public long OrderId { get; set; }

    /// <summary>
    /// The trade id associated with this trade provided by the broker.
    /// </summary>
    [NotNull]
    public long ExternalTradeId { get; set; } = DefaultId;

    /// <summary>
    /// The order id associated with this trade provided by the broker.
    /// </summary>
    [NotNull]
    public long ExternalOrderId { get; set; } = DefaultId;

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
    public int FeeAssetId { get; set; }

    /// <summary>
    /// The asset/currency of the fee.
    /// </summary>
    [DatabaseIgnore]
    public string? FeeAssetCode { get; set; }

    /// <summary>
    /// The broker's ID.
    /// </summary>
    [NotNull]
    public int BrokerId { get; set; } = ExternalNames.BrokerTypeToIds[BrokerType.Unknown];

    /// <summary>
    /// The exchange's ID.
    /// </summary>
    [NotNull]
    public int ExchangeId { get; set; } = ExternalNames.BrokerTypeToIds[BrokerType.Unknown];

    /// <summary>
    /// The trade object is coarse such that we don't have
    /// info to determine who owns it or which order is this trade being related to.
    /// Usually it is a trade observed in the market which is
    /// not related to current user.
    /// </summary>
    [DatabaseIgnore]
    public bool IsTemp { get; set; } = false;

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
        if (r != 0) r = BrokerId.CompareTo(trade?.BrokerId);
        if (r != 0) r = ExchangeId.CompareTo(trade?.ExchangeId);
        if (r != 0) r = BestMatch.CompareTo(trade?.BestMatch);
        return r;
    }

    public override string ToString()
    {
        return $"[{Id}][{ExternalTradeId}][{Time:yyMMdd-HHmmss}] secId:{SecurityId}, p:{Price}, q:{Quantity}, side:{Side}";
    }
}
