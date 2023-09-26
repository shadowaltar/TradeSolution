using Common.Attributes;
using TradeCommon.Constants;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Instruments;
public class Security
{
    [AutoIncrementOnInsert]
    public int Id { get; set; } = 0;
    public string Code { get; set; }
    public string Name { get; set; }
    public string Exchange { get; set; }
    public string Type { get; set; }
    public string? SubType { get; set; }
    public decimal LotSize { get; set; }
    public string? Currency { get; set; }

    [DatabaseIgnore]
    public Security CurrencyAsset { get; set; }
    public string? Cusip { get; set; }
    public string? Isin { get; set; }
    public string? YahooTicker { get; set; }
    public bool? IsShortable { get; set; }
    public FxSecurityInfo? FxInfo { get; set; }
    public StockSecurityInfo? StockInfo { get; set; }
    public OptionSecurityInfo? DerivativeInfo { get; set; }

    public int PricePrecision { get; set; }
    public int QuantityPrecision { get; set; }

    [DatabaseIgnore]
    public SecurityType SecurityType { get; set; }

    [DatabaseIgnore]
    public SecurityType SecuritySubType { get; set; }
    [DatabaseIgnore]
    public ExchangeType ExchangeType { get; set; }

    public bool IsAsset => Id == CurrencyAsset.Id;

    /// <summary>
    /// Ensure and return the currency/quote asset.
    /// If null, throws exception.
    /// </summary>
    /// <returns></returns>
    public Security EnsureCurrencyAsset()
    {
        return CurrencyAsset ?? FxInfo?.QuoteAsset ?? throw Exceptions.MissingQuoteAsset(Code);
    }

    public decimal RoundLotSize(decimal proposedQuantity)
    {
        var lotSizeReciprocal = 1 / LotSize;
        var result = Math.Ceiling(proposedQuantity * lotSizeReciprocal) / lotSizeReciprocal;
        if (result > proposedQuantity)
            result -= LotSize;
        return result;
    }

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
