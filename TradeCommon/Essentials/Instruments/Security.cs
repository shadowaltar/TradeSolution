using Common;
using Common.Attributes;
using System.Text.Json.Serialization;
using TradeCommon.Constants;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Instruments;
public class Security
{
    [AutoIncrementOnInsert]
    public int Id { get; set; } = 0;
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Exchange { get; set; } = "";
    public string Type { get; set; } = "";
    public string? SubType { get; set; }
    public decimal LotSize { get; set; }
    public decimal TickSize { get; set; }
    public decimal MinNotional { get; set; }
    [DatabaseIgnore]
    public decimal MinQuantity { get; set; }
    public string? Currency { get; set; }

    [DatabaseIgnore, JsonIgnore]
    public Security? QuoteSecurity { get; set; }
    public string? Cusip { get; set; }
    public string? Isin { get; set; }
    public string? YahooTicker { get; set; }
    public bool? IsShortable { get; set; }
    public int PricePrecision { get; set; }
    public int QuantityPrecision { get; set; }
    public FxSecurityInfo? FxInfo { get; set; }
    public StockSecurityInfo? StockInfo { get; set; }
    public OptionSecurityInfo? DerivativeInfo { get; set; }

    [DatabaseIgnore, JsonIgnore]
    public SecurityType SecurityType { get; set; }

    [DatabaseIgnore, JsonIgnore]
    public SecurityType SecuritySubType { get; set; }
    [DatabaseIgnore, JsonIgnore]
    public ExchangeType ExchangeType { get; set; }
    [DatabaseIgnore, JsonIgnore]
    public bool IsAsset => Id == QuoteSecurity?.Id;

    /// <summary>
    /// Ensure and return the currency/quote asset.
    /// If null, throws exception.
    /// </summary>
    /// <returns></returns>
    public Security EnsureCurrencyAsset()
    {
        return QuoteSecurity ?? FxInfo?.QuoteAsset ?? throw Exceptions.MissingQuoteAsset(Code);
    }

    /// <summary>
    /// Round quantity to security's lot size.
    /// </summary>
    /// <param name="proposedQuantity"></param>
    /// <returns></returns>
    public decimal RoundLotSize(decimal proposedQuantity)
    {
        if (!proposedQuantity.IsValid()) return proposedQuantity;
        if (LotSize == 0) return proposedQuantity;

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
        if (!proposedPrice.IsValid()) return proposedPrice;
        if (TickSize == 0) return proposedPrice;

        decimal result = proposedPrice > roundingHint
            ? Math.Floor(proposedPrice * (1 / TickSize)) * TickSize
            : Math.Ceiling(proposedPrice * (1 / TickSize)) * TickSize;
        if (proposedPrice < 0 && result > 0)
            result -= TickSize;
        if (proposedPrice > 0 && result < 0)
            result += TickSize;
        return result;
    }

    /// <summary>
    /// Get a stop loss price by a signed stop loss ratio.
    /// If exit side is sell then the sign is +ve, otherwise it is +ve.
    /// </summary>
    /// <param name="price"></param>
    /// <param name="signedStopLossRatio"></param>
    /// <returns></returns>
    public decimal GetStopLossPrice(decimal price, decimal signedStopLossRatio)
    {
        var result = decimal.Round(price * (1 - signedStopLossRatio), PricePrecision);
        if (TickSize != 0)
            result = RoundTickSize(result, price);
        return result;
    }

    /// <summary>
    /// Get a take profit price by a signed stop loss ratio.
    /// If exit side is sell then the sign is +ve, otherwise it is +ve.
    /// </summary>
    /// <param name="price"></param>
    /// <param name="signedTakeProfitRatio"></param>
    /// <returns></returns>
    public decimal GetTakeProfitPrice(decimal price, decimal signedTakeProfitRatio)
    {
        var result = decimal.Round(price * (1 + signedTakeProfitRatio), PricePrecision);
        if (TickSize != 0)
            result = RoundTickSize(result, price);
        return result;
    }

    public string FormatPrice(decimal price)
    {
        return RoundTickSize(price).ToString();
    }

    public string FormatQuantity(decimal quantity)
    {
        return RoundLotSize(quantity).ToString();
    }

    public string FormatTick(Tick tick)
    {
        var t = tick with
        {
            Bid = RoundTickSize(tick.Bid),
            Ask = RoundTickSize(tick.Ask),
            BidSize = RoundTickSize(tick.BidSize),
            AskSize = RoundTickSize(tick.AskSize),
        };
        return t.ToString();
    }

    public override string ToString()
    {
        return $"[{Id}] [{Code} {Exchange}] {Name} ({Type})";
    }

    public override bool Equals(object? obj)
    {
        return obj is Security security &&
               Id == security.Id &&
               Code == security.Code &&
               Name == security.Name &&
               Exchange == security.Exchange &&
               Type == security.Type &&
               SubType == security.SubType &&
               LotSize == security.LotSize &&
               TickSize == security.TickSize &&
               MinNotional == security.MinNotional &&
               Currency == security.Currency &&
               EqualityComparer<Security>.Default.Equals(QuoteSecurity, security.QuoteSecurity) &&
               Cusip == security.Cusip &&
               Isin == security.Isin &&
               YahooTicker == security.YahooTicker &&
               IsShortable == security.IsShortable &&
               EqualityComparer<FxSecurityInfo?>.Default.Equals(FxInfo, security.FxInfo) &&
               EqualityComparer<StockSecurityInfo?>.Default.Equals(StockInfo, security.StockInfo) &&
               EqualityComparer<OptionSecurityInfo?>.Default.Equals(DerivativeInfo, security.DerivativeInfo) &&
               PricePrecision == security.PricePrecision &&
               QuantityPrecision == security.QuantityPrecision &&
               SecurityType == security.SecurityType &&
               SecuritySubType == security.SecuritySubType &&
               ExchangeType == security.ExchangeType &&
               IsAsset == security.IsAsset;
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Id);
        hash.Add(Code);
        hash.Add(Name);
        hash.Add(Exchange);
        hash.Add(Type);
        hash.Add(SubType);
        hash.Add(LotSize);
        hash.Add(TickSize);
        hash.Add(MinNotional);
        hash.Add(Currency);
        hash.Add(QuoteSecurity?.Id);
        hash.Add(Cusip);
        hash.Add(Isin);
        hash.Add(YahooTicker);
        hash.Add(IsShortable);
        hash.Add(FxInfo);
        hash.Add(StockInfo);
        hash.Add(DerivativeInfo);
        hash.Add(PricePrecision);
        hash.Add(QuantityPrecision);
        hash.Add(SecurityType);
        hash.Add(SecuritySubType);
        hash.Add(ExchangeType);
        hash.Add(IsAsset);
        return hash.ToHashCode();
    }
}


public static class SecurityExtensions
{
    public static bool IsFrom(this Security security, string externalName)
    {
        return security.Exchange.Equals(externalName, StringComparison.OrdinalIgnoreCase);
    }
}
