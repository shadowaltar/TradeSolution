using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;

public class HistoricalMarketDataService : IHistoricalMarketDataService
{
    public async Task<List<OhlcPrice>> Get(Security security, IntervalType intervalType, DateTime start, DateTime end)
    {
        return await Storage.ReadPrices(security.Id, intervalType, SecurityTypeConverter.Parse(security.Type), start, end, security.PriceDecimalPoints);
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