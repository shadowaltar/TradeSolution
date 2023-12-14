using Common;
using log4net;
using TradeCommon.Database;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Runtime;

namespace TradeLogicCore.Algorithms;

public class OrderBookCache
{
    private static readonly ILog _log = Logger.New();

    private readonly IStorage _storage;
    private readonly string _orderBookTableName;

    private readonly Queue<ExtendedOrderBook> _cache = new();
    private readonly object _locker = new();

    private bool _shallStopAfterAnotherFlush;

    public int CacheSize { get; set; } = 50;

    public bool IsRecording { get; private set; } = false;

    public OrderBookCache(IStorage storage, int securityId)
    {
        _storage = storage;
        var table = DatabaseNames.OrderBookTableNameCache.ThreadSafeGet(securityId);
        if (table.IsBlank())
            throw Exceptions.Impossible("Order Book table must be prepared properly.");
        _orderBookTableName = table;
    }

    public void Add(ExtendedOrderBook book)
    {
        lock (_locker)
        {
            _cache.Enqueue(book);

            if (_cache.Count <= CacheSize)
                return;

            var obj = _cache.Dequeue();
            if (!IsRecording)
                return;

            // becomes batch mode
            var clonedCache = new List<ExtendedOrderBook>();
            foreach (var orderBook in _cache)
            {
                var clone = orderBook.DeepClone();
                var last = clonedCache.LastOrDefault();
                if (last != null && last.Time >= clone.Time)
                    clone.Time = clone.Time.AddMilliseconds(-1 + (last.Time - clone.Time).TotalMilliseconds);
                clonedCache.Add(clone);
            }
            _cache.Clear();
            AsyncHelper.RunSync(() => _storage.InsertOrderBooks(clonedCache, _orderBookTableName));

            // back to continuous cache mode after one more flushing
            if (_shallStopAfterAnotherFlush)
            {
                _shallStopAfterAnotherFlush = false;
                IsRecording = false;
            }
        }
    }

    /// <summary>
    /// Start to persist the cached items.
    /// 1. flush the cached ones immediately.
    /// 2. turns the continuous cache mode to batch flushing mode.
    /// </summary>
    /// <returns></returns>
    public void StartPersistence()
    {
        _shallStopAfterAnotherFlush = false;
        IsRecording = true;
    }

    public void StopPersistenceAfterAnotherFlush()
    {
        _shallStopAfterAnotherFlush = true;
    }
}
