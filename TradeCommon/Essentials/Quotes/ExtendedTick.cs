namespace TradeCommon.Essentials.Quotes;

public record ExtendedTick : Tick
{
    public DateTime Time { get; set; }
    public long SecurityId { get; set; }
    public string SecurityCode { get; set; }

    public ExtendedTick()
    {
        Reset();
    }

    public void Reset()
    {
        SecurityCode = "";
        SecurityId = 0;
        Bid = 0;
        BidSize = 0;
        Ask = 0;
        AskSize = 0;
        Time = DateTime.MinValue;
    }

    public override string ToString()
    {
        return $"[{SecurityId}][{SecurityCode}][{Time:HHmmss.fff}]{Bid} / {Mid} / {Ask}; Sizes: {BidSize} / {AskSize}";
    }
}
