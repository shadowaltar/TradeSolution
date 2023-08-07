using TradeCommon.Essentials.Portfolios;

namespace TradeLogicCore.Algorithms;

public record AlgoEntry<T>
{
    public long Id { get; set; }

    public DateTime Time { get; set; }

    public T? Variables { get; set; }

    /// <summary>
    /// Current price.
    /// </summary>
    public decimal Price { get; set; } = decimal.MinValue;
    /// <summary>
    /// The low price in the OHLC price, to verify if StopLoss is hit.
    /// </summary>
    public decimal Low { get; set; } = decimal.MinValue;
    /// <summary>
    /// Return vs previous OHLC price using two close prices.
    /// </summary>
    public decimal Return { get; set; } = 0;

    public bool IsLongSignal { get; set; }
    public bool IsShortSignal { get; set; }
    public bool IsOpened { get; set; }
    public bool IsClosing { get; set; }
    public bool IsStopLossTriggered { get; set; }

    public decimal Quantity { get; set; }
    public decimal EnterPrice { get; set; }
    public DateTime EnterTime { get; set; }
    public decimal ExitPrice { get; set; }
    public DateTime ExitTime { get; set; }
    public decimal StopLossPrice { get; set; }

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
    /// Gets / sets the portfolio snapshot related to current entry.
    /// </summary>
    public Portfolio? Portfolio { get; set; }
}