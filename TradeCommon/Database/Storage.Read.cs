using Common;
using System.Data;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeDataCore.Essentials;

namespace TradeCommon.Database;
public partial class Storage
{
    public static async Task<User?> ReadUser(string userName = "", string email = "")
    {
        var un = userName.ToLowerInvariant().Trim();
        var em = email.ToLowerInvariant().Trim();
        if (un.IsBlank() && em.IsBlank())
        {
            return null;
        }
        if (!un.IsBlank() && !em.IsBlank())
        {
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
    {nameWhereClause}{emailWhereClause}
";
        using var connection = await Connect(DatabaseNames.StaticData);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Name", un);
        command.Parameters.AddWithValue("$Email", em);

        using var r = await command.ExecuteReaderAsync();
        using var sqlHelper = new SqlReader<User>(r);
        while (await r.ReadAsync())
        {
            var user = sqlHelper.Read();
            _log.Info($"Read user with name {un} or email {em} from {DatabaseNames.UserTable} table in {DatabaseNames.StaticData}.");
            return user;
        }
        return null;
    }

    public static async Task<Account?> ReadAccount(string accountName)
    {
        var sqlPart = SqlReader<Account>.GetSelectClause();
        var tableName = DatabaseNames.AccountTable;
        var dbName = DatabaseNames.StaticData;
        var sql =
@$"
{sqlPart} FROM {tableName} WHERE Account = $Account
";
        var account = await ReadOne<Account>(sql, tableName, dbName, ("$Account", accountName));
        if (account != null)
            _log.Info($"Read account with name {accountName} from {tableName} table in {dbName}.");
        return account;
    }

    public static async Task<List<Balance>> ReadBalances(int accountId)
    {
        var sqlPart = SqlReader<Balance>.GetSelectClause();
        var tableName = DatabaseNames.BalanceTable;
        var dbName = DatabaseNames.StaticData;
        var sql =
@$"
{sqlPart} FROM {tableName} WHERE AccountId = $AccountId
";
        var results = await ReadMany<Balance>(sql, tableName, dbName, ("$AccountId", accountId));
        _log.Info($"Read balances with account id {accountId} from {tableName} table in {dbName}.");
        return results;
    }

    public static async Task<List<Order>> ReadOpenOrders(SecurityType securityType)
    {
        var sqlPart = SqlReader<Order>.GetSelectClause();
        var tableName = DatabaseNames.GetOrderTableName(securityType);
        var dbName = DatabaseNames.ExecutionData;
        var sql =
@$"
{sqlPart} FROM {tableName} WHERE Status = 'LIVE'
";
        var results = await ReadMany<Order>(sql, tableName, dbName);
        _log.Info($"Read open orders with security type {securityType} from {tableName} table in {dbName}.");
        return results;
    }

    public static async Task<Security?> ReadSecurity(string exchange, string code, SecurityType type)
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
            security.FxInfo = ReadFxSecurityInfo(sqlHelper);
            _log.Info($"Read security with code {code} and exchange {exchange} from {DatabaseNames.StockDefinitionTable} table in {DatabaseNames.StaticData}.");
            return security;
        }
        return null;
    }

    public static async Task<List<Security>> ReadSecurities(string exchange, List<int> ids)
    {
        var results = new List<Security>();
        foreach (var type in Enum.GetValues<SecurityType>())
        {
            if (type == SecurityType.Unknown) continue;
            var partialResults = await ReadSecurities(exchange, type, ids);
            if (partialResults == null)
                continue;
            results.AddRange(partialResults);
        }
        return results;
    }

    public static async Task<List<Security>?> ReadSecurities(string exchange, SecurityType type, List<int>? ids = null)
    {
        var tableName = DatabaseNames.GetDefinitionTableName(type);
        if (tableName.IsBlank())
            return null;
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        exchange = exchange.ToUpperInvariant();
        var idClause = ids == null
            ? ""
            : ids.Count == 1
            ? $"Id = {ids[0]} AND"
            : $"Id IN ({string.Join(',', ids)}) AND";
        string sql;
        if (type == SecurityType.Equity)
        {
            sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,Cusip,Isin,YahooTicker,IsShortable
FROM {tableName}
WHERE
    {idClause}
    IsEnabled = true AND
    LocalEndDate > $LocalEndDate AND
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
    LocalEndDate > $LocalEndDate AND
    Exchange = $Exchange
"
                : throw new NotImplementedException();
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
            security.FxInfo = ReadFxSecurityInfo(sqlHelper);
            results.Add(security);
        }
        _log.Info($"Read {results.Count} entries from {tableName} table in {DatabaseNames.StaticData}.");
        return results;
    }

    private static FxSecurityInfo? ReadFxSecurityInfo(SqlReader<Security> sqlHelper)
    {
        var baseCcy = sqlHelper.GetOrDefault<string>("BaseCurrency");
        var quoteCcy = sqlHelper.GetOrDefault<string>("QuoteCurrency");
        var isMarginTradingAllowed = sqlHelper.GetOrDefault<bool>("IsMarginTradingAllowed");
        var maxLotSize = sqlHelper.GetOrDefault<double?>("MaxLotSize");
        var minNotional = sqlHelper.GetOrDefault<double?>("MinNotional");
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

        var results = await ReadMany<FinancialStat>(sql, DatabaseNames.FinancialStatsTable, DatabaseNames.StaticData, ("$SecurityId", secId));
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


    public static async IAsyncEnumerable<OhlcPrice> ReadPricesAsync(int securityId, IntervalType interval, SecurityType securityType, DateTime start, DateTime? end = null, int priceDecimalPoints = 16)
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
                O: decimal.Round(r.GetDecimal("Open"), priceDecimalPoints),
                H: decimal.Round(r.GetDecimal("High"), priceDecimalPoints),
                L: decimal.Round(r.GetDecimal("Low"), priceDecimalPoints),
                C: close,
                AC: decimal.Round(r.SafeGetDecimal("AdjClose", close), priceDecimalPoints),
                V: decimal.Round(r.GetDecimal("Volume"), priceDecimalPoints),
                T: r.GetDateTime("StartTime")
            );
            count++;
            yield return price;
        }
        _log.Info($"Read {count} entries from {tableName} table in {DatabaseNames.MarketData}.");
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
            if (price.T.Year == 2023 && price.T.Month == 1)
            {

            }
            list.Add(price);
        }
        _log.Info($"Read {results.Count} entries from {tableName} table in {DatabaseNames.MarketData}.");
        return results;
    }

    private static async Task<List<T>> ReadMany<T>(string sql, string tableName, string databaseName, params (string, object)[] parameters) where T : new()
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
        var results = new List<T>();
        while (await r.ReadAsync())
        {
            var t = sqlHelper.Read();
            results.Add(t);
        }
        return results;
    }

    private static async Task<T?> ReadOne<T>(string sql, string tableName, string databaseName, params (string, object)[] parameters) where T : new()
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
