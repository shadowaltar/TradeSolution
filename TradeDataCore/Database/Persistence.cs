using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Database;

namespace TradeDataCore.Database;
public class Persistence
{
    private ConcurrentQueue<IPersistenceTask> _tasks = new();

    private bool _isRunning;

    public void Submit(IPersistenceTask task)
    {
        _tasks.Enqueue(task);
    }

    private async Task Run()
    {
        while (_isRunning)
        {
            if (_tasks.TryDequeue(out var task))
            {
                await Storage.Insert(task);
            }
        }        
    }

    public Action<IPersistenceTask> Persisted;
}
