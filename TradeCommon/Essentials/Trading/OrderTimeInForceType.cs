namespace TradeCommon.Essentials.Trading;

public enum OrderTimeInForceType
{
    /// <summary>
    /// (GoodTillDay) Order is valid until today's market close, or a specific date if set.
    /// </summary>
    GoodTillDay,
    /// <summary>
    /// (GoodTillCancel) Order is valid unless it is filled or cancelled.
    /// </summary>
    GoodTillCancel,
    /// <summary>
    /// (FoK) If exact quantity match the price of depth level, fill it, or else kill it.
    /// </summary>
    FillOrKill,
    /// <summary>
    /// (IoC) Fill as much quantity as possible in the order book, and kill the rest of propotion if any.
    /// </summary>
    ImmediateOrCancel,
}
