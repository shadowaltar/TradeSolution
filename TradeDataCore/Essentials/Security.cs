namespace TradeDataCore.Essentials;
public class Security
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Exchange { get; set; }
    public string Type { get; set; }
    public string? SubType { get; set; }
    public int LotSize { get; set; }
    public string Currency { get; set; }
    public string? Cusip { get; set; }
    public string? Isin { get; set; }
    public string? YahooTicker { get; set; }
    public bool? IsShortable { get; set; }
    public FxSetting? FxSetting { get; set; }
    public StockSetting? StockSetting { get; set; }
    public DerivativeSetting? DerivativeSetting { get; set; }
}


public class FxSetting
{
    public string BaseCurrency { get; set; }
    public string QuoteCurrency { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is FxSetting setting &&
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

public class StockSetting
{
    public string? Board { get; set; }
}


public class DerivativeSetting
{
    public DateTime? ExpiryDate { get; set; }
}