namespace TradeCommon.Essentials.Quotes;
public record Tick
{
    public decimal Bid { get; set; }
    public decimal BidSize { get; set; }
    public decimal Ask { get; set; }
    public decimal AskSize { get; set; }

    public decimal Mid => (Bid + Ask) / 2;
    public decimal Spread => Ask - Bid;

    public override string ToString()
    {
        return $"{Bid} / {Mid} / {Ask}; Spread: {Spread}; Sizes: {BidSize} / {AskSize}";
    }
}
