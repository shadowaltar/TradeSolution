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

    public static readonly IReadOnlyDictionary<PriceElementType, Func<OhlcPrice, decimal>> PriceElementSelectors = new Dictionary<PriceElementType, Func<OhlcPrice, decimal>>
    {
        { PriceElementType.Open, new(p => p.O) },
        { PriceElementType.High, new(p => p.H) },
        { PriceElementType.Low, new(p => p.L) },
        { PriceElementType.Close, new(p => p.C) },
        { PriceElementType.Volume, new(p => p.V) },
        { PriceElementType.Typical3, new(p => (p.H + p.L + p.C) / 3m) },
        { PriceElementType.Typical4, new(p => (p.O + p.H + p.L + p.C) / 4m) }
    };
}
