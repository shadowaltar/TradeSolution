namespace TradeCommon.Essentials.Quotes;
public record OrderBook
{
    public List<OrderBookLevel> Bids { get; } = new();
    public List<OrderBookLevel> Asks { get; } = new();
    public decimal BestBid => Bids.Count == 0 ? 0 : Bids[0].Price;
    public decimal BestAsk => Asks.Count == 0 ? 0 : Asks[0].Price;
    public decimal Mid => (BestBid + BestAsk) / 2;
    public decimal Spread => BestAsk - BestBid;
}
