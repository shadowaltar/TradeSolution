using TradeCommon.Constants;
using TradeCommon.Utils.Attributes;

namespace TradeCommon.Essentials.Trading;

/// <summary>
/// The activity which represents maker and taker forms a deal by matching a price and quantity.
/// It can also be called as a Deal.
/// One order object may result in zero or more trades immediately or in a period of time.
/// </summary>
public class Trade : IComparable<Trade>
{
    public const long DefaultId = 0;

    /// <summary>
    /// Unique trade id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Security id.
    /// </summary>
    public int SecurityId { get; set; }

    /// <summary>
    /// The order id associated with this trade.
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// The trade id associated with this trade provided by the broker.
    /// </summary>
    public long ExternalTradeId { get; set; } = DefaultId;

    /// <summary>
    /// The order id associated with this trade provided by the broker.
    /// </summary>
    public long ExternalOrderId { get; set; } = DefaultId;

    /// <summary>
    /// Trade execution time.
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// Side of this trade.
    /// </summary>
    public Side Side { get; set; }

    /// <summary>
    /// The execution price of this trade.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// The executed quantity in this trade.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// The fee incurred in this trade.
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    /// The currency of the fee.
    /// </summary>
    public string FeeCurrency { get; set; } = "";

    /// <summary>
    /// The broker's ID.
    /// </summary>
    public int BrokerId { get; set; } = ExternalNames.BrokerTypeToIds[BrokerType.Unknown];

    /// <summary>
    /// The exchange's ID.
    /// </summary>
    public int ExchangeId { get; set; } = ExternalNames.BrokerTypeToIds[BrokerType.Unknown];

    /// <summary>
    /// The trade object is coarse such that we don't have
    /// info to determine who owns it or which order is this trade being related to.
    /// Usually it is a trade observed in the market which is
    /// not related to current user.
    /// </summary>
    [UpsertIgnore]
    public bool IsCoarse { get; set; } = false;

    public int CompareTo(Trade? other)
    {
        var r = SecurityId.CompareTo(other?.SecurityId);
        if (r != 0) r = OrderId.CompareTo(other?.OrderId);
        if (r != 0) r = ExternalTradeId.CompareTo(other?.ExternalTradeId);
        if (r != 0) r = ExternalOrderId.CompareTo(other?.ExternalOrderId);
        if (r != 0) r = Time.CompareTo(other?.Time);
        if (r != 0) r = Side.CompareTo(other?.Side);
        if (r != 0) r = Price.CompareTo(other?.Price);
        if (r != 0) r = Quantity.CompareTo(other?.Quantity);
        if (r != 0) r = Fee.CompareTo(other?.Fee);
        if (r != 0) r = FeeCurrency.CompareTo(other?.FeeCurrency);
        if (r != 0) r = BrokerId.CompareTo(other?.BrokerId);
        if (r != 0) r = ExchangeId.CompareTo(other?.ExchangeId);
        return r;
    }

    public override string ToString()
    {
        return $"[{Id}][{ExternalTradeId}][{Time:yyMMdd-HHmmss}] secId:{SecurityId}, p:{Price}, q:{Quantity}, side:{Side}";
    }
}
