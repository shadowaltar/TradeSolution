namespace TradeCommon.Essentials.Quotes;

public class OrderBookLevel
{
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
    public string Source { get; set; } = "";
}