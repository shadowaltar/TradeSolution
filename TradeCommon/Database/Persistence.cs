using System.Collections.Concurrent;
using TradeCommon.Runtime;

namespace TradeCommon.Database;
public class Persistence : IDisposable
{
    private readonly ConcurrentQueue<IPersistenceTask> _tasks = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    private bool _isRunning;

    public event Action<IPersistenceTask>? Persisted;

    public Persistence()
    {
        Task.Factory.StartNew(async (t) =>
        {
            _isRunning = true;
            await Run();
        }, TaskCreationOptions.LongRunning, CancellationToken.None);
    }

    public void Enqueue(IPersistenceTask task)
    {
        _tasks.Enqueue(task);
    }

    private async Task Run()
    {
        while (_isRunning)
        {
            if (_tasks.TryDequeue(out var task))
            {
                if (task.ActionType == DatabaseActionType.Create)
                    await Storage.Insert(task, false);
                else if (task.ActionType == DatabaseActionType.Update)
                    await Storage.Insert(task, true);
                else
                    return;
                Persisted?.Invoke(task);
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
        Persisted = null;
        _tasks.Clear();
    }
}
