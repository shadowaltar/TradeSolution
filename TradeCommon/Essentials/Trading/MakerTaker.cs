namespace TradeCommon.Essentials.Trading;
public enum MakerTaker
{
    /// <summary>
    /// Used when Maker/Taker info is not available from the broker.
    /// </summary>
    Unknown,
    /// <summary>
    /// Maker (a trade is placed in order book and then being taken by other participants).
    /// </summary>
    Maker,
    /// <summary>
    /// Taker (a trade is executed against another entry already in order book).
    /// </summary>
    Taker,
}
