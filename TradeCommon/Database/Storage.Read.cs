using Common;
using Common.Database;
using Microsoft.Data.Sqlite;
using System.Data;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Integrity;
using TradeCommon.Runtime;
using TradeDataCore.Essentials;

namespace TradeCommon.Database;
public partial class Storage
{
    public async Task<User?> ReadUser(string userName, string email, EnvironmentType environment)
    {
        var un = userName.ToLowerInvariant().Trim();
        var em = email.ToLowerInvariant().Trim();
        if (un.IsBlank() && em.IsBlank())
        {
            return null;
        }
        if (!un.IsBlank() && !em.IsBlank())
        {
            // can only use one of the filter criteria
            return null;
        }
        var selectClause = SqlReader<User>.GetSelectClause();
        var nameWhereClause = !un.IsBlank() ? "Name = $Name" : "";
        var emailWhereClause = !em.IsBlank() ? "Email = $Email" : "";
        var tableName = DatabaseNames.UserTable;
        var sql =
@$"
{selectClause}
FROM {tableName}
WHERE
    {nameWhereClause}{emailWhereClause} AND Environment = $Environment";

        return await SqlReader.ReadOne<User>(tableName, DatabaseNames.StaticData, sql,
            ("$Name", un), ("$Email", em), ("$Environment", Environments.ToString(environment)));
    }

    public async Task<Account?> ReadAccount(string accountName, EnvironmentType environment)
    {
        var sqlPart = SqlReader<Account>.GetSelectClause();
        var tableName = DatabaseNames.AccountTable;
        var dbName = DatabaseNames.StaticData;
        var sql =
@$"
{sqlPart} FROM {tableName} WHERE Name = $Name AND Environment = $Environment
";
        return await SqlReader.ReadOne<Account>(tableName, dbName, sql,
            ("$Name", accountName), ("$Environment", Environments.ToString(environment)));
    }

    public async Task<List<Asset>> ReadAssets(int accountId)
    {
        var sqlPart = SqlReader<Asset>.GetSelectClause();
        var tableName = DatabaseNames.BalanceTable;
        var dbName = DatabaseNames.StaticData;
        var sql =
@$"
{sqlPart} FROM {tableName} WHERE AccountId = $AccountId
";
        return await SqlReader.Read<Asset>(tableName, dbName, sql, ("$AccountId", accountId));
    }

    public async Task<List<OpenOrderId>> ReadOpenOrderIds()
    {
        var sqlPart = SqlReader<OpenOrderId>.GetSelectClause();
        var tableName = DatabaseNames.OpenOrderIdTable;
        var dbName = DatabaseNames.ExecutionData;
        var sql = @$"{sqlPart} FROM {tableName}";
        return await SqlReader.Read<OpenOrderId>(tableName, dbName, sql);
    }

    public async Task<List<Order>> ReadOrders(Security security, DateTime start, DateTime end)
    {
        // TODO supports only fx / equity types
        var dbName = DatabaseNames.ExecutionData;
        var sqlPart = SqlReader<Order>.GetSelectClause();
        var secType = SecurityTypeConverter.Parse(security.Type);
        var tableName = DatabaseNames.GetOrderTableName(secType);
        var sql = @$"{sqlPart} FROM {tableName} WHERE SecurityId = {security.Id} AND CreateTime >= $StartTime AND UpdateTime <= $EndTime";
        return await SqlReader.Read<Order>(tableName, dbName, sql, ("$StartTime", start), ("$EndTime", end));
    }

    public async Task<List<Order>> ReadOpenOrders(Security? security = null, SecurityType securityType = SecurityType.Unknown)
    {
        // TODO supports only fx / equity types
        var dbName = DatabaseNames.ExecutionData;
        var sqlPart = SqlReader<Order>.GetSelectClause();
        if (security == null && securityType != SecurityType.Unknown)
        {
            var tableName = DatabaseNames.GetOrderTableName(securityType);
            var sql = @$"{sqlPart} FROM {tableName} WHERE Status = 'LIVE' AND Type = {securityType.ToString().ToUpperInvariant()}";
            return await SqlReader.Read<Order>(tableName, dbName, sql);
        }
        else if (security == null && securityType == SecurityType.Unknown)
        {
            var results = new List<Order>();
            var openOrderIds = await ReadOpenOrderIds();
            var typeGroupedIds = openOrderIds.GroupBy(ooid => ooid.SecurityType);
            foreach (var ooIds in typeGroupedIds)
            {
                securityType = ooIds.Key;
                var tableName = DatabaseNames.GetOrderTableName(securityType);
                var idClause = GetInClause("OrderId", ooIds.Select(i => i.OrderId).ToList(), false);
                var sql = @$"{sqlPart} FROM {tableName} WHERE Status = 'LIVE' AND {idClause}";
                results.AddRange(await SqlReader.Read<Order>(tableName, dbName, sql));
            }
            return results;
        }
        else
        {
            if (security == null) throw new InvalidOperationException("Will never happen.");
            if (SecurityTypeConverter.Parse(security.Type) != securityType)
            {
                return new List<Order>();
            }
            var tableName = DatabaseNames.GetOrderTableName(securityType);
            var sql = @$"{sqlPart} FROM {tableName} WHERE Status = 'LIVE' AND SecurityId = {security.Id}";
            return await SqlReader.Read<Order>(tableName, dbName, sql);
        }
    }

    public async Task<List<Trade>> ReadTrades(Security security, DateTime start, DateTime end)
    {
        var dbName = DatabaseNames.ExecutionData;
        var sqlPart = SqlReader<Trade>.GetSelectClause();
        var secType = SecurityTypeConverter.Parse(security.Type);
        var tableName = DatabaseNames.GetTradeTableName(secType);
        var sql = @$"{sqlPart} FROM {tableName} WHERE SecurityId = $SecurityId AND Time >= $StartTime AND Time <= $EndTime";
        return await SqlReader.Read<Trade>(tableName, dbName, sql, ("$SecurityId", security.Id), ("$StartTime", start), ("$EndTime", end));
    }

    public async Task<List<Trade>> ReadTrades(Security security, long orderId)
    {
        var (_, dbName) = DatabaseNames.GetTableAndDatabaseName<Trade>();
        var tableName = DatabaseNames.GetTradeTableName(security.SecurityType);
        var sqlPart = SqlReader<Trade>.GetSelectClause();
        var sql = @$"{sqlPart} FROM {tableName} WHERE SecurityId = $SecurityId AND OrderId = $OrderId";
        return await SqlReader.Read<Trade>(tableName, dbName, sql, ("$SecurityId", security.Id), ("$OrderId", orderId));
    }

    public async Task<List<Position>> ReadPositions(Account account)
    {
        var (_, dbName) = DatabaseNames.GetTableAndDatabaseName<Position>();
        var results = new List<Position>();
        foreach (var secType in Enum.GetValues<SecurityType>())
        {
            var tableName = DatabaseNames.GetPositionTableName(secType);
            if (tableName.IsBlank()) continue;

            var sqlPart = SqlReader<Position>.GetSelectClause();
            var sql = @$"{sqlPart} FROM {tableName} WHERE AccountId = $AccountId";
            var positions = await SqlReader.Read<Position>(tableName, dbName, sql, ("$AccountId", account.Id));
            results.AddRange(positions);
        }
        return results;
    }

    public async Task<Security?> ReadSecurity(ExchangeType exchange, string code, SecurityType type)
    {
        var tableName = DatabaseNames.GetDefinitionTableName(type);
        if (tableName.IsBlank())
            return null;
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
        else
        {
            sql = type == SecurityType.Fx
                ? @$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,BaseCurrency,QuoteCurrency,IsMarginTradingAllowed,MaxLotSize,MinNotional,PricePrecision,QuantityPrecision
FROM {tableName}
WHERE
    IsEnabled = true AND
    Code = $Code AND
    Exchange = $Exchange
"
                : throw new NotImplementedException();
        }

        SqlReader<Security>? sqlHelper = null;
        return await SqlReader.ReadOne(tableName, DatabaseNames.StaticData, sql, Transform,
            ("$Code", code.ToUpperInvariant()), ("$Exchange", ExchangeTypeConverter.ToString(exchange)));

        Security Transform(SqliteDataReader r)
        {
            sqlHelper ??= new SqlReader<Security>(r);
            var security = sqlHelper.Read();
            security.FxInfo = ReadFxSecurityInfo(sqlHelper);
            return security;
        }
    }

    public async Task<List<Security>> ReadSecurities(List<int>? ids = null)
    {
        var results = new List<Security>();
        foreach (var exchange in Enum.GetValues<ExchangeType>())
        {
            if (exchange.IsUnknown()) continue;
            foreach (var type in Enum.GetValues<SecurityType>())
            {
                if (type == SecurityType.Unknown) continue;
                var partialResults = await ReadSecurities(type, exchange, ids);
                if (partialResults == null)
                    continue;
                results.AddRange(partialResults);
            }
        }
        return results;
    }

    private string GetInClause<T>(string fieldName, List<T>? items, bool withEndingAnd)
    {
        var endingAnd = withEndingAnd ? " AND " : "";
        return items.IsNullOrEmpty()
            ? ""
            : items.Count == 1
            ? $"{fieldName} = {items[0]}{endingAnd}"
            : $"{fieldName} IN ({string.Join(',', items)}){endingAnd}";
    }

    public async Task<List<Security>> ReadSecurities(SecurityType type, ExchangeType exchange, List<int>? ids = null)
    {
        var tableName = DatabaseNames.GetDefinitionTableName(type);
        if (tableName.IsBlank())
            return new List<Security>();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var typeStr = type.ToString().ToUpperInvariant();
        var exchangeStr = exchange.ToString().ToUpperInvariant();
        var idClause = GetInClause("Id", ids, true);
        var exchangeClause = exchange.IsUnknown()
            ? ""
            : "AND Exchange = $Exchange";
        string sql;
        if (type == SecurityType.Equity)
        {
            sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,Cusip,Isin,YahooTicker,IsShortable
FROM {tableName}
WHERE
    {idClause}
    IsEnabled = true
    AND LocalEndDate > $LocalEndDate
    {exchangeClause}
";
            if (type == SecurityType.Equity)
                sql += $" AND Type IN ('{string.Join("','", SecurityTypes.StockTypes)}')";
        }
        else
        {
            sql = type == SecurityType.Fx
                ? @$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,BaseCurrency,QuoteCurrency,IsMarginTradingAllowed,MaxLotSize,MinNotional,PricePrecision,QuantityPrecision
FROM {tableName}
WHERE
    {idClause}
    IsEnabled = true
    AND LocalEndDate > $LocalEndDate
    {exchangeClause}
"
                : throw new NotImplementedException();
        }

        SqlReader<Security>? sqlHelper = null;
        return await SqlReader.Read(tableName, DatabaseNames.StaticData, sql, Read,
            ("$LocalEndDate", now), ("$Exchange", exchangeStr), ("$Type", typeStr));

        Security Read(SqliteDataReader r)
        {
            sqlHelper ??= new SqlReader<Security>(r);
            var security = sqlHelper.Read();
            security.SecurityType = SecurityTypeConverter.Parse(security.Type);
            security.ExchangeType = ExchangeTypeConverter.Parse(security.Exchange);
            security.FxInfo = ReadFxSecurityInfo(sqlHelper);
            return security;
        }
    }

    private FxSecurityInfo? ReadFxSecurityInfo(SqlReader<Security> sqlHelper)
    {
        var baseCcy = sqlHelper.GetOrDefault<string>("BaseCurrency");
        var quoteCcy = sqlHelper.GetOrDefault<string>("QuoteCurrency");
        var isMarginTradingAllowed = sqlHelper.GetOrDefault<bool>("IsMarginTradingAllowed");
        var maxLotSize = sqlHelper.GetOrDefault<decimal?>("MaxLotSize");
        var minNotional = sqlHelper.GetOrDefault<decimal?>("MinNotional");
        if (baseCcy != null && quoteCcy != null)
        {
            var fxInfo = new FxSecurityInfo
            {
                BaseCurrency = baseCcy,
                QuoteCurrency = quoteCcy,
                IsMarginTradingAllowed = isMarginTradingAllowed,
                MaxLotSize = maxLotSize,
                MinNotional = minNotional,
            };
            return fxInfo;
        }
        return null;
    }

    public async Task<List<FinancialStat>> ReadFinancialStats()
    {
        string sql =
@$"
SELECT SecurityId,MarketCap
FROM {DatabaseNames.FinancialStatsTable}
";
        return await SqlReader.Read<FinancialStat>(DatabaseNames.FinancialStatsTable, DatabaseNames.StaticData, sql);
    }

    public async Task<List<FinancialStat>> ReadFinancialStats(int secId)
    {
        string sql =
@$"
SELECT SecurityId,MarketCap
FROM {DatabaseNames.FinancialStatsTable}
WHERE SecurityId = $SecurityId
";
        return await SqlReader.Read<FinancialStat>(DatabaseNames.FinancialStatsTable, DatabaseNames.StaticData, sql, ("$SecurityId", secId));
    }

    public async Task<List<MissingPriceSituation>> ReadDailyMissingPriceSituations(IntervalType interval, SecurityType securityType)
    {
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        var sql = $@"SELECT * FROM (SELECT COUNT(StartTime) as Count, DATE(StartTime) as Date, SecurityId FROM {tableName}
GROUP BY DATE(startTime), SecurityId)";
        return await SqlReader.Read(tableName, DatabaseNames.MarketData, sql, Transform);

        MissingPriceSituation Transform(SqliteDataReader r) => new(r.GetInt32("SecurityId"), r.GetDateTime("Date"), r.GetInt32("Count"), interval);
    }

    public async Task<List<OhlcPrice>> ReadPrices(int securityId, IntervalType interval, SecurityType securityType, DateTime start, DateTime? end = null, int priceDecimalPoints = 16)
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

        var parameters = new (string, object?)[]
        {
            ("$SecurityId", securityId),
            ("$StartTime", start),
            ("$EndTime", end),
        };
        return await SqlReader.Read(tableName, DatabaseNames.MarketData, sql, Transform, parameters);

        OhlcPrice Transform(SqliteDataReader r)
        {
            var close = decimal.Round(r.GetDecimal("Close"), priceDecimalPoints);
            var price = new OhlcPrice
            (
                decimal.Round(r.GetDecimal("Open"), priceDecimalPoints),
                decimal.Round(r.GetDecimal("High"), priceDecimalPoints),
                decimal.Round(r.GetDecimal("Low"), priceDecimalPoints),
                close,
                decimal.Round(r.SafeGetDecimal("AdjClose", close), priceDecimalPoints),
                decimal.Round(r.GetDecimal("Volume"), priceDecimalPoints),
                r.GetDateTime("StartTime")
            );
            return price;
        }
    }

    public async IAsyncEnumerable<OhlcPrice> ReadPricesAsync(int securityId, IntervalType interval, SecurityType securityType, DateTime start, DateTime? end = null, int priceDecimalPoints = 16)
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
        var count = 0;
        while (await r.ReadAsync())
        {
            var close = decimal.Round(r.GetDecimal("Close"), priceDecimalPoints);
            var price = new OhlcPrice
            (
                decimal.Round(r.GetDecimal("Open"), priceDecimalPoints),
                decimal.Round(r.GetDecimal("High"), priceDecimalPoints),
                decimal.Round(r.GetDecimal("Low"), priceDecimalPoints),
                close,
                decimal.Round(r.SafeGetDecimal("AdjClose", close), priceDecimalPoints),
                decimal.Round(r.GetDecimal("Volume"), priceDecimalPoints),
                r.GetDateTime("StartTime")
            );
            count++;
            yield return price;
        }
        _log.Info($"Read {count} entries from {tableName} table in {DatabaseNames.MarketData}.");
    }

    public async Task<List<OhlcPrice>> ReadPrices(int securityId, IntervalType interval, SecurityType securityType, DateTime end, int entryCount, int priceDecimalPoints = 16)
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
                decimal.Round(r.GetDecimal("Open"), priceDecimalPoints),
                decimal.Round(r.GetDecimal("High"), priceDecimalPoints),
                decimal.Round(r.GetDecimal("Low"), priceDecimalPoints),
                close,
                decimal.Round(r.SafeGetDecimal("AdjClose", close), priceDecimalPoints),
                decimal.Round(r.GetDecimal("Volume"), priceDecimalPoints),
                r.GetDateTime("StartTime")
            );
            results.Add(price);
        }
        _log.Info($"Read {results.Count} entries from {tableName} table in {DatabaseNames.MarketData}.");
        return results;
    }

    public async Task<Dictionary<int, List<ExtendedOhlcPrice>>> ReadAllPrices(List<Security> securities, IntervalType interval, SecurityType securityType, TimeRangeType range)
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
                Code: sec.Code,
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

    private async Task<T?> ReadOne<T>(string sql, string tableName, string databaseName, params (string, object)[] parameters) where T : new()
    {
        using var connection = await Connect(DatabaseNames.StaticData);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (key, value) in parameters)
        {
            command.Parameters.AddWithValue(key, value);
        }

        using var r = await command.ExecuteReaderAsync();
        using var sqlHelper = new SqlReader<T>(r);
        T? t = default;
        while (await r.ReadAsync())
        {
            t = sqlHelper.Read();
        }
        return t;
    }
}
