using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeDataCore.Database;
public class Persistence
{
    private ConcurrentQueue<IPersistenceTask> _tasks = new();

    private bool _isRunning;

    public void Submit(PersistenceTask task)
    {
        _tasks.Enqueue(task);
    }

    private void Run()
    {
        while (_isRunning)
        {
            if (_tasks.TryDequeue(out var task))
            {
                await Storage.Insert(task);
            }
        }        
    }

    public Action<PersistenceTask> Persisted;
}
