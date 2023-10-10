using System.Collections.Concurrent;

namespace Common;
public class Pool<T> : IDisposable where T : class, new()
{
    private readonly ConcurrentBag<T> _objects = new();
    private bool _isDisposed = false;
    private int _leasedCount = 0;
    private int _expectedCount = 0;

    /// <summary>
    /// Gets the count of objects being leased.
    /// Not always consistent.
    /// </summary>
    public int LeasedCount => _leasedCount;

    /// <summary>
    /// Gets the count of objects if none are leased.
    /// Not always consistent.
    /// </summary>
    public int ExpectedCount => _expectedCount;

    /// <summary>
    /// Gets the count of objects still in the pool.
    /// Consistent and thread-safe.
    /// </summary>
    public int Count => _objects.Count;

    public Pool(int initialCount = 16)
    {
        if (initialCount < 0) throw new ArgumentException("Must specify a non-negative initial object count.", nameof(initialCount));

        for (int i = 0; i < initialCount; i++)
        {
            _objects.Add(new T());
        }
        _expectedCount = initialCount;
    }

    /// <summary>
    /// Lease an object.
    /// </summary>
    /// <returns></returns>
    public T Lease()
    {
        if (_isDisposed) throw new ObjectDisposedException(GetType().Name);
        if (_objects.TryTake(out var item))
        {
            Interlocked.Increment(ref _leasedCount);
            return item;
        }
        else
        {
            Interlocked.Increment(ref _leasedCount);
            Interlocked.Increment(ref _expectedCount);
            return new();
        }
    }

    /// <summary>
    /// Return an object.
    /// </summary>
    /// <param name="item"></param>
    public void Return(T item)
    {
        if (_isDisposed) throw new ObjectDisposedException(GetType().Name);
        _objects.Add(item);
        Interlocked.Decrement(ref _leasedCount);
    }

    public void Dispose()
    {
        _isDisposed = true;
        _objects.Clear();
    }
}