using System.Collections;
using System.Runtime.CompilerServices;

namespace TradeCommon.Database;

public class PersistenceTask /*: IPersistenceTask*/
{
    private object? _entry;
    private IList? _entries;
    private object? _parameter;

    public Type? Type { get; set; }

    public bool IsUpsert { get; set; }

    public PersistenceTask()
    {
    }

    public PersistenceTask(object entry, object? parameter = null, bool isUpsert = true)
    {
        _entry = entry;
        _parameter = parameter;
        IsUpsert = isUpsert;
    }

    public PersistenceTask(IList entries, object? parameter = null, bool isUpsert = true)
    {
        _entries = entries;
        _parameter = parameter;
        IsUpsert = isUpsert;
    }

    //public Security? Security { get; set; }
    //public int SecurityId { get; set; }
    //public IntervalType IntervalType { get; set; }
    //public SecurityType SecurityType { get; set; }

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

    public void SetEntry(object o, object? parameter = null)
    {
        _entry = o;
        _parameter = parameter;
    }

    public void SetEntries(IList o, object? parameter = null)
    {
        _entries = o;
        _parameter = parameter;
    }

    public void Clear()
    {
        _entry = null;
        _entries = null;
        _parameter = null;
        Type = null;
    }
}