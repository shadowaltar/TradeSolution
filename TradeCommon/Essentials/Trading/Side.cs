namespace TradeCommon.Essentials.Trading;

/// <summary>
/// Side of trade / order objects.
/// </summary>
public enum Side
{
    /// <summary>
    /// No explicit buy or sell.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates it is a buy or long.
    /// </summary>
    Buy = 1,

    /// <summary>
    /// Indicates it is a sell or short.
    /// </summary>
    Sell = -1,
}
