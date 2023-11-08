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
    public async Task<int> Count<T>(string? tableNameOverride = null, string? whereClause = "") where T : class, new()
    {
        var (t, d) = DatabaseNames.GetTableAndDatabaseName<T>();
        t = tableNameOverride ?? t;
        string sql = whereClause.IsBlank() ?
            $"SELECT COUNT(*) FROM {t}" :
            $"SELECT COUNT(*) FROM {t} WHERE {whereClause}";
        var (r, i) = await TryReadScalar<int>(sql, d);
        return r ? i : 0;
    }

    public async Task<List<T>> Read<T>(string? tableNameOverride = null, string? whereClause = "", params (string key, object? value)[] parameterValues) where T : class, new()
    {
        var (t, d) = DatabaseNames.GetTableAndDatabaseName<T>();
        t = tableNameOverride ?? t;
        return await Read<T>(t, d, whereClause, parameterValues);
    }

    public async Task<List<T>> Read<T>(string tableName, string database, string? whereClause = "", params (string key, object? value)[] parameterValues) where T : new()
    {
        var selectClause = SqlReader<T>.GetSelectClause();
        string sql = whereClause.IsBlank() ?
            $"{selectClause} FROM {tableName}" :
            $"{selectClause} FROM {tableName} WHERE {whereClause}";
        return await SqlReader.ReadMany<T>(tableName, database, _environmentString, sql, parameterValues);
    }

    public async Task<(bool, T)> TryReadScalar<T>(string sql, string database)
    {
        var typeCode = TypeConverter.ToTypeCode(typeof(T));
        var dt = await Query(sql, database, typeCode);
        return dt.Rows.Count == 0 ? ((bool, T))(false, default) : dt.Rows[0][0] is T value ? ((bool, T))(true, value) : ((bool, T))(false, default);
    }

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
            em = ""; // if both exist, only use user name for search
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

        return await SqlReader.ReadOne<User>(tableName, DatabaseNames.StaticData, _environmentString, sql,
            ("$Name", un), ("$Email", em), ("$Environment", Environments.ToString(environment)));
    }

    public async Task<Account?> ReadAccount(string accountName)
    {
        var sqlPart = SqlReader<Account>.GetSelectClause();
        var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Account>();
        var sql = @$"{sqlPart} FROM {tableName} WHERE Name = $Name";
        return await SqlReader.ReadOne<Account>(tableName, dbName, _environmentString, sql, ("$Name", accountName));
    }

    public async Task<List<Asset>> ReadAssets()
    {
        var sqlPart = SqlReader<Asset>.GetSelectClause();
        var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Asset>();
        var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId";
        return await SqlReader.ReadMany<Asset>(tableName, dbName, _environmentString, sql, ("$accountId", AccountId));
    }

    public async Task<List<AssetState>> ReadAssetStates(Security security, DateTime start)
    {
        var sqlPart = SqlReader<AssetState>.GetSelectClause();
        var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<AssetState>();
        var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId AND SecurityId = $Security AND Time >= $Time";
        return await SqlReader.ReadMany<AssetState>(tableName, dbName, _environmentString, sql, ("$accountId", AccountId), ("$Security", security.Id), ("$Time", start));
    }

    public async Task<List<Order>> ReadOrders(Security? security, List<long>? ids, DateTime? start, DateTime? end, params OrderStatus[] orderStatuses)
    {
        var orderIdClause = GetInClause("Id", ids, true, false);
        var orderStatusClause = GetOrderStatusClause(orderStatuses, true, false);
        var securityIdClause = security != null ? $" AND SecurityId = {security.Id}" : "";
        var timeRangeClause = start != null && end != null
            ? $" AND CreateTime >= $StartTime AND UpdateTime <= $EndTime"
            : start == null
            ? $" AND UpdateTime <= $EndTime"
            : end == null
            ? $" AND CreateTime >= $StartTime"
            : "";
        if (security == null)
        {
            var results = new List<Order>();
            foreach (var secType in Consts.SupportedSecurityTypes)
            {
                var partialResults = await ReadOrders(secType, securityIdClause, orderIdClause, orderStatusClause, timeRangeClause, _environmentString, AccountId, start, end);
                results.AddRange(partialResults);
            }
            return results;
        }
        else
        {
            return await ReadOrders(security.SecurityType, securityIdClause, orderIdClause, orderStatusClause, timeRangeClause, _environmentString, AccountId, start, end);
        }
    }

    private static async Task<List<Order>> ReadOrders(SecurityType type, string securityIdClause, string orderIdClause, string orderStatusClause, string timeRangeClause, string env, int accountId, DateTime? start = null, DateTime? end = null)
    {
        var sqlPart = SqlReader<Order>.GetSelectClause();
        var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Order>(type);
        var sql = @$"{sqlPart} FROM {tableName} WHERE AccountId = $AccountId {securityIdClause} {orderIdClause} {timeRangeClause} {orderStatusClause}";
        return await SqlReader.ReadMany<Order>(tableName, dbName, env, sql, ("$AccountId", accountId), ("$StartTime", start ?? DateTime.MinValue), ("$EndTime", end ?? DateTime.MaxValue));
    }

    public async Task<List<OrderState>> ReadOrderStates(Security security, DateTime start, DateTime end)
    {
        var sqlPart = SqlReader<OrderState>.GetSelectClause();
        var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<OrderState>(security.SecurityType);
        var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId AND SecurityId = {security.Id} AND Time >= $StartTime AND Time <= $EndTime";
        return await SqlReader.ReadMany<OrderState>(tableName, dbName, _environmentString, sql, ("$accountId", AccountId), ("$StartTime", start), ("$EndTime", end));
    }

    public async Task<List<Order>> ReadOrderJoinedStates(Security security, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public async Task<Order?> ReadOrderByExternalId(long externalOrderId)
    {
        var sqlPart = SqlReader<Order>.GetSelectClause();
        foreach (var secType in Consts.SupportedSecurityTypes)
        {
            var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Order>(secType);
            var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId AND ExternalOrderId = $ExternalOrderId";
            var order = await SqlReader.ReadOne<Order>(tableName, _environmentString, dbName, sql, ("$accountId", AccountId), ("$ExternalOrderId", externalOrderId));
            if (order != null)
                return order;
        }
        return null;
    }

    public async Task<List<Trade>> ReadTrades(Security security, DateTime start, DateTime end, bool? isOperational = false)
    {
        var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
        var sqlPart = SqlReader<Trade>.GetSelectClause();
        var operationalPart = isOperational == null ? "" : isOperational == true ? "AND IsOperational = 1" : "AND IsOperational = 0";
        var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId AND SecurityId = $SecurityId {operationalPart} AND Time >= $StartTime AND Time <= $EndTime ORDER BY Id";
        return await SqlReader.ReadMany<Trade>(tableName, dbName, _environmentString, sql, ("$accountId", AccountId), ("$SecurityId", security.Id), ("$StartTime", start), ("$EndTime", end));
    }

    public async Task<List<Trade>> ReadTradesByPositionId(Security security, long positionId, OperatorType positionIdComparisonOperator = OperatorType.Equals)
    {
        var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
        var sqlPart = SqlReader<Trade>.GetSelectClause();
        var @operator = OperatorTypeConverter.ConvertToSqlString(positionIdComparisonOperator);
        var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId AND SecurityId = $SecurityId AND IsOperational = 0 AND PositionId {@operator} $PositionId ORDER BY Id";
        return await SqlReader.ReadMany<Trade>(tableName, dbName, _environmentString, sql, ("$accountId", AccountId), ("$SecurityId", security.Id), ("$PositionId", positionId));
    }

    public async Task<List<Trade>> ReadTrades(Security security, List<long> ids)
    {
        var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
        var sqlPart = SqlReader<Trade>.GetSelectClause();
        var inClause = GetInClause("Id", ids, true, false);
        var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId AND SecurityId = $SecurityId AND IsOperational = 0 {inClause}";
        return await SqlReader.ReadMany<Trade>(tableName, dbName, _environmentString, sql, ("$accountId", AccountId), ("$SecurityId", security.Id));
    }

    public async Task<List<Trade>> ReadTradesByOrderId(Security security, long orderId)
    {
        var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
        var sqlPart = SqlReader<Trade>.GetSelectClause();
        var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId AND SecurityId = $SecurityId AND OrderId = $OrderId AND IsOperational = 0";
        return await SqlReader.ReadMany<Trade>(tableName, dbName, _environmentString, sql, ("$accountId", AccountId), ("$SecurityId", security.Id), ("$OrderId", orderId));
    }

    public async Task<Trade?> ReadTradeByExternalId(long externalTradeId)
    {
        var sqlPart = SqlReader<Trade>.GetSelectClause();
        foreach (var secType in Consts.SupportedSecurityTypes)
        {
            var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Trade>(secType);
            var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId AND ExternalOrderId = $ExternalOrderId AND IsOperational = 0";
            var trade = await SqlReader.ReadOne<Trade>(tableName, dbName, _environmentString, sql, ("$accountId", AccountId), ("$ExternalOrderId", externalTradeId));
            if (trade != null)
                return trade;
        }
        return null;
    }

    public async Task<Position?> ReadPosition(Security security, long positionId)
    {
        var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Position>(security.SecurityType);
        var sqlPart = SqlReader<Position>.GetSelectClause();
        var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId AND SecurityId = $SecurityId AND PositionId = $PositionId";
        return await SqlReader.ReadOne<Position>(tableName, dbName, _environmentString, sql, ("$accountId", AccountId), ("$SecurityId", security.Id), ("$PositionId", positionId));
    }

    public async Task<List<Position>> ReadPositions(DateTime start, OpenClose isOpenOrClose)
    {
        var results = new List<Position>();
        foreach (var secType in Consts.SupportedSecurityTypes)
        {
            var (tableName, dbName) = DatabaseNames.GetTableAndDatabaseName<Position>(secType);
            var openClosePart = isOpenOrClose == OpenClose.All ? "" : isOpenOrClose == OpenClose.OpenOnly ? "AND EndTradeId = 0 " : "AND EndTradeId <> 0";
            var sqlPart = SqlReader<Position>.GetSelectClause();
            var sql = @$"{sqlPart} FROM {tableName} WHERE accountId = $accountId AND UpdateTime >= $StartTime {openClosePart}ORDER BY Id";
            var positions = await SqlReader.ReadMany<Position>(tableName, dbName, _environmentString, sql, ("$accountId", AccountId), ("$StartTime", start));
            results.AddRange(positions);
        }
        return results;
    }

    public async Task<Security?> ReadSecurity(ExchangeType exchange, string code, SecurityType type)
    {
        if (type == SecurityType.Unknown)
        {
            foreach (var secType in Consts.SupportedSecurityTypes)
            {
                var s = await ReadSecurity(exchange, code, secType);
                if (s != null) return s;
            }
            return null;
        }

        var tableName = DatabaseNames.GetDefinitionTableName(type);
        if (tableName.IsBlank())
            return null;
        string sql;
        if (type == SecurityType.Equity)
        {
            sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,TickSize,Currency,Cusip,Isin,YahooTicker,IsShortable
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
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,TickSize,BaseCurrency,QuoteCurrency,IsMarginTradingAllowed,MaxLotSize,MinNotional,PricePrecision,QuantityPrecision
FROM {tableName}
WHERE
    IsEnabled = true AND
    Code = $Code AND
    Exchange = $Exchange
"
                : throw new NotImplementedException();
        }

        SqlReader<Security>? sqlHelper = null;
        return await SqlReader.ReadOne(tableName, DatabaseNames.StaticData, _environmentString, sql, Transform,
            ("$Code", code.ToUpperInvariant()), ("$Exchange", ExchangeTypeConverter.ToString(exchange)));

        Security Transform(SqliteDataReader r)
        {
            sqlHelper ??= new SqlReader<Security>(r);
            var security = sqlHelper.Read();
            security.FxInfo = ParseFxSecurityInfo(sqlHelper);
            return security;
        }
    }

    public async Task<List<Security>> ReadSecurities(List<int>? ids = null)
    {
        var results = new List<Security>();
        foreach (var exchange in Enum.GetValues<ExchangeType>())
        {
            if (exchange.IsUnknown()) continue;
            foreach (var secType in Consts.SupportedSecurityTypes)
            {
                var partialResults = await ReadSecurities(secType, exchange, ids);
                if (partialResults == null)
                    continue;
                results.AddRange(partialResults);
            }
        }
        return results;
    }

    public static string GetInClause<T>(string fieldName, List<T>? items, bool withStartingAnd, bool withEndingAnd)
    {
        var endingAnd = withEndingAnd ? " AND " : "";
        var startingAnd = withStartingAnd ? " AND " : "";
        return items.IsNullOrEmpty()
            ? ""
            : items.Count == 1
            ? $"{startingAnd}{fieldName} = {items[0]}{endingAnd}"
            : $"{startingAnd}{fieldName} IN ({string.Join(',', items)}){endingAnd}";
    }

    public static string GetOrderStatusClause(OrderStatus[] statuses, bool withStartingAnd, bool withEndingAnd)
    {
        var endingAnd = withEndingAnd ? " AND " : "";
        var startingAnd = withStartingAnd ? " AND " : "";
        return statuses.IsNullOrEmpty()
            ? ""
            : statuses.Length == 1
            ? $"{startingAnd}Status = {statuses[0]}{endingAnd}"
            : $"{startingAnd}Status IN ({string.Join(',', statuses)}){endingAnd}";
    }

    public async Task<List<Security>> ReadSecurities(SecurityType type, ExchangeType exchange, List<int>? ids = null)
    {
        var tableName = DatabaseNames.GetDefinitionTableName(type);
        if (tableName.IsBlank())
            return new List<Security>();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var typeStr = type.ToString().ToUpperInvariant();
        var exchangeStr = exchange.ToString().ToUpperInvariant();
        var idClause = GetInClause("Id", ids, false, true);
        var exchangeClause = exchange.IsUnknown()
            ? ""
            : "AND Exchange = $Exchange";
        string sql;
        if (type == SecurityType.Equity)
        {
            sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,TickSize,Currency,Cusip,Isin,YahooTicker,IsShortable
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
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,TickSize,BaseCurrency,QuoteCurrency,IsMarginTradingAllowed,MaxLotSize,MinNotional,PricePrecision,QuantityPrecision
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
        return await SqlReader.ReadMany(tableName, DatabaseNames.StaticData, _environmentString, sql, Read,
            ("$LocalEndDate", now), ("$Exchange", exchangeStr), ("$Type", typeStr));

        Security Read(SqliteDataReader r)
        {
            sqlHelper ??= new SqlReader<Security>(r);
            var security = sqlHelper.Read();
            security.SecurityType = SecurityTypeConverter.Parse(security.Type);
            security.ExchangeType = ExchangeTypeConverter.Parse(security.Exchange);
            security.FxInfo = ParseFxSecurityInfo(sqlHelper);
            return security;
        }
    }

    private static FxSecurityInfo? ParseFxSecurityInfo(SqlReader<Security> sqlHelper)
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
        return await SqlReader.ReadMany<FinancialStat>(DatabaseNames.FinancialStatsTable, DatabaseNames.StaticData, _environmentString, sql);
    }

    public async Task<List<FinancialStat>> ReadFinancialStats(int secId)
    {
        string sql =
@$"
SELECT SecurityId,MarketCap
FROM {DatabaseNames.FinancialStatsTable}
WHERE SecurityId = $SecurityId
";
        return await SqlReader.ReadMany<FinancialStat>(DatabaseNames.FinancialStatsTable, DatabaseNames.StaticData, _environmentString, sql, ("$SecurityId", secId));
    }

    public async Task<List<MissingPriceSituation>> ReadDailyMissingPriceSituations(IntervalType interval, SecurityType securityType)
    {
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        var sql = $@"SELECT * FROM (SELECT COUNT(StartTime) as Count, DATE(StartTime) as Date, SecurityId FROM {tableName}
GROUP BY DATE(startTime), SecurityId)";
        return await SqlReader.ReadMany(tableName, DatabaseNames.MarketData, _environmentString, sql, Transform);

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
        return await SqlReader.ReadMany(tableName, DatabaseNames.MarketData, _environmentString, sql, Transform, parameters);

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
        if (_log.IsDebugEnabled)
            _log.Debug($"Read {count} entries from {tableName} table in {DatabaseNames.MarketData}.");
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
        if (_log.IsDebugEnabled)
            _log.Debug($"Read {results.Count} entries from {tableName} table in {DatabaseNames.MarketData}.");
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
        if (_log.IsDebugEnabled)
            _log.Debug($"Read {results.Count} entries from {tableName} table in {DatabaseNames.MarketData}.");
        return results;
    }

    public async Task<List<ExtendedOrderBook>> ReadOrderBooks(Security security, int level, DateTime date)
    {
        var tableName = DatabaseNames.GetOrderBookTableName(security.Code, security.ExchangeType, level);
        using var connection = await Connect(DatabaseNames.MarketData);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {tableName} WHERE SecurityId = {security.Id} AND DATE(Time) = '{date:yyyy-MM-dd}'";
        using var r = await command.ExecuteReaderAsync();
        var results = new List<ExtendedOrderBook>();
        while (await r.ReadAsync())
        {
            var time = r.GetDateTime("Time");
            var orderBook = new ExtendedOrderBook
            {
                SecurityId = security.Id,
                Time = time,
            };

            for (int i = 1; i < level + 1; i++)
            {
                var bid = new OrderBookLevel { Price = r.GetDecimal("B" + i), Size = r.GetDecimal("BS" + i) };
                orderBook.Bids.Add(bid);
                var ask = new OrderBookLevel { Price = r.GetDecimal("B" + i), Size = r.GetDecimal("BS" + i) };
                orderBook.Asks.Add(ask);
            }
            results.Add(orderBook);
        }
        if (_log.IsDebugEnabled)
            _log.Debug($"Read {results.Count} entries from {tableName} table in {DatabaseNames.MarketData}.");
        return results;
    }
}
