﻿using Common;
using Common.Attributes;
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
[Storage("positions", DatabaseNames.ExecutionData)]
[Unique(nameof(Id))]
[Unique(nameof(StartOrderId), nameof(StartTradeId))]
[Index(nameof(SecurityId))]
[Index(nameof(CreateTime))]
public sealed record Position : Asset, ILongShortEntry, IComparable<Position>
{
    [DatabaseIgnore]
    private static readonly IdGenerator _positionIdGenerator = IdGenerators.Get<Position>();

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
    /// It is also the gross pnl (unrealized if quantity != 0, otherwise realized).
    /// </summary>
    public decimal Notional { get; set; }

    /// <summary>
    /// The intended entering side.
    /// </summary>
    public Side Side { get; set; }

    [DatabaseIgnore, JsonIgnore]
    public Side CloseSide => Side == Side.Buy ? Side.Sell : Side == Side.Sell ? Side.Buy : Side.None;

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

    public decimal AccumulatedFee { get; set; }

    /// <summary>
    /// Whether it is a closed position.
    /// Usually it means (remaining) quantity equals to zero.
    /// If <see cref="TradeCommon.Essentials.Instruments.Security.MinNotional"/> is defined (!= 0),
    /// then when quantity <= minNotional it will also be considered as closed.
    /// </summary>
    [DatabaseIgnore]
    public bool IsClosed => (Security == null || Security.MinQuantity == 0) ? Quantity == 0 : Quantity <= Security.MinQuantity;

    [DatabaseIgnore]
    public decimal Return
    {
        get
        {
            if (Side == Side.Buy)
            {
                return Maths.ZeroDivision((ShortPrice - LongPrice), LongPrice);
            }
            else if (Side == Side.Sell)
            {
                return Maths.ZeroDivision((LongPrice - ShortPrice), ShortPrice);
            }
            return 0;
        }
    }

    /// <summary>
    /// Apply a trade to this position.
    /// If the position is closed after applied, returns true.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public bool Apply(Trade trade)
    {
        if (trade.SecurityId != SecurityId || trade.AccountId != AccountId)
            throw Exceptions.InvalidTradePositionCombination("The trade does not belong to the ");

        trade.ApplyTo(this);

        UpdateTime = trade.Time;
        TradeCount++;
        AccumulatedFee += trade.Fee;

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

            Side = trade.Side,

            LockedQuantity = 0,

            StartOrderId = trade.OrderId,
            StartTradeId = trade.Id,
            EndOrderId = 0,
            EndTradeId = 0,
            Price = 0, // should be handled by Apply()
            Quantity = 0, // should be handled by Apply()
            TradeCount = 0, // should be handled by Apply()
            AccumulatedFee = trade.Fee,
        };
        position.Apply(trade);
        return position;
    }

    public static IEnumerable<Position> CreateOrApply(List<Trade> trades, Position? position = null)
    {
        if (trades.IsNullOrEmpty())
        {
            if (position != null)
            {
                yield return position;
            }
            yield break;
        }

        foreach (var trade in trades)
        {
            if (position == null)
            {
                position = Create(trade);
                trade.PositionId = position.Id;
            }
            else
            {
                var isClosed = position.Apply(trade);
                trade.PositionId = position.Id;
                if (isClosed)
                {
                    yield return position;
                    position = null;
                }
            }
        }
        if (position != null)
            yield return position;
    }

    /// <summary>
    /// Create a new position, or apply the trade and change the internal
    /// state of given position.
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    public static Position? CreateOrApply(Trade trade, Position? position = null)
    {
        if (position == null)
        {
            position = Create(trade);
        }
        else if (!position.IsClosed)
        {
            position.Apply(trade);
        }
        else
        {
            position = Create(trade); // case that given position is actually closed; should create a new one here
        }
        trade.PositionId = position.Id;
        return position;
    }

    public int CompareTo(Position? other)
    {
        if (other == null) return 1;
        var r = base.CompareTo(other);
        if (r == 0) r = CloseTime.CompareTo(other.CloseTime);

        if (r == 0) r = Price.CompareTo(other.Price);
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

        if (r == 0) r = AccumulatedFee.CompareTo(other.AccumulatedFee);
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

        return CompareTo(obj) == 0;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(),
                                CloseTime,
                                HashCode.Combine(
                                Price,
                                Notional,
                                LongPrice,
                                LongQuantity,
                                LongNotional,
                                ShortPrice,
                                ShortQuantity,
                                ShortNotional),
                                HashCode.Combine(
                                StartOrderId,
                                EndOrderId,
                                StartTradeId,
                                EndTradeId,
                                TradeCount,
                                AccumulatedFee));
    }

    public override string ToString()
    {
        return $"[{Id}][{CreateTime:yyMMdd-HHmmss}->{(CloseTime == DateTime.MaxValue ? "NotYet" : CloseTime.ToString("yyMMdd-HHmmss"))}]" +
            $" secId:{SecurityId}, p:{Price}, q:{Quantity}";
    }
}