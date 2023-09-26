using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// The info of a position with respect to a security.
///
/// Usually for one account + security, only one position exists.
///
/// When quantity reaches zero, another position should be created
/// instead of modifying the closed one.
/// </summary>
[Storage("positions", DatabaseNames.ExecutionData)]
[Unique(nameof(Id))]
[Index(nameof(SecurityId))]
[Index(nameof(CreateTime))]
public sealed record Position : Asset
{
    /// <summary>
    /// The time which the position is fully closed.
    /// It should be the time when last trade fills.
    /// </summary>
    public DateTime CloseTime { get; set; } = DateTime.MaxValue;

    /// <summary>
    /// The price of this position.
    /// It is the weighted average price of all the trades related to this position.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// The notional amount of this position, which is price * quantity.
    /// </summary>
    public decimal Notional { get; set; }

    /// <summary>
    /// The beginning notional amount.
    /// </summary>
    public decimal StartNotional { get; set; }

    /// <summary>
    /// Realized pnl, which is the sum of all realized pnl from each closed trades.
    /// </summary>
    public decimal RealizedPnl { get; set; }

    public long StartOrderId { get; set; }
    public long EndOrderId { get; set; }
    public long StartTradeId { get; set; }
    public long EndTradeId { get; set; }

    public decimal AccumulatedFee { get; set; }

    /// <summary>
    /// Whether it is a closed position.
    /// Usually it means (remaining) quantity equals to zero.
    /// </summary>
    [DatabaseIgnore]
    public bool IsClosed => Quantity == 0;


    public bool Equals(Position? obj)
    {
        if (!base.Equals(obj))
            return false;

        if (CloseTime == obj.CloseTime
            && Price == obj.Price
            && Notional == obj.Notional
            && StartNotional == obj.StartNotional
            && StartOrderId == obj.StartOrderId
            && EndOrderId == obj.EndOrderId
            && StartTradeId == obj.StartTradeId
            && EndTradeId == obj.EndTradeId
            && AccumulatedFee == obj.AccumulatedFee)
            return true;
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(),
                                CloseTime,
                                Price,
                                Notional,
                                StartNotional,
                                StartOrderId,
                                EndOrderId,
                                HashCode.Combine(StartTradeId,
                                EndTradeId,
                                AccumulatedFee));
    }
}
