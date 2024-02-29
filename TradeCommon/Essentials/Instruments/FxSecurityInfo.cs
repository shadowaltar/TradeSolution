using Common;
using Common.Attributes;
using System.Text.Json.Serialization;

namespace TradeCommon.Essentials.Instruments;

public class FxSecurityInfo
{
    /// <summary>
    /// Indicates it is not an FX pair but the currency asset itself.
    /// </summary>
    public bool IsAsset => QuoteCurrency.IsBlank();

    public bool IsMarginTradingAllowed { get; set; } = false;

    public string? BaseCurrency { get; set; }
    public string? QuoteCurrency { get; set; }

    public decimal? MaxLotSize { get; set; }

    [DatabaseIgnore, JsonIgnore]
    public Security? BaseSecurity { get; set; }

    [DatabaseIgnore, JsonIgnore]
    public Security? QuoteSecurity { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is FxSecurityInfo info &&
               IsMarginTradingAllowed == info.IsMarginTradingAllowed &&
               BaseCurrency == info.BaseCurrency &&
               QuoteCurrency == info.QuoteCurrency &&
               MaxLotSize == info.MaxLotSize;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(IsMarginTradingAllowed, BaseCurrency, QuoteCurrency, MaxLotSize);
    }

    public override string ToString()
    {
        return $"{BaseCurrency}-{QuoteCurrency}";
    }
}
