using System.Collections;
using System.Runtime.CompilerServices;

namespace TradeCommon.Database;

public class PersistenceTask
{
    private object? _entry;
    private IList? _entries;

    public Type? Type { get; set; }
    public string? TableNameOverride { get; set; }
    public bool IsUpsert { get; set; }

    public PersistenceTask()
    {
    }

    public PersistenceTask(object entry, bool isUpsert = true, string? tableNameOverride = null)
    {
        _entry = entry;
        TableNameOverride = tableNameOverride;
        IsUpsert = isUpsert;
    }

    public PersistenceTask(IList entries, bool isUpsert = true, string? tableNameOverride = null)
    {
        _entries = entries;
        TableNameOverride = tableNameOverride;
        IsUpsert = isUpsert;
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
    }
}