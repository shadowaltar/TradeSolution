using TradeCommon.Essentials;
using TradeCommon.Essentials.Corporates;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;
public interface IHistoricalMarketDataService
{
    OhlcPrice Get(Security security, IntervalType intervalType, DateTime at);
    List<OhlcPrice> Get(Security security, IntervalType intervalType, DateTime start, DateTime end);
    List<Tick> GetTicks(Security security, DateTime start, DateTime end);
    
}
