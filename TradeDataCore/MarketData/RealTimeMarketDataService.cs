using BenchmarkDotNet.Running;
using Common;
using System.Collections.Concurrent;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeCommon.Utils.Common;
using TradeDataCore.Instruments;

namespace TradeDataCore.MarketData;
public class RealTimeMarketDataService : IMarketDataService, IDisposable
{
    private readonly IExternalQuotationManagement _external;
    private readonly IHistoricalMarketDataService _historicalMarketDataService;
    private readonly ISecurityService _securityService;

    public IExternalQuotationManagement External => _external;

    public event Action<int, OhlcPrice>? NextOhlc;
    public event Action<int, Tick>? NextTick;
    public event Action<int>? HistoricalPriceStopped;

    private readonly Dictionary<(int securityId, IntervalType interval), int> _ohlcSubscriptionCounters = new();
    private readonly Dictionary<int, int> _tickSubscriptionCounters = new();

    public RealTimeMarketDataService(IExternalQuotationManagement external,
        IHistoricalMarketDataService historicalMarketDataService,
        ISecurityService securityService)
    {
        _external = external;
        _historicalMarketDataService = historicalMarketDataService;
        _securityService = securityService;
        _external.NextOhlc -= OnNextOhlc;
        _external.NextOhlc += OnNextOhlc;
    }

    private void OnNextOhlc(int securityId, OhlcPrice price)
    {
        NextOhlc?.Invoke(securityId, price);
    }

    public async Task Initialize()
    {
        await _external.Initialize();
    }

    public async Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType interval, DateTime? start = null, DateTime? end = null)
    {
        var errorDescription = "";
        var externalNames = MarketDataSources.GetExternalNames(security);
        if (externalNames.IsNullOrEmpty())
        {
            errorDescription = "Unknown combination of security type, sub-type and exchange name; security: " + security.Name;
        }
        if (Conditions.AnyNull(start, end))
        {
            errorDescription = "Must specify both or none of the start / end time.";
        }
        if (start != null && end != null && start > end)
        {
            errorDescription = "Start time must be smaller than end time";
        }
        if (start != null && end != null && start < end && end > DateTime.UtcNow)
        {
            errorDescription = "If end time is specified it must be smaller than utc now to trigger back-test prices";
        }
        if (!errorDescription.IsBlank())
        {
            return ExternalConnectionStates.SubscribedOhlcFailed(security, errorDescription);
        }

        if (Conditions.AllNotNull(start, end) && end < DateTime.UtcNow && start < end)
        {
            //if (!_historicalMarketDataService.HasSubscription)
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
                    HistoricalPriceStopped?.Invoke(count);
                });
            }
            return ExternalConnectionStates.SubscribedHistoricalOhlcOk(security, start.Value, end.Value);
        }
        if (Conditions.AllNull(start, end))
        {
            var c = CountOhlcSubscription(security.Id, interval, 1);
            if (c == 1)
            {
                // need to subscribe
                return await _external.SubscribeOhlc(security, interval);
            }
            else
            {
                // already subscribed
                return ExternalConnectionStates.AlreadySubscribedRealTimeOhlc(security, interval);
            }
        }
        throw new InvalidOperationException("Unexpected time range condition.");
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

    public Task<ExternalConnectionState> SubscribeTick(Security security)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeAllOhlcs()
    {
        List<(int securityId, IntervalType interval)> keys;
        lock (_ohlcSubscriptionCounters)
        {
            keys = _ohlcSubscriptionCounters.Select(pair => pair.Key).ToList();
        }
        _securityService.GetSecurity()
        foreach (var key in keys)
        {
            return await _external.UnsubscribeOhlc(key.securityId, key.interval);
        }
    }

    public Task<ExternalConnectionState> UnsubscribeAllTicks()
    {
        throw new NotImplementedException();
    }

    public async Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType interval)
    {
        var count = CountOhlcSubscription(security.Id, interval, -1);
        if (count == 0)
            return await _external.UnsubscribeOhlc(security, interval);

        return ExternalConnectionStates.StillHasSubscribedRealTimeOhlc(security, interval);
    }

    public Task<ExternalConnectionState> UnsubscribeTick(Security security)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        _external.NextOhlc -= OnNextOhlc;
        NextOhlc = null;
    }
}
