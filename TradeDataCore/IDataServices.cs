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

    IRealTimeMarketDataService RealTimeMarketData { get; }

    IFinancialStatsDataService FinancialStatsData { get; }

    OhlcPrice GetOhlcPrice(Security security, IntervalType type, DateTime at);
    List<OhlcPrice> GetOhlcPrices(Security security, IntervalType type, DateTime start, DateTime end);
    List<OhlcPrice> GetOhlcPrices(Security security, IntervalType type, DateTime end, int lookBackPeriod);

    double GetFinancialStat(Security security, FinancialStatType type, DateTime at);
    List<double> GetFinancialStats(Security security, FinancialStatType type, DateTime start, DateTime end);
    List<double> GetFinancialStats(Security security, FinancialStatType type, DateTime end, int lookBackPeriod);
    List<(DateTime, double)> GetTimedFinancialStats(Security security, FinancialStatType type, DateTime start, DateTime end);
    List<(DateTime, double)> GetTimedFinancialStats(Security security, FinancialStatType type, DateTime end, int lookBackPeriod);
}