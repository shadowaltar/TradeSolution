using TradeCommon.Constants;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// The info of a position with respect to a security.
///
/// Usually for one account + security, only one position exists.
///
/// When quantity reaches zero, another position should be created
/// instead of modifying the closed one.
/// </summary>
public class Position
{
    /// <summary>
    /// Unique id of this position.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The security id of this position.
    /// </summary>
    public int SecurityId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.MinValue;

    public DateTime EndTime { get; set; } = DateTime.MaxValue;

    /// <summary>
    /// The quantity which this position holds.
    /// It is the sum of all buy + sell trades' quantities.
    /// Negative indicates it is in short-selling state.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Price of this position.
    /// It is the weighted average price of all the trades related to this position.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Currency of the quantity. If it is an FX position, this
    /// returns the quote currency. Default is USD.
    /// </summary>
    public string Currency { get; set; } = ForexNames.Usd;

    /// <summary>
    /// Realized pnl, which is the sum of all realized pnl from each closed trades.
    /// </summary>
    public decimal RealizedPnl { get; set; }

    /// <summary>
    /// All orders related to this position.
    /// </summary>
    public List<Order> Orders { get; set; } = new();

    /// <summary>
    /// All trades related to this position.
    /// </summary>
    public List<Trade> Trades { get; set; } = new();

    /// <summary>
    /// Whether it is a closed position.
    /// Usually it means (remaining) quantity equals to zero.
    /// </summary>
    public bool IsClosed { get; set; }
}
