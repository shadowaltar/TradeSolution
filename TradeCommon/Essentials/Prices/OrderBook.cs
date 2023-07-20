using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Essentials.Prices;
public class OrderBook
{
    public List<OrderBookLevel> Bids { get; } = new();
    public List<OrderBookLevel> Asks { get; } = new();
    public decimal BestBid => Bids.Count == 0 ? 0 : Bids[0].Price;
    public decimal BestAsk => Asks.Count == 0 ? 0 : Asks[0].Price;
    public decimal Mid => (BestBid + BestAsk) / 2;
    public decimal Spread => BestAsk - BestBid;
}
