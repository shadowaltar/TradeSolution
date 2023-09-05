using TradeCommon.Constants;
using Common.Attributes;

namespace TradeCommon.Essentials.Instruments;
public class Security
{
    [UpsertIgnore]
    [AutoIncrementOnInsert]
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Exchange { get; set; }
    public string Type { get; set; }
    public string? SubType { get; set; }
    public double LotSize { get; set; }
    public string? Currency { get; set; }
    public string? Cusip { get; set; }
    public string? Isin { get; set; }
    public string? YahooTicker { get; set; }
    public bool? IsShortable { get; set; }
    public FxSecurityInfo? FxInfo { get; set; }
    public StockSecurityInfo? StockInfo { get; set; }
    public OptionSecurityInfo? DerivativeInfo { get; set; }

    public int PricePrecision { get; set; }
    public int QuantityPrecision { get; set; }

    public override string ToString()
    {
        return $"[{Id}] [{Code} {Exchange}] {Name} ({Type})";
    }
}


public static class SecurityExtensions
{
    public static bool IsFrom(this Security security, string externalName)
    {
        return security.Exchange.Equals(externalName, StringComparison.OrdinalIgnoreCase);
    }
}
