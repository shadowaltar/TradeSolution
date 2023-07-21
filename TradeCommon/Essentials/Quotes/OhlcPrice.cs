namespace TradeCommon.Essentials.Quotes;

/// <summary>
/// A candlestick price object.
/// </summary>
/// <param name="O">Open</param>
/// <param name="H">High</param>
/// <param name="L">Low</param>
/// <param name="C">Close</param>
/// <param name="V">Volume</param>
/// <param name="T">StartTime</param>
public record OhlcPrice(decimal O, decimal H, decimal L, decimal C, decimal V, DateTime T)
{
    public static implicit operator OhlcPrice?((decimal O, decimal H, decimal L, decimal C, decimal V, DateTime T) tuple)
        => new(tuple.O, tuple.H, tuple.L, tuple.C, tuple.V, tuple.T);
}
