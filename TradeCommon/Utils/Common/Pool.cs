using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Utils.Common;
public class Pool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _objects = new();

    public T Get() => _objects.TryTake(out var item) ? item : new();

    public void Return(T item) => _objects.Add(item);
    public int Count => _objects.Count;
}
