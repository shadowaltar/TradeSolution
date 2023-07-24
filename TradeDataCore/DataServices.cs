using TradeCommon.Essentials;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeDataCore.MarketData;
using TradeDataCore.StaticData;

namespace TradeDataCore;
public class DataServices : IDataServices
{
    public DataServices(IHistoricalMarketDataService historicalMarketDataService,
                        IRealTimeMarketDataService realTimeMarketDataService,
                        IFinancialStatsDataService financialStatsDataService)
    {
        HistoricalMarketData = historicalMarketDataService;
        RealTimeMarketData = realTimeMarketDataService;
        FinancialStatsData = financialStatsDataService;
    }

    public IHistoricalMarketDataService HistoricalMarketData { get; }
    public IRealTimeMarketDataService RealTimeMarketData { get; }
    public IFinancialStatsDataService FinancialStatsData { get; }

    public OhlcPrice GetOhlcPrice(Security security, IntervalType type, DateTime at)
    {
        throw new NotImplementedException();
    }

    public List<OhlcPrice> GetOhlcPrices(Security security, IntervalType type, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public List<OhlcPrice> GetOhlcPrices(Security security, IntervalType type, DateTime end, int lookBackPeriod)
    {
        throw new NotImplementedException();
    }

    public double GetFinancialStat(Security security, FinancialStatType type, DateTime at)
    {
        throw new NotImplementedException();
    }

    public List<double> GetFinancialStats(Security security, FinancialStatType type, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public List<double> GetFinancialStats(Security security, FinancialStatType type, DateTime end, int lookBackPeriod)
    {
        throw new NotImplementedException();
    }

    public List<(DateTime, double)> GetTimedFinancialStats(Security security, FinancialStatType type, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public List<(DateTime, double)> GetTimedFinancialStats(Security security, FinancialStatType type, DateTime end, int lookBackPeriod)
    {
        throw new NotImplementedException();
    }
}
