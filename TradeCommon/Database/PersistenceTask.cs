﻿using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeCommon.Database;

public class PersistenceTask<T> : IPersistenceTask
{
    public PersistenceTask(T entry)
    {
        Entry = entry;
    }

    public PersistenceTask(List<T> entries)
    {
        Entries = entries;
    }

    public DatabaseActionType ActionType { get; set; } = DatabaseActionType.Create;

    public int SecurityId { get; set; }
    public IntervalType IntervalType { get; set; }
    public SecurityType SecurityType { get; set; }

    /// <summary>
    /// For single item usage.
    /// </summary>
    public T Entry { get; set; }

    /// <summary>
    /// For batch / transaction item processing.
    /// </summary>
    public List<T> Entries { get; set; }

    /// <summary>
    /// Target table for insertion.
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Target database for insertion.
    /// </summary>
    public string DatabaseName { get; set; }
}