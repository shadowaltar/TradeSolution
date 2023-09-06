using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeCommon.Database;

public class PersistenceTask<T> : IPersistenceTask
{
    public string TypeName { get; }

    public PersistenceTask(T entry, string? databaseName = null)
    {
        Entry = entry;
        TypeName = typeof(T).Name;
        DatabaseName = databaseName ?? DatabaseNames.GetDatabaseName<T>();
    }

    public PersistenceTask(List<T> entries, string? databaseName = null)
    {
        Entries = entries;
        TypeName = typeof(T).Name;
        DatabaseName = databaseName ?? DatabaseNames.GetDatabaseName<T>();
    }

    public DatabaseActionType ActionType { get; set; } = DatabaseActionType.Create;

    public int SecurityId { get; set; }
    public IntervalType IntervalType { get; set; }
    public SecurityType SecurityType { get; set; }

    /// <summary>
    /// For single item usage.
    /// </summary>
    public T? Entry { get; private set; }

    /// <summary>
    /// For batch / transaction item processing.
    /// </summary>
    public List<T>? Entries { get; private set; }

    /// <summary>
    /// Target table for insertion.
    /// </summary>
    public string TableName { get; private set; }

    /// <summary>
    /// Target database for insertion.
    /// </summary>
    public string DatabaseName { get; private set; }
}