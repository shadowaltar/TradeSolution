namespace TradeCommon.Essentials.Instruments;

public record SimpleSecurity(long Id, string Code, string Name, string Exchange, string Type)
{
    public SimpleSecurity(Security security) : this(security.Id, security.Code, security.Name, security.Exchange, security.Type)
    {
    }

    public override string ToString()
    {
        return $"[{Id}] [{Code} {Exchange}] {Name} ({Type})";
    }
}
