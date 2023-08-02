using System.Collections.Concurrent;
using TradeCommon.Utils.Common;

namespace TradeCommon.Database;
public class Persistence : IDisposable
{
    private readonly MessageBroker<IPersistenceTask> _broker;
    private readonly ConcurrentQueue<IPersistenceTask> _tasks = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly bool _isRunning;

    public event Action<IPersistenceTask>? Persisted;

    public Persistence(MessageBroker<IPersistenceTask> broker)
    {
        _broker = broker;
        Task.Factory.StartNew(async (t) => await Run(), TaskCreationOptions.LongRunning, CancellationToken.None);
    }

    public void Enqueue(IPersistenceTask task)
    {
        _broker.Enqueue(task);
    }

    private async Task Run()
    {
        while (_isRunning)
        {
            if (_tasks.TryDequeue(out var task))
            {
                await Storage.Insert(task);
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
        _cancellationTokenSource.Cancel();
        Persisted = null;
        _broker.Dispose();
    }
}
