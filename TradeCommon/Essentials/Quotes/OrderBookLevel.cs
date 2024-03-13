namespace TradeCommon.Essentials.Quotes;

public record OrderBookLevel : ICloneable
{
    public decimal Price { get; set; }
    public decimal Size { get; set; }

    public override string ToString()
    {
        return $"{Price}: {Size}";
    }

    object ICloneable.Clone()
    {
        return this with { };
    }

    public static OrderBookLevel BidPlaceholder { get; } = new OrderBookLevel { Price = 0, Size = 0 };
    public static OrderBookLevel AskPlaceholder { get; } = new OrderBookLevel { Price = 0, Size = 0 };
}