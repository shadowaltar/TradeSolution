using Common;
using Microsoft.Data.Sqlite;
using System.Data;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeDataCore.Essentials;

namespace TradeCommon.Database;
public partial class Storage
{
    public static async Task<Security> ReadSecurity(string exchange, string code, SecurityType type)
    {
        var tableName = DatabaseNames.GetDefinitionTableName(type);
        string sql;
        if (type == SecurityType.Equity)
        {
            sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,Cusip,Isin,YahooTicker,IsShortable
FROM {tableName}
WHERE
    Code = $Code AND
    Exchange = $Exchange
";
            if (type == SecurityType.Equity)
                sql += $" AND Type IN ('{string.Join("','", SecurityTypes.StockTypes)}')";
        }
        else if (type == SecurityType.Fx)
        {
            sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,BaseCurrency,QuoteCurrency
FROM {tableName}
WHERE
    Code = $Code AND
    Exchange = $Exchange
";
        }
        else
        {
            throw new NotImplementedException();
        }

        using var connection = await Connect(DatabaseNames.StaticData);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Code", code.ToUpperInvariant());
        command.Parameters.AddWithValue("$Exchange", exchange.ToUpperInvariant());

        using var r = await command.ExecuteReaderAsync();
        using var sqlHelper = new SqlReader<Security>(r);
        while (await r.ReadAsync())
        {
            var security = sqlHelper.Read();
            var baseCcy = sqlHelper.GetOrDefault<string>("BaseCurrency");
            var quoteCcy = sqlHelper.GetOrDefault<string>("QuoteCurrency");
            if (baseCcy != null && quoteCcy != null)
            {
                security.FxInfo = new FxSecurityInfo
                {
                    BaseCurrency = baseCcy,
                    QuoteCurrency = quoteCcy
                };
            }
            _log.Info($"Read security with code {code} and exchange {exchange} from {DatabaseNames.StockDefinitionTable} table in {DatabaseNames.StaticData}.");
            return security;
        }
        return null;
    }

    public static async Task<List<Security>> ReadSecurities(string exchange, SecurityType type)
    {
        var tableName = DatabaseNames.GetDefinitionTableName(type);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        exchange = exchange.ToUpperInvariant();
        string sql;
        if (type == SecurityType.Equity)
        {
            sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,Cusip,Isin,YahooTicker,IsShortable
FROM {tableName}
WHERE
    IsEnabled = true AND
    LocalEndDate > $LocalEndDate AND
    Exchange = $Exchange
";
            if (type == SecurityType.Equity)
                sql += $" AND Type IN ('{string.Join("','", SecurityTypes.StockTypes)}')";
        }
        else if (type == SecurityType.Fx)
        {
            sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,BaseCurrency,QuoteCurrency
FROM {tableName}
WHERE
    IsEnabled = true AND
    LocalEndDate > $LocalEndDate AND
    Exchange = $Exchange
";
        }
        else
        {
            throw new NotImplementedException();
        }

        using var connection = await Connect(DatabaseNames.StaticData);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$LocalEndDate", now);
        command.Parameters.AddWithValue("$Exchange", exchange);
        command.Parameters.AddWithValue("$Type", type);

        using var r = await command.ExecuteReaderAsync();
        using var sqlHelper = new SqlReader<Security>(r);
        var results = new List<Security>();
        while (await r.ReadAsync())
        {
            var security = sqlHelper.Read();
            var baseCcy = sqlHelper.GetOrDefault<string>("BaseCurrency");
            var quoteCcy = sqlHelper.GetOrDefault<string>("QuoteCurrency");
            if (baseCcy != null && quoteCcy != null)
            {
                security.FxInfo = new FxSecurityInfo
                {
                    BaseCurrency = baseCcy,
                    QuoteCurrency = quoteCcy
                };
            }
            results.Add(security);
        }
        _log.Info($"Read {results.Count} entries from {tableName} table in {DatabaseNames.StaticData}.");
        return results;
    }

    public static async Task<List<FinancialStat>> ReadFinancialStats()
    {
        string sql =
@$"
SELECT SecurityId,MarketCap
FROM {DatabaseNames.FinancialStatsTable}
";
        using var connection = await Connect(DatabaseNames.StaticData);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var r = await command.ExecuteReaderAsync();
        using var sqlHelper = new SqlReader<FinancialStat>(r);
        var results = new List<FinancialStat>();
        while (await r.ReadAsync())
        {
            var stats = sqlHelper.Read();
            results.Add(stats);
        }
        _log.Info($"Read {results.Count} entries from {DatabaseNames.FinancialStatsTable} table in {DatabaseNames.StaticData}.");
        return results;
    }

    public static async Task<List<FinancialStat>> ReadFinancialStats(int secId)
    {
        string sql =
@$"
SELECT SecurityId,MarketCap
FROM {DatabaseNames.FinancialStatsTable}
WHERE SecurityId = $SecurityId
";
        using var connection = await Connect(DatabaseNames.StaticData);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("$SecurityId", SqliteType.Text).Value = secId;
        using var r = await command.ExecuteReaderAsync();
        using var sqlHelper = new SqlReader<FinancialStat>(r);
        var results = new List<FinancialStat>();
        while (await r.ReadAsync())
        {
            var stats = sqlHelper.Read();
            results.Add(stats);
        }
        _log.Info($"Read {results.Count} entries from {DatabaseNames.FinancialStatsTable} table in {DatabaseNames.StaticData}.");
        return results;
    }

    public static async Task<List<OhlcPrice>> ReadPrices(int securityId, IntervalType interval, SecurityType securityType, DateTime start, DateTime? end = null, int priceDecimalPoints = 16)
    {
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        var dailyPriceSpecificColumn = securityType == SecurityType.Equity && interval == IntervalType.OneDay ? "AdjClose," : "";
        string sql =
@$"
SELECT SecurityId, Open, High, Low, Close, {dailyPriceSpecificColumn} Volume, StartTime
FROM {tableName}
WHERE
    SecurityId = $SecurityId AND
    StartTime > $StartTime
";
        if (end != null)
            sql += $" AND StartTime <= $EndTime";

        using var connection = await Connect(DatabaseNames.MarketData);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$SecurityId", securityId);
        command.Parameters.AddWithValue("$StartTime", start);
        command.Parameters.AddWithValue("$EndTime", end);

        using var r = await command.ExecuteReaderAsync();
        var results = new List<OhlcPrice>();
        while (await r.ReadAsync())
        {
            var close = decimal.Round(r.GetDecimal("Close"), priceDecimalPoints);
            var price = new OhlcPrice
            (
                O: decimal.Round(r.GetDecimal("Open"), priceDecimalPoints),
                H: decimal.Round(r.GetDecimal("High"), priceDecimalPoints),
                L: decimal.Round(r.GetDecimal("Low"), priceDecimalPoints),
                C: close,
                AC: decimal.Round(r.SafeGetDecimal("AdjClose", close), priceDecimalPoints),
                V: decimal.Round(r.GetDecimal("Volume"), priceDecimalPoints),
                T: r.GetDateTime("StartTime")
            );
            results.Add(price);
        }
        if (results.Count == 0)
        {
            var x = command.PrintActualSql();
        }
        _log.Info($"Read {results.Count} entries from {tableName} table in {DatabaseNames.MarketData}.");
        return results;
    }

    public static async Task<List<OhlcPrice>> ReadPrices(int securityId, IntervalType interval, SecurityType securityType, DateTime end, int entryCount, int priceDecimalPoints = 16)
    {
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        var dailyPriceSpecificColumn = securityType == SecurityType.Equity && interval == IntervalType.OneDay ? "AdjClose," : "";
        string sql =
@$"
SELECT SecurityId, Open, High, Low, Close, {dailyPriceSpecificColumn} Volume, StartTime
FROM {tableName}
WHERE
    SecurityId = $SecurityId AND
    StartTime <= $StartTime
LIMIT $EntryCount
";

        using var connection = await Connect(DatabaseNames.MarketData);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$SecurityId", securityId);
        command.Parameters.AddWithValue("$StartTime", end);
        command.Parameters.AddWithValue("$EntryCount", entryCount);

        using var r = await command.ExecuteReaderAsync();
        var results = new List<OhlcPrice>();
        while (await r.ReadAsync())
        {
            var close = decimal.Round(r.GetDecimal("Close"), priceDecimalPoints);
            var price = new OhlcPrice
            (
                O: decimal.Round(r.GetDecimal("Open"), priceDecimalPoints),
                H: decimal.Round(r.GetDecimal("High"), priceDecimalPoints),
                L: decimal.Round(r.GetDecimal("Low"), priceDecimalPoints),
                C: close,
                AC: decimal.Round(r.SafeGetDecimal("AdjClose", close), priceDecimalPoints),
                V: decimal.Round(r.GetDecimal("Volume"), priceDecimalPoints),
                T: r.GetDateTime("StartTime")
            );
            results.Add(price);
        }
        _log.Info($"Read {results.Count} entries from {tableName} table in {DatabaseNames.MarketData}.");
        return results;
    }

    public static async Task<Dictionary<int, List<ExtendedOhlcPrice>>> ReadAllPrices(List<Security> securities, IntervalType interval, SecurityType securityType, TimeRangeType range)
    {
        if (securities.Count == 0)
            return new();

        var now = DateTime.Today;
        var intervalStr = IntervalTypeConverter.ToIntervalString(interval);
        var start = TimeRangeTypeConverter.ConvertTimeSpan(range, OperatorType.Minus)(now);
        var dailyPriceSpecificColumn = securityType == SecurityType.Equity && interval == IntervalType.OneDay ? "AdjClose," : "";
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        string sql =
@$"
SELECT SecurityId, Open, High, Low, Close, {dailyPriceSpecificColumn} Volume, StartTime
FROM {tableName}
WHERE
    StartTime > $StartTime AND
    SecurityId IN 
";
        var ids = securities.Select((s, i) => (id: s.Id, param: $"$Id{i}")).ToArray();
        var securityMap = securities.ToDictionary(s => s.Id, s => s);
        sql = sql + "(" + string.Join(",", ids.Select(p => p.param)) + ")";
        using var connection = await Connect(DatabaseNames.MarketData);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$StartTime", start);

        for (int i = 0; i < ids.Length; i++)
        {
            command.Parameters.AddWithValue(ids[i].param, ids[i].id);
        }
        using var r = await command.ExecuteReaderAsync();

        var results = new Dictionary<int, List<ExtendedOhlcPrice>>();
        while (await r.ReadAsync())
        {
            var close = r.GetDecimal("Close");
            var secId = r.GetInt32("SecurityId");
            if (!securityMap.TryGetValue(secId, out var sec))
                continue;
            var list = results.GetOrCreate(secId);
            var price = new ExtendedOhlcPrice
            (
                Id: sec.Code,
                Ex: sec.Exchange,
                O: r.GetDecimal("Open"),
                H: r.GetDecimal("High"),
                L: r.GetDecimal("Low"),
                C: close,
                AC: r.SafeGetDecimal("AdjClose", close),
                V: r.GetDecimal("Volume"),
                I: intervalStr,
                T: r.SafeGetString("StartTime").ParseDate("yyyy-MM-dd HH:mm:ss")
            );
            list.Add(price);
        }
        _log.Info($"Read {results.Count} entries from {tableName} table in {DatabaseNames.MarketData}.");
        return results;
    }

    public static async Task<List<Order>> ReadOpenOrders(ExchangeType exchangeType)
    {
        throw new NotImplementedException();
    }
}
