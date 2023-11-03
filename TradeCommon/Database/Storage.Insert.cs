using Common;
using Microsoft.Data.Sqlite;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

namespace TradeCommon.Database;

public partial class Storage
{
    public async Task<int> Insert(PersistenceTask task)
    {
        int count;
        if (task.Type == typeof(Trade))
        {
            count = await Insert<Trade>(task);
        }
        else if (task.Type == typeof(Position))
        {
            count = await Insert<Position>(task);
        }
        else if (task.Type == typeof(Order))
        {
            count = await Insert<Order>(task);
        }
        else if (task.Type == typeof(Asset))
        {
            count = await Insert<Asset>(task);
        }
        else if (task.Type == typeof(Account))
        {
            count = await Insert<Account>(task);
        }
        else if (task.Type == typeof(Security))
        {
            count = await Insert<Security>(task);
        }
        else if (task.Type == typeof(FinancialStat))
        {
            count = await Insert<FinancialStat>(task);
        }
        else if (task.Type == typeof(AlgoEntry))
        {
            count = await Insert<AlgoEntry>(task);
        }
        else if (task.Type == typeof(AlgoBatch))
        {
            count = await Insert<AlgoBatch>(task);
        }
        else if (task.Type == typeof(OrderState))
        {
            count = await Insert<OrderState>(task);
        }
        else
            throw new InvalidOperationException($"Persistence task type {task.Type?.Name} is not supported.");
        return count;
    }

    public async Task<int> Insert<T>(PersistenceTask task) where T : class, new()
    {
        if (task.Action is not DatabaseActionType.Insert and not DatabaseActionType.Upsert)
        {
            _log.Warn("Invalid db action type.");
            return 0;
        }
        var entry = task.GetEntry<T>();
        if (entry != null)
        {
            var count = await InsertOne(entry, task.Action == DatabaseActionType.Upsert);
            if (_log.IsDebugEnabled)
                _log.Debug($"Persisted {count} entry into database: {typeof(T).Name}");
            return count;
        }

        var entries = task.GetEntries<T>();
        if (!entries.IsNullOrEmpty())
        {
            var count = await InsertMany(entries, task.Action == DatabaseActionType.Upsert);
            if (_log.IsDebugEnabled)
                _log.Debug($"Persisted {count} entries into database: {typeof(T).Name}");
            return count;
        }
        throw Exceptions.Impossible();
    }

    public async Task<int> InsertOne<T>(T entry, bool isUpsert, string? tableNameOverride = null) where T : class, new()
    {
        var writer = _writers.Get<T>();
        var (t, _) = DatabaseNames.GetTableAndDatabaseName(entry);
        return await writer.InsertOne(entry, isUpsert, tableNameOverride ?? t);
    }

    public async Task<int> InsertMany<T>(IList<T> entries, bool isUpsert, string? tableNameOverride = null) where T : class, new()
    {
        if (entries.Count == 0)
            return 0;
        var writer = _writers.Get<T>();
        var (t, _) = DatabaseNames.GetTableAndDatabaseName(entries.First());
        return await writer.InsertMany(entries, isUpsert, tableNameOverride ?? t);
    }

    public async Task<int> InsertOrderBooks(List<ExtendedOrderBook> orderBooks, string tableName)
    {
        if (orderBooks.IsNullOrEmpty())
            return 0;
        var first = orderBooks[0];
        var level = first.Bids.Count; // assuming all order books have the same level of depth.

        using var connection = await Connect(DatabaseNames.MarketData);
        using var transaction = connection.BeginTransaction();

        var count = 0;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            string sql;
            lock (_insertSqls)
            {
                if (!_insertSqls.TryGetValue(tableName, out sql))
                {
                    var bidPart = "";
                    var askPart = "";
                    var bidValuePart = "";
                    var askValuePart = "";
                    for (int i = 0; i < level; i++)
                    {
                        var idx = i + 1;
                        bidPart += $"B{idx}, BS{idx}, ";
                        askPart += $"A{idx}, AS{idx}, ";
                        bidValuePart += $"$B{idx}, $BS{idx}, ";
                        askValuePart += $"$A{idx}, $AS{idx}, ";
                    }
                    askPart = askPart[..^2];
                    askValuePart = askValuePart[..^2];
                    sql = $"INSERT INTO {tableName} (SecurityId, Time, {bidPart}{askPart}) VALUES ($SecId, $Time, {bidValuePart}{askValuePart})";
                    _insertSqls[tableName] = sql;
                }
            }
            command.CommandText = sql;
            foreach (var orderBook in orderBooks)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$SecId", orderBook.SecurityId);
                command.Parameters.AddWithValue("$Time", orderBook.Time);
                for (int i = 0; i < level; i++)
                {
                    var idx = i + 1;
                    command.Parameters.AddWithValue("$B" + idx, orderBook.Bids[i].Price);
                    command.Parameters.AddWithValue("$BS" + idx, orderBook.Bids[i].Size);
                    command.Parameters.AddWithValue("$A" + idx, orderBook.Asks[i].Price);
                    command.Parameters.AddWithValue("$AS" + idx, orderBook.Asks[i].Size);
                }
                await command.ExecuteNonQueryAsync();
                count++;
            }
            transaction.Commit();
            if (_log.IsDebugEnabled)
                _log.Debug($"Inserted {count} order book entries into {tableName} table.");
        }
        catch (Exception e)
        {
            _log.Error($"Failed to inserted order book entries into {tableName} table.", e);
            transaction.Rollback();
        }
        finally
        {
            command?.Dispose();
            await connection.CloseAsync();
            connection.Dispose();
        }

        return count;
    }

    public async Task UpsertStockDefinitions(List<Security> entries)
    {
        const string sql =
@$"
INSERT INTO {DatabaseNames.StockDefinitionTable}
    (Code, Name, Exchange, Type, SubType, LotSize, TickSize, Currency, Cusip, Isin, YahooTicker, IsShortable, IsEnabled, LocalStartDate, LocalEndDate)
VALUES
    ($Code,$Name,$Exchange,$Type,$SubType,$LotSize,$TickSize,$Currency,$Cusip,$Isin,$YahooTicker,$IsShortable,$IsEnabled,$LocalStartDate,$LocalEndDate)
ON CONFLICT (Code, Exchange)
DO UPDATE SET
    Name = excluded.Name,
    Type = excluded.Type,
    SubType = excluded.SubType,
    LotSize = excluded.LotSize,
    TickSize = excluded.TickSize,
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
                command.Parameters.AddWithValue("$TickSize", entry.TickSize);
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
            if (_log.IsDebugEnabled)
                _log.Debug($"Upserted {entries.Count} entries into securities table.");
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
    (Code, Name, Exchange, Type, SubType, LotSize, TickSize, BaseCurrency, QuoteCurrency, IsEnabled, IsMarginTradingAllowed, LocalStartDate, LocalEndDate, MaxLotSize, MinNotional, PricePrecision, QuantityPrecision)
VALUES
    ($Code,$Name,$Exchange,$Type,$SubType,$LotSize,$TickSize,$BaseCurrency,$QuoteCurrency,$IsEnabled,$IsMarginTradingAllowed,$LocalStartDate,$LocalEndDate,$MaxLotSize,$MinNotional,$PricePrecision,$QuantityPrecision)
ON CONFLICT (Code, BaseCurrency, QuoteCurrency, Exchange)
DO UPDATE SET
    Name = excluded.Name,
    Type = excluded.Type,
    SubType = excluded.SubType,
    LotSize = excluded.LotSize,
    TickSize = excluded.TickSize,
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
                command.Parameters.AddWithValue("$TickSize", entry.TickSize);
                command.Parameters.AddWithValue("$BaseCurrency", entry.FxInfo!.BaseCurrency);
                command.Parameters.AddWithValue("$QuoteCurrency", entry.FxInfo!.QuoteCurrency);
                command.Parameters.AddWithValue("$PricePrecision", entry.PricePrecision);
                command.Parameters.AddWithValue("$QuantityPrecision", entry.QuantityPrecision);
                command.Parameters.AddWithValue("$IsEnabled", true);
                command.Parameters.AddWithValue("$IsMarginTradingAllowed", entry.FxInfo!.IsMarginTradingAllowed);
                command.Parameters.AddWithValue("$MaxLotSize", entry.FxInfo!.MaxLotSize ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$MinNotional", entry.MinNotional);
                command.Parameters.AddWithValue("$LocalStartDate", 0);
                command.Parameters.AddWithValue("$LocalEndDate", DateTime.MaxValue.ToString("yyyy-MM-dd HH:mm:ss"));

                await command.ExecuteNonQueryAsync();
                count++;
            }
            transaction.Commit();
            if (_log.IsDebugEnabled)
                _log.Debug($"Upserted {entries.Count} entries into securities table.");
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
            if (_log.IsDebugEnabled)
                _log.Debug($"Upserted {count} prices into prices table.");
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
            if (_log.IsDebugEnabled)
                _log.Debug($"Upserted {count} entries into {tableName} table.");
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
}
