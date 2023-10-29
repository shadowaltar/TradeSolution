using TradeCommon.Infra;

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
}