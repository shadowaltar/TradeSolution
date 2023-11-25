namespace Common;
public class BlockingQueue<T> : IDisposable
{
    private readonly Queue<T> _queue = new Queue<T>();
    private readonly int _maxSize;
    private bool _isClosing;
    private bool _disposedValue;

    public BlockingQueue(int maxSize) { _maxSize = maxSize; }

    public void Enqueue(T item)
    {
        lock (_queue)
        {
            while (_queue.Count >= _maxSize)
            {
                Monitor.Wait(_queue);
            }
            _queue.Enqueue(item);
            if (_queue.Count == 1)
            {
                // wake up any blocked dequeue
                Monitor.PulseAll(_queue);
            }
        }
    }

    public T Dequeue()
    {
        lock (_queue)
        {
            while (_queue.Count == 0)
            {
                Monitor.Wait(_queue);
            }
            T item = _queue.Dequeue();
            if (_queue.Count == _maxSize - 1)
            {
                // wake up any blocked enqueue
                Monitor.PulseAll(_queue);
            }
            return item;
        }
    }

    public void Close()
    {
        lock (_queue)
        {
            _isClosing = true;
            Monitor.PulseAll(_queue);
        }
    }

    public bool TryDequeue(out T value)
    {
        lock (_queue)
        {
            while (_queue.Count == 0)
            {
                if (_isClosing)
                {
                    value = default;
                    return false;
                }
                Monitor.Wait(_queue);
            }
            value = _queue.Dequeue();
            if (_queue.Count == _maxSize - 1)
            {
                // wake up any blocked enqueue
                Monitor.PulseAll(_queue);
            }
            return true;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Close();
                _queue.Clear();
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}