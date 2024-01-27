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
[Unique(nameof(SessionId), nameof(SequenceId), nameof(Time))]
[Index(nameof(SessionId))]
public record AlgoEntry : SecurityRelatedEntry, ILongShortEntry
{
    /// <summary>
    /// Index / serial number / sequence id of an algo entry in a batch.
    /// </summary>
    public int SequenceId { get; set; }

    public long SessionId { get; set; }

    /// <summary>
    /// The id which indicates a position's existence over multiple algo-entries
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

    ///// <summary>
    ///// The open or close position order Id. If no order it equals to zero.
    ///// </summary>
    //public long OrderId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>
    /// Return vs previous OHLC price using two close prices.
    /// </summary>
    public decimal Return { get; set; }
    public decimal? EnterPrice { get; set; }
    public decimal? ExitPrice { get; set; }
    //public decimal? StopLossPrice { get; set; }
    //public decimal? TakeProfitPrice { get; set; }
    public DateTime? EnterTime { get; set; }
    public TimeSpan? Elapsed { get; set; }

    /// <summary>
    /// Notional value of this entry. Current Price * Quantity being hold.
    /// </summary>
    public decimal Notional { get; set; }

    /// <summary>
    /// Realized return of this entry which is just closed. 1 - Exit Price / Enter Price.
    /// </summary>
    public decimal RealizedReturn { get; set; }

    /// <summary>
    /// Realized PNL of this entry which is just closed. (Exit Price - Enter Price) * Quantity held.
    /// </summary>
    public decimal RealizedPnl { get; set; }

    /// <summary>
    /// Fee incurred when enter and/or exit a position.
    /// </summary>
    public decimal Fee { get; set; }

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

    [NotNull, AsJson]
    public IAlgorithmVariables Variables { get; set; }
}

public interface IAlgorithmVariables
{
    string Format(Security security);
}
