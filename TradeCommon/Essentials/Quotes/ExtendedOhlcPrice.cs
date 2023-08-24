namespace TradeCommon.Essentials.Quotes;

/// <summary>
/// An extended version of a candlestick price object with security info.
/// </summary>
/// <param name="Code">Code of security</param>
/// <param name="Ex">Exchange of security</param>
/// <param name="O">Open</param>
/// <param name="H">High</param>
/// <param name="L">Low</param>
/// <param name="C">Close</param>
/// <param name="AC">Adjusted Close</param>
/// <param name="V">Volume</param>
/// <param name="I">Interval</param>
/// <param name="T">StartTime</param>
public record ExtendedOhlcPrice(string Code, string Ex, decimal O, decimal H, decimal L, decimal C, decimal AC, decimal V, string I, DateTime T)
    : OhlcPrice(O, H, L, C, AC, V, T);