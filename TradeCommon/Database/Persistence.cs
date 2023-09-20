using Common;
using System.Collections.Concurrent;
using TradeCommon.Runtime;

namespace TradeCommon.Database;
public class Persistence : IDisposable
{
    private readonly IStorage _storage;
    private readonly ConcurrentQueue<PersistenceTask> _tasks = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Pool<PersistenceTask> _taskPool = new();

    private bool _isRunning;

    public Persistence(IStorage storage)
    {
        Task.Factory.StartNew(async (t) =>
        {
            _isRunning = true;
            await Run();
        }, TaskCreationOptions.LongRunning, CancellationToken.None);
        _storage = storage;
    }

    public void Enqueue<T>(T entry, object? parameter = null, bool isUpsert = true)
    {
        if (entry != null)
        {
            var task = _taskPool.Lease();
            task.SetEntry(entry, parameter);
            task.IsUpsert = isUpsert;
            task.Type = typeof(T);
            _tasks.Enqueue(task);
        }
    }

    public void Enqueue<T>(List<T> entries, object? parameter = null, bool isUpsert = true)
    {
        if (!entries.IsNullOrEmpty())
        {
            var task = _taskPool.Lease();
            task.SetEntries(entries, parameter);
            task.IsUpsert = isUpsert;
            task.Type = typeof(T);
            _tasks.Enqueue(task);
        }
    }

    private async Task Run()
    {
        while (_isRunning)
        {
            if (_tasks.TryDequeue(out var task))
            {
                await _storage.Insert(task, task.IsUpsert);
                task.Clear();
                _taskPool.Return(task);
            }
            else
            {
                Thread.Sleep(100);
            }
        }
    }

    public void Dispose()
    {
        _isRunning = false;
        _cancellationTokenSource.Cancel();
        _tasks.Clear();
    }
}
