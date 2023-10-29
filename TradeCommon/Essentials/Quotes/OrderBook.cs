using Common;

namespace TradeCommon.Essentials.Quotes;
public record OrderBook
{
    public List<OrderBookLevel> Bids { get; set; } = new();
    public List<OrderBookLevel> Asks { get; set; } = new();
    public decimal BestBid => Bids.Count == 0 ? 0 : Bids[0].Price;
    public decimal BestBidSize => Bids.Count == 0 ? 0 : Bids[0].Size;
    public decimal BestAsk => Asks.Count == 0 ? 0 : Asks[0].Price;
    public decimal BestAskSize => Asks.Count == 0 ? 0 : Asks[0].Size;
    public decimal Mid => (BestBid + BestAsk) / 2;
    public decimal Spread => BestAsk - BestBid;

    public override string ToString()
    {
        return Json.Serialize(this);
    }
}

public record ExtendedOrderBook : OrderBook
{
    public DateTime Time { get; set; }
    public int SecurityId { get; set; }

    public override string ToString()
    {
        return Json.Serialize(this, true);
    }
}