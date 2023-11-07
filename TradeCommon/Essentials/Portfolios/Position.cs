using Common;
using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using TradeCommon.Calculations;
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
[Storage("positions", DatabaseNames.ExecutionData, SortProperties = false)]
[Unique(nameof(Id))]
[Unique(nameof(StartOrderId), nameof(StartTradeId))]
[Unique(nameof(SecurityId), nameof(AccountId), nameof(StartOrderId))]
[Index(nameof(SecurityId))]
[Index(nameof(CreateTime))]
public sealed record Position : SecurityRelatedEntry, ILongShortEntry, IIdEntry, IComparable<Position>
{
    [DatabaseIgnore]
    private static readonly IdGenerator _positionIdGenerator = IdGenerators.Get<Position>();

    /// <summary>
    /// Unique id of this asset asset.
    /// </summary>
    [NotNull, Positive]
    public long Id { get; set; } = 0;

    /// <summary>
    /// The associated account's Id.
    /// </summary>
    public int AccountId { get; set; } = 0;

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    /// <summary>
    /// The time which the position is fully closed.
    /// It should be the time when last trade fills.
    /// </summary>
    public DateTime CloseTime { get; set; } = DateTime.MaxValue;

    /// <summary>
    /// The price of this position.
    /// It is the weighted average price of all the trades related to this position.
    /// </summary>
    [DatabaseIgnore]
    public decimal Price { get; set; }

    /// <summary>
    /// Amount of quote currency held by this position.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// The notional amount of this position, which is price * quantity.
    /// It is also the gross pnl (unrealized if quantity != 0, otherwise realized).
    /// </summary>
    public decimal Notional { get; set; }

    /// <summary>
    /// The intended entering side.
    /// </summary>
    public Side Side { get; set; }

    /// <summary>
    /// All the long trades' sum of quantity.
    /// </summary>
    public decimal LongQuantity { get; set; }

    /// <summary>
    /// All the short trades' sum of quantity.
    /// </summary>
    public decimal ShortQuantity { get; set; }

    /// <summary>
    /// All the long trades' weighted average price.
    /// </summary>
    public decimal LongPrice { get; set; }

    /// <summary>
    /// All the short trades' weighted average price.
    /// </summary>
    public decimal ShortPrice { get; set; }

    /// <summary>
    /// All the long trades' notional amount.
    /// </summary>
    public decimal LongNotional { get; set; }

    /// <summary>
    /// All the short trades' notional amount.
    /// </summary>
    public decimal ShortNotional { get; set; }

    /// <summary>
    /// First order's id when the position is opened.
    /// </summary>
    public long StartOrderId { get; set; }

    /// <summary>
    /// Last order's id when the position is closed.
    /// </summary>
    public long EndOrderId { get; set; }

    /// <summary>
    /// First trade's id when the position is opened.
    /// </summary>
    public long StartTradeId { get; set; }

    /// <summary>
    /// Last trade's id when the position is closed.
    /// </summary>
    public long EndTradeId { get; set; }

    /// <summary>
    /// Count of trades in this position.
    /// </summary>
    public int TradeCount { get; set; }

    [DatabaseIgnore, JsonIgnore]
    public Side CloseSide => Side == Side.Buy ? Side.Sell : Side == Side.Sell ? Side.Buy : Side.None;

    /// <summary>
    /// Whether it is a closed position.
    /// It means quantity + "working quantity" equals to zero,
    /// or smaller than the minimum allowed residual threshold,
    /// (when <see cref="Security.MinNotional"/> is defined (!= 0)).
    /// </summary>
    [DatabaseIgnore, JsonIgnore]
    public bool IsClosed => (Security == null || Security.MinQuantity == 0) ? Quantity == 0 : Math.Abs(Quantity) <= Security.MinQuantity;

    [DatabaseIgnore, JsonIgnore]
    public bool IsNew => TradeCount == 1;

    [DatabaseIgnore, JsonIgnore]
    public decimal PnL => Notional;

    [DatabaseIgnore]
    public decimal Return => Side switch
    {
        Side.Buy => (ShortPrice == 0 || LongPrice == 0) ? 0 : (ShortPrice - LongPrice).ZeroDivision(LongPrice),
        Side.Sell => (ShortPrice == 0 || LongPrice == 0) ? 0 : (ShortPrice - LongPrice).ZeroDivision(ShortPrice),
        _ => 0,
    };

    /// <summary>
    /// Apply a trade to this position.
    /// If the position is closed after applied, returns true.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public bool Apply(Trade trade, decimal residualQuantity)
    {
        if (trade.IsSecurityInvalid()) throw Exceptions.InvalidSecurityInTrades();

        if (trade.SecurityId != SecurityId || trade.AccountId != AccountId)
            throw Exceptions.InvalidTradePositionCombination("The trade does not belong to the ");

        if (trade.IsOperational) return false;

        trade.ApplyTo(this, residualQuantity);

        UpdateTime = trade.Time;
        TradeCount++;
        if (IsClosed)
        {
            EndOrderId = trade.OrderId;
            EndTradeId = trade.Id;
            CloseTime = trade.Time;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Create a new position by a trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public static Position Create(Trade trade, decimal residualQuantity)
    {
        if (trade.IsSecurityInvalid()) throw Exceptions.InvalidSecurityInTrades();

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

            Side = trade.Side,

            StartOrderId = trade.OrderId,
            StartTradeId = trade.Id,
            EndOrderId = 0,
            EndTradeId = 0,
            Quantity = 0, // should be handled by Apply()
            TradeCount = 0, // should be handled by Apply()
        };
        position.Apply(trade, residualQuantity);
        return position;
    }

    ///// <summary>
    ///// Create a new position, or apply the trade and change the internal
    ///// state of given position.
    ///// </summary>
    ///// <param name="trade"></param>
    ///// <param name="position"></param>
    ///// <returns></returns>
    //public static Position? CreateOrApply(Trade trade, Position? position = null)
    //{
    //    if (position == null)
    //    {
    //        position = Create(trade);
    //    }
    //    else if (!position.IsClosed)
    //    {
    //        position.Apply(trade);
    //    }
    //    else
    //    {
    //        position = Create(trade); // case that given position is actually closed; should create a new one here
    //    }
    //    trade.PositionId = position.Id;
    //    return position;
    //}

    public int CompareTo(Position? other)
    {
        if (other == null) return 1;
        var r = AccountId.CompareTo(other.AccountId);
        if (r == 0) r = CreateTime.CompareTo(other.CreateTime);
        if (r == 0) r = UpdateTime.CompareTo(other.UpdateTime);
        if (r == 0) r = CloseTime.CompareTo(other.CloseTime);

        if (r == 0) r = SecurityId.CompareTo(other.SecurityId);
        if (r == 0) r = SecurityCode.CompareTo(other.SecurityCode);

        if (r == 0) r = Quantity.CompareTo(other.Quantity);
        if (r == 0) r = Notional.CompareTo(other.Notional);

        if (r == 0) r = LongQuantity.CompareTo(other.LongQuantity);
        if (r == 0) r = LongPrice.CompareTo(other.LongPrice);
        if (r == 0) r = LongNotional.CompareTo(other.LongNotional);
        if (r == 0) r = ShortQuantity.CompareTo(other.ShortQuantity);
        if (r == 0) r = ShortPrice.CompareTo(other.ShortPrice);
        if (r == 0) r = ShortNotional.CompareTo(other.ShortNotional);

        if (r == 0) r = StartOrderId.CompareTo(other.StartOrderId);
        if (r == 0) r = EndOrderId.CompareTo(other.EndOrderId);
        if (r == 0) r = StartTradeId.CompareTo(other.StartTradeId);
        if (r == 0) r = EndTradeId.CompareTo(other.EndTradeId);

        if (r == 0) r = TradeCount.CompareTo(other.TradeCount);
        return r;
    }

    public bool EqualsIgnoreId(IIdEntry other)
    {
        return other is Position position && CompareTo(position) == 0;
    }

    public bool Equals(Position? obj)
    {
        return base.Equals(obj) && CompareTo(obj) == 0;
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Id);
        hashCode.Add(AccountId);
        hashCode.Add(CreateTime);
        hashCode.Add(UpdateTime);
        hashCode.Add(CloseTime);
        hashCode.Add(Quantity);
        hashCode.Add(Notional);
        hashCode.Add(Side);
        hashCode.Add(LongQuantity);
        hashCode.Add(ShortQuantity);
        hashCode.Add(LongPrice);
        hashCode.Add(ShortPrice);
        hashCode.Add(LongNotional);
        hashCode.Add(ShortNotional);
        hashCode.Add(StartOrderId);
        hashCode.Add(EndOrderId);
        hashCode.Add(StartTradeId);
        hashCode.Add(EndTradeId);
        hashCode.Add(TradeCount);
        return hashCode.ToHashCode();
    }

    public override string ToString()
    {
        return $"ID:{Id}, Time:{{T0:{CreateTime:yyMMdd-HHmmss}, T1:{(CloseTime == DateTime.MaxValue ? "NotYet" : CloseTime.ToString("yyMMdd-HHmmss"))}}}," +
            $" SEC:{SecurityCode}, TRDCOUNT:{TradeCount}," +
            $" DETAILS:{{SIDE:,{Side}, Q:{Security.FormatQuantity(Quantity)}, N:{Notional}, LN:{LongNotional}, SN:{ShortNotional}}}}}";
    }
}