namespace TradeCommon.Runtime;

public interface ITimeRelatedEntry
{
    DateTime CreateTime { get; set; }
    DateTime UpdateTime { get; set; }
}