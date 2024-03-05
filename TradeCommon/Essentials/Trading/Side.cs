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

public static class Sides
{
    public static Side[] BuySell { get; } = [Side.Buy, Side.Sell];
}

public class SideConverter
{
    public static Side Parse(string? sideStr)
    {
        if (sideStr == null)
            return Side.None;

        sideStr = sideStr.Trim().ToUpperInvariant();
        return sideStr switch
        {
            "B" or "BUY" or "LONG" or "L" or "BID" or "1" => Side.Buy,
            "S" or "SELL" or "SHORT" or "OFFER" or "-1" => Side.Sell,
            _ => Side.None
        };
    }

    public static string ToBinance(Side side)
    {
        return side switch
        {
            Side.Buy => "BUY",
            Side.Sell => "SELL",
            _ => ""
        };
    }
}