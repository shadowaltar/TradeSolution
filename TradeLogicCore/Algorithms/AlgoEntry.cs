using Common.Attributes;
using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms;

/// <summary>
/// Class which records prop algo execution.
/// </summary>
public record AlgoEntry([UpsertIgnore] long Id, [UpsertIgnore, InsertIgnore, SelectIgnore] Security Security)
{
    public int PositionId { get; set; }

    public int SecurityId { get; set; } = -1;

    public required DateTime Time { get; set; }

    /// <summary>
    /// Current price. Usually the close of OHLC price object.
    /// </summary>
    public decimal Price { get; set; } = decimal.MinValue;

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
    public decimal Return { get; set; } = 0;

    public decimal? EnterPrice { get; set; }
    public decimal? ExitPrice { get; set; }
    public decimal? SLPrice { get; set; }
    public decimal? TPPrice { get; set; }
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
    /// Unrealized PNL of this entry which is still opened. (Exit Price - Current Price) * Quantity held.
    /// </summary>
    public decimal UnrealizedPnl { get; set; }
    /// <summary>
    /// Fee incurred when enter and/or exit a position.
    /// </summary>
    public decimal Fee { get; set; }

    ///// <summary>
    ///// Gets / sets the portfolio snapshot related to current entry.
    ///// </summary>
    //public Portfolio? Portfolio { get; set; }
}

public record AlgoEntry<T> : AlgoEntry
{
    public AlgoEntry([UpsertIgnore] long Id, [InsertIgnore, SelectIgnore, UpsertIgnore] Security Security) : base(Id, Security)
    {
    }


    public required T Variables { get; set; }
}

public enum SignalType
{
    Close = -1,
    Hold = 0,
    None = 0,
    Open = 1,
}

public enum CloseType
{
    None,
    Normal,
    StopLoss,
    TakeProfit,
}