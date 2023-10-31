using Common;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

namespace TradeCommon.Database;

public partial class Storage
{

    public async Task<int> Delete(PersistenceTask task)
    {
        int count;
        if (task.Type == typeof(Trade))
        {
            count = await Delete<Trade>(task);
        }
        else if (task.Type == typeof(Position))
        {
            count = await Insert<Position>(task);
        }
        else if (task.Type == typeof(Order))
        {
            count = await Insert<Order>(task);
        }
        else if (task.Type == typeof(Asset))
        {
            count = await Insert<Asset>(task);
        }
        else if (task.Type == typeof(Account))
        {
            count = await Insert<Account>(task);
        }
        else if (task.Type == typeof(Security))
        {
            count = await Insert<Security>(task);
        }
        else if (task.Type == typeof(FinancialStat))
        {
            count = await Insert<FinancialStat>(task);
        }
        else
            throw new InvalidOperationException($"Persistence task type {task.Type?.Name} is not supported.");
        return count;
    }

    public async Task<int> Delete<T>(PersistenceTask task) where T : class, new()
    {
        if (task.Action is not DatabaseActionType.Delete)
        {
            _log.Warn("Invalid db action type.");
            return 0;
        }
        var entry = task.GetEntry<T>();
        if (entry != null)
        {
            var count = await DeleteOne(entry);
            _log.Info($"Deleted {count} entry from database: {typeof(T).Name}");
            return count;
        }

        var entries = task.GetEntries<T>();
        if (!entries.IsNullOrEmpty())
        {
            var count = await DeleteMany(entries);
            _log.Info($"Deleted {count} entries from database: {typeof(T).Name}");
            return count;
        }
        throw Exceptions.Impossible();
    }

    public async Task<int> DeleteOne<T>(T entry, string? tableNameOverride = null) where T : class, new()
    {
        var writer = _writers.Get<T>();
        var (t, _) = DatabaseNames.GetTableAndDatabaseName(entry);
        return await writer.DeleteOne(entry, tableNameOverride ?? t);
    }

    public async Task<int> DeleteMany<T>(IList<T> entries, string? tableNameOverride = null) where T : class, new()
    {
        if (entries.Count == 0)
            return 0;
        var writer = _writers.Get<T>();
        var (t, _) = DatabaseNames.GetTableAndDatabaseName(entries.First());
        return await writer.DeleteMany(entries, tableNameOverride ?? t);
    }

    public async Task DeleteOpenOrderId(OpenOrderId openOrderId)
    {
        var writer = _writers.Get<OpenOrderId>();
        await writer.DeleteOne(openOrderId);
    }

    public async Task<int> MoveToError<T>(T entry) where T : SecurityRelatedEntry, new()
    {
        string? from = "";
        string? to = "";
        if (entry is Order order)
        {
            from = DatabaseNames.GetOrderTableName(order.Security.SecurityType);
            to = DatabaseNames.GetOrderTableName(order.Security.SecurityType, true);
        }
        else if (entry is Trade trade)
        {
            from = DatabaseNames.GetTradeTableName(trade.Security.SecurityType);
            to = DatabaseNames.GetTradeTableName(trade.Security.SecurityType, true);
        }
        else if (entry is Position position)
        {
            from = DatabaseNames.GetPositionTableName(position.Security.SecurityType);
            to = DatabaseNames.GetPositionTableName(position.Security.SecurityType, true);
        }
        if (from.IsBlank() || to.IsBlank())
            return 0;

        var writer = _writers.Get<T>();
        return await writer.MoveOne<T>(entry, true, from!, to!);
    }
}
