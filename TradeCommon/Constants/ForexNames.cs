namespace TradeCommon.Constants;
public static class ForexNames
{
    public const string Hkd = "HKD";
    public const string Usd = "USD";
    public const string Eur = "EUR";

    public static readonly List<string> Assets = new()
    {
        Usd, Hkd, Eur,
    };
}
