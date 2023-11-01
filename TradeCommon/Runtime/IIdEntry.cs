namespace TradeCommon.Runtime;

public interface IIdEntry
{
    long Id { get; set; }

    bool EqualsIgnoreId(IIdEntry other);
}
