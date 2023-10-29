using Common;
using Common.Attributes;
using TradeCommon.Database;

namespace TradeCommon.Essentials.Quotes;

[Storage("depth_books_{0}", DatabaseNames.MarketData)]
public record DepthBook
{
    public DateTime Time;
    public int SecurityId;

    [DatabaseIgnore]
    public List<DepthLevel> Asks = new();

    [DatabaseIgnore]
    public List<DepthLevel> Bids = new();

    public string FlattenBids => Json.Serialize(Bids, true);
    public string FlattenAsks => Json.Serialize(Asks, true);
}


public record struct DepthLevel
{
    public decimal P;
    public decimal Q;
}