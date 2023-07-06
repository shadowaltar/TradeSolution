namespace TradeLogicCore.Essentials;
public enum OrderSide
{
    Buy,
    Sell,
}

public enum OrderType
{
    Limit,
    Market,
}

public enum OrderFilling
{
    /// <summary>
    /// (FoK) If exact quantity match the price of depth level, fill it, or else kill it.
    /// </summary>
    FillExactPriceOrKillAll,
    /// <summary>
    /// (FaK) Fill as much quantity as possible in the order book, and kill the rest of propotion if any.
    /// </summary>
    FillAllPricesAndKillRest,
}

public enum OrderValidity
{
    /// <summary>
    /// (GoodTillDate) Order is valid for the rest of the day (until market close).
    /// </summary>
    Day,
    /// <summary>
    /// (GoodTillDate) Order is valid until expiry date.
    /// </summary>
    Expiry,
    /// <summary>
    /// (GoodTillDate) Order is valid until a date specified.
    /// </summary>
    Specified,
    /// <summary>
    /// (GoodTillCancel) Order is valid unless it is filled or cancelled.
    /// </summary>
    Cancel
}
