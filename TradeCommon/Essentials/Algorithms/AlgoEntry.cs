using Common.Attributes;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Essentials.Algorithms;

/// <summary>
/// Class which records prop algo execution.
/// </summary>
[Storage("algorithm_entries", "algorithm")]
[Unique(nameof(BatchId), nameof(AlgoId), nameof(VersionId), nameof(Time))]
[Index(nameof(BatchId))]
public record AlgoEntry
{
    /// <summary>
    /// The id of batch of algorithm execution.
    /// </summary>
    [NotNull]
    public int BatchId { get; set; }

    /// <summary>
    /// The algorithm's Id.
    /// </summary>
    [NotNull]
    public int AlgoId { get; set; }

    /// <summary>
    /// The version id of this algorithm.
    /// </summary>
    [NotNull]
    public int VersionId { get; set; }

    /// <summary>
    /// The id which indicates a position's existence over multiple algo-entries
    /// </summary>
    [NotNull, DefaultValue(0)]
    public long PositionId { get; set; }

    [NotNull]
    public int SecurityId { get; set; } = -1;

    [DatabaseIgnore]
    public Security Security { get; set; }

    [NotNull]
    public DateTime Time { get; set; }

    /// <summary>
    /// Current price. Usually the close of OHLC price object.
    /// </summary>
    [NotNull]
    public decimal Price { get; set; }

    /// <summary>
    /// Current OHLC price object's high price.
    /// </summary>
    public decimal? HighPrice { get; set; }

    /// <summary>
    /// Current OHLC price object's low price.
    /// </summary>
    public decimal? LowPrice { get; set; }

    /// <summary>
    /// 1 -> open signal; -1 -> close signal; 0 -> undetermined or just hold position.
    /// </summary>
    public SignalType LongSignal { get; set; }

    /// <summary>
    /// 1 -> open signal; -1 -> close signal; 0 -> undetermined or just hold position.
    /// </summary>
    public SignalType ShortSignal { get; set; }

    public bool IsLong { get; set; }
    public CloseType LongCloseType { get; set; }
    public bool IsShort { get; set; }
    public CloseType ShortCloseType { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>
    /// Return vs previous OHLC price using two close prices.
    /// </summary>
    public decimal Return { get; set; }
    public decimal? EnterPrice { get; set; }
    public decimal? ExitPrice { get; set; }
    public decimal? StopLossPrice { get; set; }
    public decimal? TakeProfitPrice { get; set; }
    public DateTime? EnterTime { get; set; }
    public TimeSpan? Elapsed { get; set; }

    /// <summary>
    /// Notional value of this entry. Current Price * Quantity being hold.
    /// </summary>
    public decimal? Notional { get; set; }

    /// <summary>
    /// Realized return of this entry which is just closed. 1 - Exit Price / Enter Price.
    /// </summary>
    public decimal? RealizedReturn { get; set; }

    /// <summary>
    /// Realized PNL of this entry which is just closed. (Exit Price - Enter Price) * Quantity held.
    /// </summary>
    public decimal? RealizedPnl { get; set; }


    /// <summary>
    /// Unrealized PNL of this entry which is still opened. (Current Price - Enter Price) * Quantity held.
    /// </summary>
    public decimal UnrealizedPnl { get; set; }

    /// <summary>
    /// Fee incurred when enter and/or exit a position.
    /// </summary>
    public decimal Fee { get; set; }

    [Positive]
    public decimal FeeAssetId { get; set; }

    public decimal? ActualQuantity { get; set; }
    public decimal? ActualEnterPrice { get; set; }
    public decimal? ActualExitPrice { get; set; }
    public decimal? ActualRealizedReturn { get; set; }
    public decimal? ActualRealizedPnl { get; set; }
}

public record AlgoEntry<T> : AlgoEntry
{
    [NotNull, AsJson]
    public T Variables { get; set; }
}
