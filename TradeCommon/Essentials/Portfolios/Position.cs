using Common;
using Common.Attributes;
using TradeCommon.Database;
using TradeCommon.Essentials.Trading;
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
public sealed record Position : Asset, IComparable<Position>
{
    [DatabaseIgnore]
    private static readonly IdGenerator _positionIdGenerator;

    static Position()
    {
        _positionIdGenerator = IdGenerators.Get<Position>();
    }

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

    public int CompareTo(Position? other)
    {
        if (other == null) return 1;
        var r = base.CompareTo(other);
        if (r != 0) r = CloseTime.CompareTo(other.CloseTime);
        if (r != 0) r = Price.CompareTo(other.Price);
        if (r != 0) r = Notional.CompareTo(other.Notional);
        if (r != 0) r = StartNotional.CompareTo(other.StartNotional);
        if (r != 0) r = RealizedPnl.CompareTo(other.RealizedPnl);
        if (r != 0) r = StartOrderId.CompareTo(other.StartOrderId);
        if (r != 0) r = EndOrderId.CompareTo(other.EndOrderId);
        if (r != 0) r = StartTradeId.CompareTo(other.StartTradeId);
        if (r != 0) r = EndTradeId.CompareTo(other.EndTradeId);
        if (r != 0) r = AccumulatedFee.CompareTo(other.AccumulatedFee);
        return r;
    }

    public override bool EqualsIgnoreId(ITimeBasedUniqueIdEntry other)
    {
        if (other is not Position position) return false;
        return CompareTo(position) == 0;
    }

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

    public override string ToString()
    {
        return $"[{Id}][{CreateTime:yyMMdd-HHmmss}][{(CloseTime==DateTime.MaxValue? "": CloseTime.ToString("yyMMdd-HHmmss"))}]" +
            $" secId:{SecurityId}, p:{Price}, q:{Quantity}";
    }

    /// <summary>
    /// Create a new position by a trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public static Position Create(Trade trade)
    {
        var position = new Position
        {
            Id = _positionIdGenerator.NewTimeBasedId,
            AccountId = trade.AccountId,

            Security = trade.Security,
            SecurityId = trade.SecurityId,
            SecurityCode = trade.SecurityCode,

            CreateTime = trade.Time,
            UpdateTime = trade.Time,
            CloseTime = DateTime.MaxValue,

            Quantity = trade.Quantity,
            Price = trade.Price,
            LockedQuantity = 0,
            Notional = trade.Quantity * trade.Price,
            StartNotional = trade.Quantity * trade.Price,
            RealizedPnl = 0,

            StartOrderId = trade.OrderId,
            StartTradeId = trade.Id,
            EndOrderId = 0,
            EndTradeId = 0,
        };

        return position;
    }
}
