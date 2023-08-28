using TradeCommon.Runtime;

namespace TradeCommon.Database;

public interface IPersistenceTask
{
    DatabaseActionType ActionType { get; }
}