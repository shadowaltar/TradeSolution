using System.ComponentModel;

namespace TradeCommon.Essentials.Trading;

public enum OrderTimeInForceType
{
    Unknown,

    /// <summary>
    /// (GoodTillDay) Order is valid until today's market close, or a specific date if set.
    /// </summary>
    [Description("GTD")]
    GoodTillDay,
    /// <summary>
    /// (GoodTillCancel) Order is valid unless it is filled or cancelled.
    /// Supported by:
    ///     Binance
    /// </summary>
    [Description("GTC")]
    GoodTillCancel,
    /// <summary>
    /// (FoK) If exact quantity match the price of depth level, fill it, or else kill it.
    /// Supported by:
    ///     Binance
    /// </summary>
    [Description("FOK")]
    FillOrKill,
    /// <summary>
    /// (IoC) Fill as much quantity as possible in the order book, and kill the rest of proportion if any.
    /// Supported by:
    ///     Binance
    /// </summary>
    [Description("IOC")]
    ImmediateOrCancel,
}
