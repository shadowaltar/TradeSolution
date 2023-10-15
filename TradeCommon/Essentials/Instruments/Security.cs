using Common.Attributes;
using Microsoft.Diagnostics.Runtime.Utilities;
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
    public decimal TickSize { get; set; }
    public string? Currency { get; set; }

    [DatabaseIgnore]
    public Security QuoteSecurity { get; set; }
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

    public bool IsAsset => Id == QuoteSecurity.Id;

    /// <summary>
    /// Ensure and return the currency/quote asset.
    /// If null, throws exception.
    /// </summary>
    /// <returns></returns>
    public Security EnsureCurrencyAsset()
    {
        return QuoteSecurity ?? FxInfo?.QuoteAsset ?? throw Exceptions.MissingQuoteAsset(Code);
    }

    public decimal RoundLotSize(decimal proposedQuantity)
    {
        var lotSizeReciprocal = 1 / LotSize;
        var result = Math.Ceiling(proposedQuantity * lotSizeReciprocal) / lotSizeReciprocal;
        if (result > proposedQuantity)
            result -= LotSize;
        return result;
    }

    /// <summary>
    /// Round price to security's tick size.
    /// Optionally provide a hint price which will try to round to it as close as possible;
    /// by default hint price is 0.
    /// </summary>
    /// <param name="proposedPrice"></param>
    /// <returns></returns>
    public decimal RoundTickSize(decimal proposedPrice, decimal roundingHint = 0)
    {
        var tickSizeReciprocal = 1 / TickSize;
        decimal result;
        if (proposedPrice > roundingHint)
        {
            result = Math.Floor(proposedPrice * tickSizeReciprocal) / tickSizeReciprocal;
        }
        else
        {
            result = Math.Ceiling(proposedPrice * tickSizeReciprocal) / tickSizeReciprocal;
        }
        if (proposedPrice < 0 && result > 0)
            result -= TickSize;
        if (proposedPrice > 0 && result < 0)
            result += TickSize;
        return result;
    }

    public decimal GetStopLossPrice(decimal price, decimal signedStopLossRatio)
    {
        var result = decimal.Round(price * (1 - signedStopLossRatio), PricePrecision);
        if (TickSize != 0)
            result = RoundTickSize(result, price);
        return result;
    }

    public decimal GetTakeProfitPrice(decimal price, decimal signedTakeProfitRatio)
    {
        var result = decimal.Round(price * (1 + signedTakeProfitRatio), PricePrecision);
        if (TickSize != 0)
            result = RoundTickSize(result, price);
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
