namespace TradeCommon.Runtime;

public interface ITimeBasedUniqueIdEntry
{
    long Id { get; set; }

    bool EqualsIgnoreId(ITimeBasedUniqueIdEntry other);
}
