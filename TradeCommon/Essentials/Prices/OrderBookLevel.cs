namespace TradeCommon.Essentials.Prices;

public class OrderBookLevel
{
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
    public string Source { get; set; } = "";
}