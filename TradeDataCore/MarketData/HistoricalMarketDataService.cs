using TradeCommon.Essentials;
using TradeCommon.Essentials.Corporates;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;

public class HistoricalMarketDataService : IHistoricalMarketDataService
{
    public OhlcPrice Get(Security security, IntervalType intervalType, DateTime at)
    {
        throw new NotImplementedException();
    }

    public List<OhlcPrice> Get(Security security, IntervalType intervalType, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public List<Tick> GetTicks(Security security, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }
}