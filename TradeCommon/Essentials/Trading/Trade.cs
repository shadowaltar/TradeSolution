using Common;
using Common.Attributes;
using Microsoft.CodeAnalysis;
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
public record Trade : SecurityRelatedEntry, IComparable<Trade>, ITimeBasedUniqueIdEntry
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
    /// If it is best match, returns 1, if unknown, returns 0, otherwise returns -1;
    /// </summary>
    [DatabaseIgnore]
    public int BestMatch { get; set; } = 0;

    /// <summary>
    /// Calculate weighted average price, quantity and notional amount to 
    /// </summary>
    /// <param name="entry"></param>
    public void ApplyTo(ILongShortEntry entry)
    {
        var sign = Side == Side.Sell ? -1 : Side == Side.Buy ? 1 : 0;
        if (Side == Side.Buy)
        {
            entry.LongNotional += Price * Quantity;
            entry.LongQuantity += Quantity;
            entry.LongPrice = entry.LongNotional.ZeroDivision(entry.LongQuantity);
        }
        else if (Side == Side.Sell)
        {
            entry.ShortNotional += Price * Quantity;
            entry.ShortQuantity += Quantity;
            entry.ShortPrice = entry.ShortNotional.ZeroDivision(entry.ShortQuantity);
        }
        entry.Notional = entry.ShortNotional - entry.LongNotional;
        entry.Quantity += sign * Quantity;
        entry.Price = entry.Notional.ZeroDivision(entry.Quantity);
    }

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
        return $"[Id:{Id}][ETId:{ExternalTradeId}][{Time:yyMMdd-HHmmss}][SecId:{SecurityId}][PId:{PositionId}], {Side} p*q:{Price}*{Quantity}, [OId:{OrderId}][EOId:{ExternalOrderId}]";
    }
}
