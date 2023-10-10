using System.Collections;
using System.Runtime.CompilerServices;
using TradeCommon.Runtime;

namespace TradeCommon.Database;

public class PersistenceTask
{
    private object? _entry;
    private IList? _entries;

    public Type? Type { get; set; }
    public string? TableNameOverride { get; set; }
    public DatabaseActionType Action { get; set; } = DatabaseActionType.Unknown;

    public PersistenceTask()
    {
    }

    public T? GetEntry<T>() where T : class
    {
        if (_entry != null)
            return Unsafe.As<T>(_entry);
        return default;
    }

    public IList<T> GetEntries<T>() where T : class
    {
        if (_entries != null)
            return Unsafe.As<IList<T>>(_entries);
        return new List<T>(0);
    }

    public void SetEntry(object o)
    {
        _entry = o;
    }

    public void SetEntries(IList o)
    {
        _entries = o;
    }

    public void Clear()
    {
        _entry = null;
        _entries = null;
        Type = null;
        Action = DatabaseActionType.Unknown;
    }
}