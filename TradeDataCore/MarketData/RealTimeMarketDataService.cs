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

namespace TradeDataCore.MarketData;
public class RealTimeMarketDataService : IMarketDataService, IDisposable
{
    private readonly IExternalQuotationManagement _external;
    private readonly IHistoricalMarketDataService _historicalMarketDataService;

    public IExternalQuotationManagement External => _external;

    public event Action<int, OhlcPrice>? NextOhlc;
    public event Action<int, Tick>? NextTick;
    public event Action<int>? HistoricalPriceStopped;

    private Dictionary<int, int> _realTimeSubscriptionCounters = new();
    private Dictionary<int, int> _historicalSubscriptionCounters = new();

    public RealTimeMarketDataService(IExternalQuotationManagement external,
        IHistoricalMarketDataService historicalMarketDataService)
    {
        _external = external;
        _historicalMarketDataService = historicalMarketDataService;
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
            return new ExternalConnectionState
            {
                Action = ExternalActionType.Subscribe,
                StatusCode = nameof(StatusCodes.InvalidArgument),
                ExternalPartyId = security.Exchange,
                Description = errorDescription,
                Type = SubscriptionType.MarketData,
                UniqueConnectionId = "",
            };
        }

        if (Conditions.AllNotNull(start, end) && end < DateTime.UtcNow && start < end)
        {
            if (!_historicalMarketDataService.HasSubscription)
            {
                await Task.Run(async () =>
                {
                    LockAndCount(ref _historicalSubscriptionCounters, security.Id);

                    var priceCount = await _historicalMarketDataService.Subscribe(security, interval, start.Value, end.Value, OnNextHistoricalPrice);
                    HistoricalPriceStopped?.Invoke(priceCount);
                });
            }
            return new ExternalConnectionState
            {
                Action = ExternalActionType.Subscribe,
                StatusCode = nameof(StatusCodes.SubscriptionOk),
                ExternalPartyId = security.Exchange,
                Description = $"Subscribed historical data from {start.Value:yyyyMMdd-HHmmss} to {end.Value:yyyyMMdd-HHmmss}",
                Type = SubscriptionType.HistoricalMarketData,
                UniqueConnectionId = "",
            };
        }
        if (Conditions.AllNull(start, end))
        {
            LockAndCount(ref _realTimeSubscriptionCounters, security.Id);

            return await _external.SubscribeOhlc(security);
        }
        throw new InvalidOperationException("Unexpected time range condition.");
    }

    private int LockAndCount(ref Dictionary<int, int> counters, int id)
    {
        var newCount = 0;
        lock (counters)
        {
            var count = counters[id];
            counters[id] = count + 1;
            newCount = count + 1;
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
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeAllTicks()
    {
        throw new NotImplementedException();
    }

    public async Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType interval)
    {
        LockAndCount(ref _realTimeSubscriptionCounters, security.Id,)
        return await _external.UnsubscribeOhlc(security);
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
