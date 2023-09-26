using Common;
using Common.Database;
using Microsoft.Data.Sqlite;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Database;

public partial class Storage
{
    public async Task<int> Insert(PersistenceTask task)
    {
        bool isUpsert = task.IsUpsert;
        if (task.Type == typeof(Trade))
        {
            return await Insert<Trade>(task);
        }
        if (task.Type == typeof(Position))
        {
            return await Insert<Position>(task);
        }
        if (task.Type == typeof(Order))
        {
            return await Insert<Order>(task);
        }
        if (task.Type == typeof(Asset))
        {
            return await Insert<Asset>(task);
        }
        if (task.Type == typeof(Account))
        {
            return await Insert<Account>(task);
        }

        if (task.Type == typeof(Security))
        {
            return await Insert<Security>(task);
        }
        if (task.Type == typeof(FinancialStat))
        {
            return await Insert<FinancialStat>(task);
        }


        throw new InvalidOperationException($"Persistence task type {task.Type?.Name} is not supported.");
    }

    public async Task<int> Insert<T>(PersistenceTask task) where T : class, new()
    {
        var entry = task.GetEntry<T>();
        if (entry != null)
        {
            var count = await InsertOne(entry, task.IsUpsert, task.TableNameOverride);
            _log.Info($"Persisted {count} entry into database: {typeof(T).Name}");
            return count;
        }

        var entries = task.GetEntries<T>();
        if (!entries.IsNullOrEmpty())
        {
            var count = await InsertMany(entries, task.IsUpsert, task.TableNameOverride);
            _log.Info($"Persisted {count} entries into database: {typeof(T).Name}");
            return count;
        }
        throw new InvalidOperationException("Impossible case.");
    }

    public async Task<int> InsertOne<T>(T entry, bool isUpsert, string? tableNameOverride = null) where T : class, new()
    {
        var type = typeof(T);
        var writer = _writers.GetOrCreate(type.Name, () => new SqlWriter<T>(this), (_, w) => Register(w));
        var (t, d) = DatabaseNames.GetTableAndDatabaseName<T>(entry);
        return await writer.InsertOne(entry, isUpsert, tableNameOverride ?? t);
    }

    public async Task<int> InsertMany<T>(IList<T> entry, bool isUpsert, string? tableNameOverride = null) where T : class, new()
    {
        if (entry.Count == 0)
            return 0;
        var type = typeof(T);
        var writer = _writers.GetOrCreate(type.Name, () => new SqlWriter<T>(this), (_, w) => Register(w));
        var (t, d) = DatabaseNames.GetTableAndDatabaseName<T>(entry.First());
        return await writer.InsertMany(entry, isUpsert, tableNameOverride ?? t);
    }

    public async Task UpsertStockDefinitions(List<Security> entries)
    {
        const string sql =
@$"
INSERT INTO {DatabaseNames.StockDefinitionTable}
    (Code, Name, Exchange, Type, SubType, LotSize, Currency, Cusip, Isin, YahooTicker, IsShortable, IsEnabled, LocalStartDate, LocalEndDate)
VALUES
    ($Code,$Name,$Exchange,$Type,$SubType,$LotSize,$Currency,$Cusip,$Isin,$YahooTicker,$IsShortable,$IsEnabled,$LocalStartDate,$LocalEndDate)
ON CONFLICT (Code, Exchange)
DO UPDATE SET
    Name = excluded.Name,
    Type = excluded.Type,
    SubType = excluded.SubType,
    LotSize = excluded.LotSize,
    Currency = excluded.Currency,
    Cusip = excluded.Cusip,
    YahooTicker = excluded.YahooTicker,
    Isin = excluded.Isin,
    IsShortable = excluded.IsShortable,
    IsEnabled = excluded.IsEnabled,
    LocalEndDate = excluded.LocalEndDate
;
";

        using var connection = await Connect(DatabaseNames.StaticData);
        using var transaction = connection.BeginTransaction();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var entry in entries)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$Code", entry.Code);
                command.Parameters.AddWithValue("$Name", entry.Name);
                command.Parameters.AddWithValue("$Exchange", entry.Exchange);
                command.Parameters.AddWithValue("$Type", entry.Type.ToUpperInvariant());
                command.Parameters.AddWithValue("$SubType", entry.SubType?.ToUpperInvariant() ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$LotSize", entry.LotSize);
                command.Parameters.AddWithValue("$Currency", entry.Currency.ToUpperInvariant());
                command.Parameters.AddWithValue("$Cusip", entry.Cusip?.ToUpperInvariant() ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$Isin", entry.Isin?.ToUpperInvariant() ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$YahooTicker", entry.YahooTicker?.ToUpperInvariant() ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$IsShortable", entry.IsShortable);
                command.Parameters.AddWithValue("$IsEnabled", true);
                command.Parameters.AddWithValue("$LocalStartDate", 0);
                command.Parameters.AddWithValue("$LocalEndDate", DateTime.MaxValue.ToString("yyyy-MM-dd HH:mm:ss"));

                await command.ExecuteNonQueryAsync();
            }
            transaction.Commit();
            _log.Info($"Upserted {entries.Count} entries into securities table.");
        }
        catch (Exception e)
        {
            _log.Error($"Failed to upsert into securities table.", e);
            transaction.Rollback();
        }

        await connection.CloseAsync();
    }

    public async Task UpsertFxDefinitions(List<Security> entries)
    {
        const string sql =
@$"
INSERT INTO {DatabaseNames.FxDefinitionTable}
    (Code, Name, Exchange, Type, SubType, LotSize, BaseCurrency, QuoteCurrency, IsEnabled, IsMarginTradingAllowed, LocalStartDate, LocalEndDate, MaxLotSize, MinNotional, PricePrecision, QuantityPrecision)
VALUES
    ($Code,$Name,$Exchange,$Type,$SubType,$LotSize,$BaseCurrency,$QuoteCurrency,$IsEnabled,$IsMarginTradingAllowed,$LocalStartDate,$LocalEndDate,$MaxLotSize,$MinNotional,$PricePrecision,$QuantityPrecision)
ON CONFLICT (Code, BaseCurrency, QuoteCurrency, Exchange)
DO UPDATE SET
    Name = excluded.Name,
    Type = excluded.Type,
    SubType = excluded.SubType,
    LotSize = excluded.LotSize,
    IsEnabled = excluded.IsEnabled,
    LocalEndDate = excluded.LocalEndDate
;
";

        using var connection = await Connect(DatabaseNames.StaticData);
        using var transaction = connection.BeginTransaction();

        var count = 0;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var entry in entries)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$Code", entry.Code);
                command.Parameters.AddWithValue("$Name", entry.Name);
                command.Parameters.AddWithValue("$Exchange", entry.Exchange);
                command.Parameters.AddWithValue("$Type", entry.Type.ToUpperInvariant());
                command.Parameters.AddWithValue("$SubType", entry.SubType?.ToUpperInvariant() ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$LotSize", entry.LotSize);
                command.Parameters.AddWithValue("$BaseCurrency", entry.FxInfo!.BaseCurrency);
                command.Parameters.AddWithValue("$QuoteCurrency", entry.FxInfo!.QuoteCurrency);
                command.Parameters.AddWithValue("$PricePrecision", entry.PricePrecision);
                command.Parameters.AddWithValue("$QuantityPrecision", entry.QuantityPrecision);
                command.Parameters.AddWithValue("$IsEnabled", true);
                command.Parameters.AddWithValue("$IsMarginTradingAllowed", entry.FxInfo!.IsMarginTradingAllowed);
                command.Parameters.AddWithValue("$MaxLotSize", entry.FxInfo!.MaxLotSize ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$MinNotional", entry.FxInfo!.MinNotional ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$LocalStartDate", 0);
                command.Parameters.AddWithValue("$LocalEndDate", DateTime.MaxValue.ToString("yyyy-MM-dd HH:mm:ss"));

                await command.ExecuteNonQueryAsync();
                count++;
            }
            transaction.Commit();
            _log.Info($"Upserted {entries.Count} entries into securities table.");
        }
        catch (Exception e)
        {
            _log.Error($"Failed to upsert into securities table.", e);
            transaction.Rollback();
        }
        finally
        {
            command?.Dispose();
        }

        await connection.CloseAsync();
    }

    public async Task<(int securityId, int count)> UpsertPrices(int securityId, IntervalType interval, SecurityType securityType, List<OhlcPrice> prices)
    {
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        var dailyPriceSpecificColumn1 = securityType == SecurityType.Equity && interval == IntervalType.OneDay ? "AdjClose," : "";
        var dailyPriceSpecificColumn2 = securityType == SecurityType.Equity && interval == IntervalType.OneDay ? "$AdjClose," : "";
        var dailyPriceSpecificColumn3 = securityType == SecurityType.Equity && interval == IntervalType.OneDay ? "AdjClose = excluded.AdjClose," : "";
        string sql =
@$"
INSERT INTO {tableName}
    (SecurityId, Open, High, Low, Close, Volume, {dailyPriceSpecificColumn1} StartTime)
VALUES
    ($SecurityId, $Open, $High, $Low, $Close, $Volume, {dailyPriceSpecificColumn2} $StartTime)
ON CONFLICT (SecurityId, StartTime)
DO UPDATE SET
    Open = excluded.Open,
    High = excluded.High,
    Low = excluded.Low,
    Close = excluded.Close,
    {dailyPriceSpecificColumn3}
    Volume = excluded.Volume;
";
        var count = 0;
        using var connection = await Connect(DatabaseNames.MarketData);
        using var transaction = connection.BeginTransaction();

        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var price in prices)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$SecurityId", securityId);
                command.Parameters.Add(new SqliteParameter("$Open", SqliteType.Real)).Value = price.O;
                command.Parameters.AddWithValue("$High", price.H);
                command.Parameters.AddWithValue("$Low", price.L);
                command.Parameters.AddWithValue("$Close", price.C);
                if (securityType == SecurityType.Equity && interval == IntervalType.OneDay)
                    command.Parameters.AddWithValue("$AdjClose", price.AC);
                command.Parameters.AddWithValue("$Volume", price.V);
                command.Parameters.AddWithValue("$StartTime", price.T);

                await command.ExecuteNonQueryAsync();
                count++;
            }
            transaction.Commit();
            _log.Info($"Upserted {count} prices into prices table.");
        }
        catch (Exception e)
        {
            _log.Error($"Failed to upsert into prices table.", e);
            transaction.Rollback();
        }
        finally
        {
            command?.Dispose();
        }

        await connection.CloseAsync();

        return (securityId, count);
    }

    public async Task<int> UpsertSecurityFinancialStats(List<FinancialStat> stats)
    {
        var count = 0;
        var tableName = DatabaseNames.FinancialStatsTable;
        var sql =
@$"
INSERT INTO {tableName}
    (SecurityId, MarketCap)
VALUES
    ($SecurityId, $MarketCap)
ON CONFLICT (SecurityId)
DO UPDATE SET MarketCap = excluded.MarketCap;
";
        using var connection = await Connect(DatabaseNames.StaticData);
        using var transaction = connection.BeginTransaction();

        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var stat in stats)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$SecurityId", stat.SecurityId);
                command.Parameters.AddWithValue("$MarketCap", stat.MarketCap);
                count++;
                await command.ExecuteNonQueryAsync();
            }
            transaction.Commit();
            _log.Info($"Upserted {count} entries into {tableName} table.");
        }
        catch (Exception e)
        {
            _log.Error($"Failed to upsert into {tableName} table.", e);
            transaction.Rollback();
        }
        finally
        {
            command?.Dispose();
        }

        await connection.CloseAsync();
        return count;
    }

    //public async Task<int> InsertUser(User user)
    //{
    //    var writer = _writers.GetOrCreate(nameof(User), () => new SqlWriter<User>(this), (_, w) => Register(w));
    //    return await writer.InsertOne(user, false);
    //}

    //public async Task<int> InsertAccount(Account account, bool isUpsert)
    //{
    //    var writer = _writers.GetOrCreate(nameof(Account), () => new SqlWriter<Account>(this), (_, w) => Register(w));
    //    var tableName = DatabaseNames.AccountTable;
    //    if (!_writers.TryGetValue(DataType.Account.ToString(), out var writer))
    //    {
    //        writer = new SqlWriter<Account>(this, tableName, DatabaseFolder, DatabaseNames.StaticData);
    //        _writers[DataType.Account.ToString()] = writer;
    //    }
    //    return await writer.InsertOne(account, isUpsert);
    //}

    public async Task<int> InsertMany<T>(IList<T> entries, bool isUpsert, string? sql = null, string? tableNameOverride = null) where T : class, new()
    {
        var writer = _writers.GetOrCreate(typeof(T).Name, () => new SqlWriter<T>(this), (_, w) => Register(w));
        return await writer.InsertMany(entries, isUpsert, tableNameOverride);
    }

    public async Task<int> InsertOne<T>(T entry, bool isUpsert, string? sql = null, string? tableNameOverride = null) where T : class, new()
    {
        var writer = _writers.GetOrCreate(typeof(T).Name, () => new SqlWriter<T>(this), (_, w) => Register(w));
        return await writer.InsertOne(entry, isUpsert, tableNameOverride);
    }

    //public async Task<int> InsertBalance(Asset asset, bool isUpsert)
    //{
    //    var tableName = DatabaseNames.BalanceTable;
    //    if (!_writers.TryGetValue(DataType.Asset.ToString(), out var writer))
    //    {
    //        writer = new SqlWriter<Asset>(this, tableName, DatabaseFolder, DatabaseNames.StaticData);
    //        _writers[DataType.Asset.ToString()] = writer;
    //    }
    //    return await writer.InsertOne(asset, isUpsert);
    //}

    //public async Task<int> InsertOrder(Order order, Security security, bool isUpsert = true)
    //{
    //    if (security == null) return 0;
    //    var tableName = DatabaseNames.GetOrderTableName(security.Type);
    //    if (!_writers.TryGetValue(DataType.Order.ToString(), out var writer))
    //    {
    //        writer = new SqlWriter<Order>(this, tableName, DatabaseFolder, DatabaseNames.ExecutionData);
    //        _writers[DataType.Order.ToString()] = writer;
    //    }
    //    return await writer.InsertOne(order, isUpsert, tableName);
    //}

    //public async Task<int> InsertTrade(Trade trade, Security security, bool isUpsert = true)
    //{
    //    if (security == null) return 0;
    //    var tableName = DatabaseNames.GetTradeTableName(security.Type);
    //    if (!_writers.TryGetValue(DataType.Trade.ToString(), out var writer))
    //    {
    //        writer = new SqlWriter<Trade>(this, tableName, DatabaseFolder, DatabaseNames.ExecutionData);
    //        _writers[DataType.Trade.ToString()] = writer;
    //    }
    //    return await writer.InsertOne(trade, isUpsert);
    //}

    //public async Task<int> InsertPosition(Position position, Security security, bool isUpsert = true)
    //{
    //    if (security == null) return 0;
    //    var tableName = DatabaseNames.GetPositionTableName(security.Type);
    //    if (!_writers.TryGetValue(DataType.Position.ToString(), out var writer))
    //    {
    //        writer = new SqlWriter<Position>(this, tableName, DatabaseFolder, DatabaseNames.ExecutionData);
    //        _writers[DataType.Position.ToString()] = writer;
    //    }
    //    return await writer.InsertOne(position, isUpsert);
    //}

    //public async Task InsertOpenOrderId(OpenOrderId openOrderId)
    //{
    //    var writer = _writers.GetOrCreate(nameof(OpenOrderId), () => new SqlWriter<OpenOrderId>(this), (_, w) => Register(w));
    //    var tableName = DatabaseNames.OpenOrderIdTable;
    //    if (!_writers.TryGetValue(DataType.OpenOrderId.ToString(), out var writer))
    //    {
    //        writer = new SqlWriter<OpenOrderId>(this, tableName, DatabaseFolder, DatabaseNames.ExecutionData);
    //        _writers[DataType.OpenOrderId.ToString()] = writer;
    //    }
    //    await writer.InsertOne(openOrderId, false);
    //}
}
