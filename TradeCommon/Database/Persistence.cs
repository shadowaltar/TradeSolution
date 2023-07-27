using Common;
using System.Collections.Concurrent;

namespace TradeCommon.Database;
public class Persistence : IDisposable
{
    private readonly MessageBroker<IPersistenceTask> _broker;
    private readonly ConcurrentQueue<IPersistenceTask> _tasks = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isRunning;

    public event Action<IPersistenceTask>? Persisted;

    public Persistence(MessageBroker<IPersistenceTask> broker)
    {
        _broker = broker;
        _broker.NewItem += OnNewItem;
    }

    private void OnNewItem()
    {
        var item = _broker.Dequeue();
        if (item != null)
        {
            _tasks.Enqueue(item);
        }
    }

    public void Start()
    {
        _isRunning = true;
        Task.Factory.StartNew(() => Run(),
                              _cancellationTokenSource.Token,
                              TaskCreationOptions.LongRunning,
                              TaskScheduler.Default);
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
        _broker.NewItem -= OnNewItem;
    }
}
