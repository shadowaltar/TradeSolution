using Common;
using System.Data;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Database;
using TradeDataCore.MarketData;
using TradeDataCore.StaticData;
using TradeCommon.Constants;

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

    public Task<OhlcPrice> GetOhlcPrice(Security security, IntervalType type, DateTime at)
    {
        throw new NotImplementedException();
    }

    public Task<List<OhlcPrice>> GetOhlcPrices(Security security, IntervalType type, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public async Task<List<OhlcPrice>> GetOhlcPrices(Security security, IntervalType interval, DateTime end, int lookBackPeriod)
    {
        var securityType = SecurityTypeConverter.Parse(security.Type);
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        // find out the exact start time
        // roughly -x biz days:
        DateTime roughStart;
        if (interval == IntervalType.OneDay)
            roughStart = end.AddBusinessDays(-lookBackPeriod);

        var prices = await Storage.ReadPrices(security.Id, interval, securityType, end, lookBackPeriod);

        return prices;
    }

    public async Task<Dictionary<int, List<DateTime>>> GetSecurityIdToPriceTimes(Security security, IntervalType interval)
    {
        var securityType = SecurityTypeConverter.Parse(security.Type);
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        var dt = await Storage.Query($"SELECT SecurityId, StartTime FROM {tableName} WHERE SecurityId = {security.Id}",
            DatabaseNames.MarketData,
            TypeCode.Int32, TypeCode.DateTime);

        var results = new Dictionary<int, List<DateTime>>();
        foreach (DataRow row in dt.Rows)
        {
            var id = (int)row["SecurityId"];
            var dateTimes = results.GetOrCreate(security.Id);
            dateTimes.Add((DateTime)row["StartTime"]);
        }
        return results;
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
