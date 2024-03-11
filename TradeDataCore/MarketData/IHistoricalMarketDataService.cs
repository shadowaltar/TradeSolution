using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;
public interface IHistoricalMarketDataService
{
    event Action<long, IntervalType, OhlcPrice>? NextPrice;

    public bool HasSubscription { get; }

    Task<OhlcPrice> Get(Security security, IntervalType intervalType, DateTime at);

    Task<List<OhlcPrice>> Get(Security security, IntervalType intervalType, DateTime start, DateTime end);

    IAsyncEnumerable<OhlcPrice> GetAsync(Security security, IntervalType intervalType, DateTime start, DateTime end);

    Task StartGet(Security security, IntervalType intervalType, DateTime start, DateTime end);

    Task<List<Tick>> GetTicks(Security security, DateTime start, DateTime end);

    Task<int> Subscribe(Security security, IntervalType interval, DateTime start, DateTime stop, Action<long, IntervalType, OhlcPrice>? callback);
}
