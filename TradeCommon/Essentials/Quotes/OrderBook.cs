using Common;
using TradeCommon.Constants;

namespace TradeCommon.Essentials.Quotes;
public record OrderBook
{
    public List<OrderBookLevel> Bids { get; set; } = new List<OrderBookLevel>(Consts.DefaultOrderBookDepth);
    public List<OrderBookLevel> Asks { get; set; } = new List<OrderBookLevel>(Consts.DefaultOrderBookDepth);
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
        clone.Bids = new List<OrderBookLevel>(Consts.DefaultOrderBookDepth);
        for (int i = 0; i < Consts.DefaultOrderBookDepth; i++)
        {
            if (Bids.Count <= i)
            {
                clone.Bids.Add(OrderBookLevel.BidPlaceholder);
            }
            else
            {
                clone.Bids.Add(Bids[i] with { });
            }
        }
        clone.Asks = new List<OrderBookLevel>(Consts.DefaultOrderBookDepth);
        for (int i = 0; i < Consts.DefaultOrderBookDepth; i++)
        {
            if (Asks.Count <= i)
            {
                clone.Asks.Add(OrderBookLevel.AskPlaceholder);
            }
            else
            {
                clone.Asks.Add(Asks[i] with { });
            }
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
        clone.Bids = new List<OrderBookLevel>(Consts.DefaultOrderBookDepth);
        for (int i = 0; i < Consts.DefaultOrderBookDepth; i++)
        {
            if (Bids.Count <= i)
            {
                clone.Bids.Add(OrderBookLevel.BidPlaceholder);
            }
            else
            {
                clone.Bids.Add(Bids[i] with { });
            }
        }
        clone.Asks = new List<OrderBookLevel>(Consts.DefaultOrderBookDepth);
        for (int i = 0; i < Consts.DefaultOrderBookDepth; i++)
        {
            if (Asks.Count <= i)
            {
                clone.Asks.Add(OrderBookLevel.AskPlaceholder);
            }
            else
            {
                clone.Asks.Add(Asks[i] with { });
            }
        }
        return clone;
    }
}