using TradeCommon.Essentials.Corporates;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;

public class HistoricalMarketDataService : IHistoricalMarketDataService
{
    public List<OhlcPrice> Get(Security security, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public List<IStockCorporateAction> GetCorporateActions(Security security, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public List<FinancialStats> GetFundamentals(Security security, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public List<Tick> GetTicks(Security security, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }
}