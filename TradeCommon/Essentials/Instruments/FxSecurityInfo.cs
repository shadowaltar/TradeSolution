namespace TradeCommon.Essentials.Instruments;

public class FxSecurityInfo
{
    public string? BaseCurrency { get; set; }
    public string? QuoteCurrency { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is FxSecurityInfo setting &&
               BaseCurrency == setting.BaseCurrency &&
               QuoteCurrency == setting.QuoteCurrency;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(BaseCurrency, QuoteCurrency);
    }

    public override string ToString()
    {
        return $"{BaseCurrency}-{QuoteCurrency}";
    }
}
