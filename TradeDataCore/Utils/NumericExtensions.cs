namespace TradeDataCore.Utils;
public static class NumericExtensions
{
    public static decimal? NullIfZero(this decimal value)
    {
        return value == 0 ? null : value;
    }
}
