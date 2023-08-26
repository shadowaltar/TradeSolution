using TradeCommon.Essentials;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeDataCore.MarketData;
using TradeDataCore.StaticData;

namespace TradeDataCore;

public interface IDataServices
{
    IHistoricalMarketDataService HistoricalMarketData { get; }

    IMarketDataService RealTimeMarketData { get; }

    IFinancialStatsDataService FinancialStatsData { get; }

    Task<OhlcPrice> GetOhlcPrice(Security security, IntervalType type, DateTime at);
    Task<List<OhlcPrice>> GetOhlcPrices(Security security, IntervalType type, DateTime start, DateTime end);
    Task<List<OhlcPrice>> GetOhlcPrices(Security security, IntervalType type, DateTime end, int lookBackPeriod);

    double GetFinancialStat(Security security, FinancialStatType type, DateTime at);
    List<double> GetFinancialStats(Security security, FinancialStatType type, DateTime start, DateTime end);
    List<double> GetFinancialStats(Security security, FinancialStatType type, DateTime end, int lookBackPeriod);
    List<(DateTime, double)> GetTimedFinancialStats(Security security, FinancialStatType type, DateTime start, DateTime end);
    List<(DateTime, double)> GetTimedFinancialStats(Security security, FinancialStatType type, DateTime end, int lookBackPeriod);
}