using System.Collections.Concurrent;

namespace Common;

public class MessageBroker<T>
{
    private readonly ConcurrentQueue<T> _queue = new();

    public event Action? NewItem;

    public void Enqueue(T item)
    {
        _queue.Enqueue(item);

        NewItem?.Invoke();
    }

    public T? Dequeue()
    {
        if (_queue.TryDequeue(out var item))
            return item;
        return default;
    }
}
