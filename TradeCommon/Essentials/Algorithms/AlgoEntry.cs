using Common;
using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Algorithms;

/// <summary>
/// Class which records prop algo execution.
/// </summary>
[Storage("algorithm_entries", "algorithm")]
[Unique(nameof(SessionId), nameof(PositionId), nameof(Time))]
[Index(nameof(SessionId))]
public record AlgoEntry : SecurityRelatedEntry, ILongShortEntry
{
    /// <summary>
    /// Sequence id which starts from zero in every algo batch session.
    /// </summary>
    public int SequenceId { get; set; }

    public long SessionId { get; set; }

    /// <summary>
    /// Position Id; default is 0, meaning that this entry is not related to a position.
    /// </summary>
    [NotNull]
    public long PositionId { get; set; } = 0;

    [NotNull]
    public DateTime Time { get; set; }

    /// <summary>
    /// Current price. Usually the close of OHLC price object.
    /// </summary>
    [NotNull]
    public decimal Price { get; set; }

    /// <summary>
    /// 1 -> open signal; -1 -> close signal; 0 -> undetermined or just hold position.
    /// </summary>
    public SignalType LongSignal { get; set; }

    /// <summary>
    /// 1 -> open signal; -1 -> close signal; 0 -> undetermined or just hold position.
    /// </summary>
    public SignalType ShortSignal { get; set; }

    public CloseType LongCloseType { get; set; }

    public CloseType ShortCloseType { get; set; }

    public Side OpenSide => LongQuantity > 0 ? Side.Buy : ShortQuantity > 0 ? Side.Sell : Side.None;

    public Side CloseSide => OpenSide.Invert();

    ///// <summary>
    ///// The open or close position order Id. If no order it equals to zero.
    ///// </summary>
    //public long OrderId { get; set; }

    /// <summary>
    /// Target quantity being traded.
    /// This is not the actual traded quantity in any given timestamp.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// An return value between current vs last entry.
    /// It is a theoretical return if enter then exit at the end of the two OHLC's close prices.
    /// </summary>
    public decimal EntryReturn { get; set; }

    /// <summary>
    /// Return from (<see cref="TheoreticExitPrice"/>-<see cref="TheoreticEnterPrice"/>)*<see cref="Quantity"/>.
    /// </summary>
    [DatabaseIgnore]
    public decimal TheoreticPnl => (TheoreticExitPrice ?? 0 - TheoreticEnterPrice ?? 0) * Quantity;

    /// <summary>
    /// The algo target enter price.
    /// Actual price should look at <see cref="Order.Price"/>
    /// which is an weighted average value of the related <see cref="Trade.Price"/>.
    /// </summary>
    public decimal? TheoreticEnterPrice { get; set; }

    /// <summary>
    /// The algo target exit price.
    /// Actual price should look at <see cref="Order.Price"/>
    /// which is an weighted average value of the related <see cref="Trade.Price"/>.
    /// </summary>
    public decimal? TheoreticExitPrice { get; set; }

    //public decimal? StopLossPrice { get; set; }
    //public decimal? TakeProfitPrice { get; set; }

    public DateTime? TheoreticEnterTime { get; set; }
    public DateTime? TheoreticExitTime { get; set; }
    public TimeSpan? TheoreticElapsed => TheoreticEnterTime != null ? TheoreticExitTime == null ? TheoreticExitTime - TheoreticEnterTime : DateTime.UtcNow - TheoreticEnterTime : null;

    /// <summary>
    /// A target notional value of this entry. Short notional - long notional.
    /// </summary>
    public decimal Notional { get; set; }

    /// <summary>
    /// Realized return of this entry which is just closed. 1 - Exit Price / Enter Price.
    /// </summary>
    public decimal RealizedReturn => (TheoreticEnterPrice == null || TheoreticEnterPrice == 0 || TheoreticExitPrice == null || TheoreticExitPrice == 0) ? 0 : (1 - TheoreticExitPrice / TheoreticEnterPrice).Value;

    ///// <summary>
    ///// Realized PNL of this entry which is just closed. (Exit Price - Enter Price) * Quantity held.
    ///// </summary>
    //public decimal RealizedPnl => (TheoreticEnterPrice == null || TheoreticEnterPrice == 0 || TheoreticExitPrice == null || TheoreticExitPrice == 0) ? 0 : ((TheoreticExitPrice - TheoreticEnterPrice) * Quantity).Value;

    /// <summary>
    /// (Only applicable for FX / Crypto) fee incurred related to base currency.
    /// </summary>
    public decimal BaseFee { get; set; }

    /// <summary>
    /// Fee incurred related to security's quote currency.
    /// </summary>
    public decimal QuoteFee { get; set; }

    [DatabaseIgnore]
    public decimal LongPrice { get; set; }
    [DatabaseIgnore]
    public decimal LongNotional { get; set; }
    [DatabaseIgnore]
    public decimal LongQuantity { get; set; }
    [DatabaseIgnore]
    public decimal ShortQuantity { get; set; }
    [DatabaseIgnore]
    public decimal ShortPrice { get; set; }
    [DatabaseIgnore]
    public decimal ShortNotional { get; set; }

    public int OrderCount { get; set; }
    public int TradeCount { get; set; }

    [NotNull, AsJson]
    public IAlgorithmVariables Variables { get; set; }
}

public interface IAlgorithmVariables
{
    string Format(Security security);
}
