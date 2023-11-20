using Common;
using TradeCommon.Database;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Runtime;

namespace TradeLogicCore.Algorithms;
public class OrderBookCache
{
    private readonly IStorage _storage;
    private readonly string _orderBookTableName;
    private readonly object _locker = new();

    private bool _shallStopAfterAnotherFlush;

    public int CacheSize { get; set; } = 20;

    public Queue<ExtendedOrderBook> Cache { get; } = new();

    public bool IsRecording { get; private set; } = false;

    public OrderBookCache(IStorage storage, int securityId)
    {
        _storage = storage;
        var table = DatabaseNames.OrderBookTableNameCache.ThreadSafeGet(securityId);
        if (table.IsBlank())
            throw Exceptions.Impossible("Order Book table must be prepared properly.");
        _orderBookTableName = table;
    }

    public async Task<ExtendedOrderBook?> Add(ExtendedOrderBook book)
    {
        ExtendedOrderBook? obj = null;
        List<ExtendedOrderBook>? clonedCache = null;
        lock (_locker)
        {
            Cache.Enqueue(book);
            if (Cache.Count > CacheSize)
            {
                obj = Cache.Dequeue();
                if (IsRecording) // becomes batch mode
                    clonedCache = CloneAndClearCache();
            }
        }
        if (clonedCache != null && IsRecording)
        {
            await Flush(clonedCache);

            // back to continuous cache mode after one more flushing
            if (_shallStopAfterAnotherFlush)
            {
                _shallStopAfterAnotherFlush = false;
                IsRecording = false;
            }
        }
        return obj;
    }

    /// <summary>
    /// Start to persist the cached items.
    /// 1. flush the cached ones immediately.
    /// 2. turns the continuous cache mode to batch flushing mode.
    /// </summary>
    /// <returns></returns>
    public async Task StartPersistence()
    {
        _shallStopAfterAnotherFlush = false;
        IsRecording = true;
        List<ExtendedOrderBook> cloned;
        lock (_locker)
            cloned = CloneAndClearCache();
        await Flush(cloned);
    }

    public void StopPersistenceAfterAnotherFlush()
    {
        _shallStopAfterAnotherFlush = true;
    }

    private List<ExtendedOrderBook> CloneAndClearCache()
    {
        var clonedCache = new List<ExtendedOrderBook>();
        lock (_locker)
        {
            foreach (var orderBook in Cache)
            {
                var clone = orderBook.DeepClone();
                clonedCache.Add(clone);
            }
            Cache.Clear();
        }
        return clonedCache;
    }

    private async Task Flush(List<ExtendedOrderBook> clonedCache)
    {
        await _storage.InsertOrderBooks(clonedCache, _orderBookTableName); // fire and forget
    }
}
