using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;

public class HistoricalMarketDataService : IHistoricalMarketDataService, IPriceProvider
{
    private readonly IStorage _storage;

    public HistoricalMarketDataService(IStorage storage)
    {
        _storage = storage;
    }

    public bool HasSubscription => NextPrice != null;

    public event Action<long, IntervalType, OhlcPrice>? NextPrice;


    public async Task<List<OhlcPrice>> Get(Security security, IntervalType intervalType, DateTime start, DateTime end)
    {
        return await _storage.ReadPrices(security.Id, intervalType, SecurityTypeConverter.Parse(security.Type), start, end, security.PricePrecision);
    }

    public IAsyncEnumerable<OhlcPrice> GetAsync(Security security, IntervalType intervalType, DateTime start, DateTime end)
    {
        return _storage.ReadPricesAsync(security.Id, intervalType, SecurityTypeConverter.Parse(security.Type), start, end, security.PricePrecision);
    }

    public Task StartGet(Security security, IntervalType intervalType, DateTime start, DateTime end)
    {
        var id = security.Id;
        var asyncEnumerable = _storage.ReadPricesAsync(security.Id, intervalType, SecurityTypeConverter.Parse(security.Type), start, end, security.PricePrecision);
        return Task.Run(async () =>
        {
            await foreach (var p in asyncEnumerable)
            {
                NextPrice?.Invoke(id, intervalType, p);
            }
        });
    }

    public async Task<int> Subscribe(Security security, IntervalType interval, DateTime start, DateTime stop, Action<long, IntervalType, OhlcPrice>? callback)
    {
        var count = 0;
        var id = security.Id;
        await foreach (var p in GetAsync(security, interval, start, stop))
        {
            callback?.Invoke(id, interval, p);
            count++;
        }
        return count;
    }

    Task<OhlcPrice> IHistoricalMarketDataService.Get(Security security, IntervalType intervalType, DateTime at)
    {
        throw new NotImplementedException();
    }

    Task<List<Tick>> IHistoricalMarketDataService.GetTicks(Security security, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }
}