using Common;
using TradeCommon.Constants;
using TradeCommon.Essentials.Trading;
using Common.Attributes;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// The info of a position with respect to a security.
///
/// Usually for one account + security, only one position exists.
///
/// When quantity reaches zero, another position should be created
/// instead of modifying the closed one.
/// </summary>
public record Position
{
    /// <summary>
    /// Unique id of this position.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The account id associated to this position.
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// The time which the position is established.
    /// It should be the first trade fills.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// The time which the position is updated.
    /// </summary>
    public DateTime UpdateTime { get; set; } = DateTime.MaxValue;

    /// <summary>
    /// The quantity which this position holds.
    /// It is the sum of all buy + sell trades' quantities.
    /// Negative indicates it is in short-selling state.
    /// Zero means the position has been closed.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// The quantity which this position holds and cannot be traded.
    /// </summary>
    public decimal LockQuantity { get; set; }

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
    /// Position's security code.
    /// </summary>
    public string? SecurityCode { get; set; } = null;

    /// <summary>
    /// The security id of this position.
    /// If it is an equity position, it is the security definition Id.
    /// If it is an fx position, it is the base-quote pair definition Id.
    /// If it is a cash position, it is the id of a pure asset, like USD, EUR, BTC etc. 
    /// </summary>
    public int SecurityId { get; set; }

    /// <summary>
    /// Position base asset's Id.
    /// If it is an equity position, it is the same as <see cref="SecurityId"/>.
    /// If it is an fx position, it is the base asset Id.
    /// If it is a cash position, it is the id of a pure asset, like USD, EUR, BTC etc.
    /// </summary>
    public int BaseAssetId { get; set; } = -1;

    /// <summary>
    /// Position quote asset's Id.
    /// If it is an equity position, it is USD for US market, or HKD for HK market.
    /// If it is an fx position, it is the quote asset Id.
    /// If it is a cash position, it is the id of a pure asset, like USD, EUR, BTC etc.
    /// </summary>
    public int QuoteAssetId { get; set; } = -1;

    /// <summary>
    /// Realized pnl, which is the sum of all realized pnl from each closed trades.
    /// </summary>
    public decimal RealizedPnl { get; set; }

    /// <summary>
    /// All orders related to this position.
    /// </summary>
    [DatabaseIgnore]
    public List<Order> Orders { get; set; } = new();

    /// <summary>
    /// All trades related to this position.
    /// </summary>
    [DatabaseIgnore]
    public List<Trade> Trades { get; set; } = new();

    /// <summary>
    /// Whether it is a closed position.
    /// Usually it means (remaining) quantity equals to zero.
    /// </summary>
    [UpsertIgnore]
    public bool IsClosed => Quantity == 0;
}
