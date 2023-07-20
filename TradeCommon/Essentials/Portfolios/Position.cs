namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// The info of a current open position with respect to a security.
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

    /// <summary>
    /// The quantity which this position holds.
    /// Negative indicates it is in short-selling state.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Currency of the quantity. If it is an FX position, this
    /// returns the quote currency.
    /// </summary>
    public string Currency { get; set; }
}
