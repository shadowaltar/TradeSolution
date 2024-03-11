using Common;

namespace TradeCommon.Essentials.Quotes;
public record OrderBook
{
    public List<OrderBookLevel> Bids { get; set; } = [];
    public List<OrderBookLevel> Asks { get; set; } = [];
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

    public virtual OrderBook DeepClone()
    {
        var clone = this with { };
        clone.Bids = [];
        foreach (var bid in Bids)
        {
            clone.Bids.Add(bid with { });
        }
        clone.Asks = [];
        foreach (var ask in Asks)
        {
            clone.Asks.Add(ask with { });
        }
        return clone;
    }
}

public record ExtendedOrderBook : OrderBook
{
    public DateTime Time { get; set; }
    public long SecurityId { get; set; }

    public override string ToString()
    {
        return $"{Time:yyyyMMdd-HHmmss.fff} B/A: {Bids.FirstOrDefault()?.Price}/{Asks.FirstOrDefault()?.Price}, Size:{Bids.FirstOrDefault()?.Size}/{Asks.FirstOrDefault()?.Size}";
    }

    public override ExtendedOrderBook DeepClone()
    {
        var clone = this with { };
        clone.Bids = [];
        foreach (var bid in Bids)
        {
            clone.Bids.Add(bid with { });
        }
        clone.Asks = [];
        foreach (var ask in Asks)
        {
            clone.Asks.Add(ask with { });
        }
        return clone;
    }
}