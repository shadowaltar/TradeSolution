namespace TradeCommon.Constants;
public static class SecurityTypes
{
    public static readonly IList<string> StockTypes = new string[] {
        "EQUITY", Stock, "ADR",
        "REAL ESTATE INVESTMENT TRUSTS", "REITS", "REIT",
        "EXCHANGE TRADED PRODUCTS", "ETP"};

    

    public const string Stock = "STOCK";
    public const string Fx = "FX";
    public const string Crypto = "CRYPTO";
    public const string Future = "FUTURE";
    public const string Futures = "FUTURES";
    public const string Forward = "FORWARD";
    public const string Option = "OPTION";
}
