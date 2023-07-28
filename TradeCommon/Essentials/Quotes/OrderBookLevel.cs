namespace TradeCommon.Essentials.Quotes;

public record OrderBookLevel
{
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
}