using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;
public interface IHistoricalMarketDataService
{
    Task<OhlcPrice> Get(Security security, IntervalType intervalType, DateTime at);
    
    Task<List<OhlcPrice>> Get(Security security, IntervalType intervalType, DateTime start, DateTime end);
    
    IAsyncEnumerable<OhlcPrice> GetAsync(Security security, IntervalType intervalType, DateTime start, DateTime end);
    
    Task StartGet(Security security, IntervalType intervalType, DateTime start, DateTime end);
    
    Task<List<Tick>> GetTicks(Security security, DateTime start, DateTime end);
}
