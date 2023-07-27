﻿using System.Collections.Concurrent;
using TradeCommon.Essentials.Quotes;

namespace Common;

public class MessageBroker<T> : IDisposable
{
    private readonly ConcurrentQueue<T> _queue = new();
    private bool _isRunning = true;
    private bool _disposedValue;

    public event Action<T>? NewItem;

    public void Enqueue(T item)
    {
        _queue.Enqueue(item);
    }

    public void Run()
    {
        Task.Factory.StartNew(t =>
        {
            while (_isRunning)
            {
                var item = Dequeue();
                if (item == null)
                {
                    Thread.Sleep(100);
                    continue;
                }
                NewItem?.Invoke(item);
            }
        }, TaskCreationOptions.LongRunning, CancellationToken.None);
    }

    public T? Dequeue()
    {
        if (_queue.TryDequeue(out var item))
            return item;
        return default;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _isRunning = false;
                NewItem = null;
            }

            _queue.Clear();
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
