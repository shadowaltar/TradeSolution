using Common;
using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Calculations;
using TradeCommon.Essentials.Portfolios;
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
[Index(nameof(PositionId))]
[Storage("trades", "execution")]
public record Trade : SecurityRelatedEntry, IComparable<Trade>, IIdEntry
{
    /// <summary>
    /// Unique trade id.
    /// </summary>
    [NotNull]
    public long Id { get; set; } = 0;

    /// <summary>
    /// The order id associated with this 
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
    /// Side of this 
    /// </summary>
    [NotNull]
    public Side Side { get; set; }

    /// <summary>
    /// The execution price of this 
    /// </summary>
    [NotNull]
    public decimal Price { get; set; }

    /// <summary>
    /// The executed quantity in this 
    /// </summary>
    [NotNull]
    public decimal Quantity { get; set; }

    [DatabaseIgnore]
    public decimal Notional => Price * Quantity;

    /// <summary>
    /// The fee incurred in this 
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
    /// The algo session id which generates this order.
    /// </summary>
    public int AlgoSessionId { get; set; }

    /// <summary>
    /// The algo entry id which triggers this order.
    /// </summary>
    public int AlgoEntryId { get; set; }

    /// <summary>
    /// Calculate weighted average price, quantity and notional amount to 
    /// </summary>
    /// <param name="entry"></param>
    public void ApplyTo(ILongShortEntry entry, decimal residualQuantity)
    {
        // we always assume quantity >= 0, and its sign is determined by Side
        Assertion.Shall(Quantity >= 0);

        var sign = Side == Side.Sell ? -1 : Side == Side.Buy ? 1 : 0;
        // residual quantity is only applied if it is the same sign of the signed-quantity.
        var signedQuantity = sign * Quantity;
        var quantity = Quantity;
        if (sign == Math.Sign(residualQuantity))
        {
            signedQuantity += residualQuantity;
            quantity = Math.Abs(signedQuantity);
        }
        if (Side == Side.Buy)
        {
            entry.LongNotional += Price * quantity;
            entry.LongQuantity += quantity; // always +ve
            entry.LongPrice = entry.LongNotional.ZeroDivision(entry.LongQuantity);
        }
        else if (Side == Side.Sell)
        {
            entry.ShortNotional += Price * quantity;
            entry.ShortQuantity += quantity; // always +ve
            entry.ShortPrice = entry.ShortNotional.ZeroDivision(entry.ShortQuantity);
        }
        entry.Notional = entry.ShortNotional - entry.LongNotional;
        entry.Quantity += signedQuantity; // this is signed
    }

    public int CompareTo(Trade? trade)
    {
        var r = SecurityId.CompareTo(trade?.SecurityId);
        if (r == 0) r = OrderId.CompareTo(trade?.OrderId);
        if (r == 0) r = ExternalTradeId.CompareTo(trade?.ExternalTradeId);
        if (r == 0) r = ExternalOrderId.CompareTo(trade?.ExternalOrderId);
        if (r == 0) r = PositionId.CompareTo(trade?.PositionId);
        if (r == 0) r = Time.CompareTo(trade?.Time);
        if (r == 0) r = Side.CompareTo(trade?.Side);
        if (r == 0) r = Price.CompareTo(trade?.Price);
        if (r == 0) r = Quantity.CompareTo(trade?.Quantity);
        if (r == 0) r = Fee.CompareTo(trade?.Fee);
        if (r == 0) r = FeeAssetCode.SafeCompareTo(trade?.FeeAssetCode);
        if (r == 0) r = AccountId.CompareTo(trade?.AccountId);
        return r;
    }

    public bool EqualsIgnoreId(IIdEntry other)
    {
        return other is Trade trade && CompareTo(trade) == 0;
    }

    public override string ToString()
    {
        return $"ID:{Id}, ETID:{ExternalTradeId}, T:{Time:yyMMdd-HHmmss}, SEC:{SecurityCode}, PID:{PositionId}, DETAILS:{{ SIDE:,{Side}, P:{Security.FormatPrice(Price)}, Q:{Security.FormatQuantity(Quantity)}}}, OID:{OrderId}, EOID:{ExternalOrderId}";
    }
}
