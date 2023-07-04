namespace TradeDataCore.Essentials;
public class Security
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Exchange { get; set; }
    public string Type { get; set; }
    public string? SubType { get; set; }
    public int LotSize { get; set; }
    public string? Cusip { get; set; }
    public string? Isin { get; set; }
    public bool IsShortable { get; set; }
    public StockSetting? StockSetting { get; set; }
    public DerivativeSetting? DerivativeSetting { get; set; }
}


public class StockSetting
{
    public string? Board { get; set; }
}


public class DerivativeSetting
{
    public DateTime? ExpiryDate { get; set; }
}