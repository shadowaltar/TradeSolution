namespace TradeCommon.Essentials.Instruments;

public static class SecurityTypeConverter
{
    public static SecurityType Parse(string? str)
    {
        if (str == null)
            return SecurityType.Unknown;

        str = str.Trim().ToUpperInvariant();
        if (StockTypes.Contains(str))
            return SecurityType.Equity;

        return str switch
        {
            "FX" or "CRYPTO" => SecurityType.Fx,
            "FUTURE" or "FUTURES" => SecurityType.Future,
            "FORWARD" => SecurityType.Forward,
            "OPTION" => SecurityType.Option,
            _ => SecurityType.Unknown,
        };
    }

    public static readonly IList<string> StockTypes = new string[] {
        "EQUITY", "STOCK", "ADR",
        "REAL ESTATE INVESTMENT TRUSTS", "REITS", "REIT",
        "EXCHANGE TRADED PRODUCTS", "ETP"};

    public static bool Matches(string typeStr, SecurityType type) => Parse(typeStr) == type;

    public static bool IsEquity(string typeStr) => Matches(typeStr, SecurityType.Equity);
}
