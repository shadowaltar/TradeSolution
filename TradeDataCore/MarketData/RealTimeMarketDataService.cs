using Common;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeCommon.Utils.Common;
using TradeDataCore.Instruments;
using static TradeCommon.Utils.Delegates;

namespace TradeDataCore.MarketData;
public class RealTimeMarketDataService : IMarketDataService, IDisposable
{
    private readonly IHistoricalMarketDataService _historicalMarketDataService;
    private readonly ISecurityService _securityService;

    public IExternalQuotationManagement External { get; }

    public event OhlcPriceReceivedCallback? NextOhlc;
    public event TickPriceReceivedCallback? NextTick;
    public event OrderBookReceivedCallback? NextOrderBook;
    public event Action<int>? HistoricalPriceEnd;

    private readonly Dictionary<(int securityId, IntervalType interval), int> _ohlcSubscriptionCounters = new();
    private readonly Dictionary<int, int> _tickSubscriptionCounters = new();

    public RealTimeMarketDataService(IExternalQuotationManagement external,
        IHistoricalMarketDataService historicalMarketDataService,
        ISecurityService securityService)
    {
        External = external;
        _historicalMarketDataService = historicalMarketDataService;
        _securityService = securityService;
        External.NextOhlc -= OnNextOhlc;
        External.NextOhlc += OnNextOhlc;
        External.NextTick -= OnNextTick;
        External.NextTick += OnNextTick;
        External.NextOrderBook -= OnNextOrderBook;
        External.NextOrderBook += OnNextOrderBook;
    }

    private void OnNextOhlc(int securityId, OhlcPrice price, bool isComplete)
    {
        NextOhlc?.Invoke(securityId, price, isComplete);
    }

    private void OnNextTick(ExtendedTick tick)
    {
        NextTick?.Invoke(tick.SecurityId, tick.SecurityCode, tick);
    }

    private void OnNextOrderBook(ExtendedOrderBook orderBook)
    {
        NextOrderBook?.Invoke(orderBook);
    }

    public async Task Initialize()
    {
        await External.Initialize();
    }

    public async Task<Dictionary<string, decimal>?> GetPrices(List<Security> securities)
    {
        var state = await External.GetPrices(securities.Select(s => s.Code).ToArray());
        return state.ResultCode == ResultCode.GetPriceOk ? state.Get<Dictionary<string, decimal>>() : null;
    }

    public async Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType interval, DateTime? start = null, DateTime? end = null)
    {
        var errorDescription = "";
        if (start != null && end != null && start > end)
        {
            errorDescription = "Start time must be smaller than end time";
        }
        if (start != null && end != null && start < end && end > DateTime.UtcNow)
        {
            errorDescription = "If end time is specified it must be smaller than utc now to trigger back-test prices";
        }
        if (!errorDescription.IsBlank())
            return ExternalConnectionStates.SubscribedOhlcFailed(security, errorDescription);

        if (Conditions.AllNotNull(start, end) && end < DateTime.UtcNow && start < end)
        {
            await Task.Run(async () =>
            {
                var count = 0;
                var id = security.Id;
                await foreach (var p in _historicalMarketDataService.GetAsync(security, interval, start.Value, end.Value))
                {
                    OnNextHistoricalPrice(id, interval, p);
                    count++;
                }
                HistoricalPriceEnd?.Invoke(count);
            });
            return ExternalConnectionStates.SubscribedHistoricalOhlcOk(security, start.Value, end.Value);
        }

        if (Conditions.AllNull(start, end))
        {
            var c = CountOhlcSubscription(security.Id, interval, 1);
            if (c == 1)
            {
                // need to subscribe
                return External.SubscribeOhlc(security, interval);
            }
            else
            {
                // already subscribed
                return ExternalConnectionStates.AlreadySubscribedRealTimeOhlc(security, interval);
            }
        }
        throw Exceptions.InvalidTimeRange(start, end);
    }

    /// <summary>
    /// Counts the OHLC price external subscription.
    /// Pass in change == 1 or -1 to increase / decrease subscription counter.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="interval"></param>
    /// <param name="change"></param>
    /// <returns></returns>
    private int CountOhlcSubscription(int id, IntervalType interval, int change)
    {
        var newCount = 0;
        var key = (id, interval);
        lock (_ohlcSubscriptionCounters)
        {
            var count = _ohlcSubscriptionCounters.GetOrCreate(key);
            newCount = count + change;
            if (newCount > 0)
                _ohlcSubscriptionCounters[key] = newCount;
            else
                _ohlcSubscriptionCounters.Remove(key);
        }
        return newCount;
    }

    private void OnNextHistoricalPrice(int securityId, IntervalType interval, OhlcPrice price)
    {
        throw new NotImplementedException();
    }

    public async Task<ExternalConnectionState> SubscribeTick(Security security)
    {
        return External.SubscribeTick(security);
    }

    public async Task<ExternalConnectionState> SubscribeOrderBook(Security security, int? levels = null)
    {
        return External.SubscribeOrderBook(security, 5);
    }

    public async Task<ExternalConnectionState> UnsubscribeAllOhlcs()
    {
        List<(int securityId, IntervalType interval)> keys;
        lock (_ohlcSubscriptionCounters)
        {
            keys = _ohlcSubscriptionCounters.Select(pair => pair.Key).ToList();
        }
        var securities = await _securityService.GetSecurities(keys.Select(k => k.securityId).Distinct().ToList());
        var securityMap = securities.ToDictionary(s => s.Id, s => s);
        var states = new List<ExternalConnectionState>();
        foreach (var (securityId, interval) in keys)
        {
            var security = securityMap[securityId];
            var state = await External.UnsubscribeOhlc(security, interval);
            states.Add(state);
        }
        return ExternalConnectionStates.UnsubscribedMultipleRealTimeOhlc(states);
    }

    public Task<ExternalConnectionState> UnsubscribeAllTicks()
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeOrderBook(Security security)
    {
        throw new NotImplementedException();
    }

    public async Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType interval)
    {
        var count = CountOhlcSubscription(security.Id, interval, -1);
        return count == 0
            ? await External.UnsubscribeOhlc(security, interval)
            : ExternalConnectionStates.StillHasSubscribedRealTimeOhlc(security, interval);
    }

    public Task<ExternalConnectionState> UnsubscribeTick(Security security)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        External.NextOhlc -= OnNextOhlc;
        NextOhlc = null;
    }
}
