﻿using Common;
using Microsoft.Data.Sqlite;
using TradeCommon.Constants;
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
    private readonly string DatabaseFolder = Consts.DatabaseFolder;

    public async Task Insert(IPersistenceTask task, bool isUpsert = true)
    {
        _log.Info("Persisting into database: " + task.GetType().Name);

        if (task is PersistenceTask<OhlcPrice> priceTask)
        {
            if (!priceTask.Entries.IsNullOrEmpty())
                await UpsertPrices(priceTask.SecurityId, priceTask.IntervalType, priceTask.SecurityType, priceTask.Entries);
        }
        else if (task is PersistenceTask<Order> orderTask)
        {
            if (!orderTask.Entries.IsNullOrEmpty())
                foreach (var entry in orderTask.Entries)
                    await InsertOrder(entry, isUpsert);
            else if (orderTask.Entry != null)
                await InsertOrder(orderTask.Entry, isUpsert);
        }
        else if (task is PersistenceTask<Trade> tradeTask)
        {
            if (!tradeTask.Entries.IsNullOrEmpty())
                foreach (var entry in tradeTask.Entries)
                    await InsertTrade(entry, isUpsert);
            else if (tradeTask.Entry != null)
                await InsertTrade(tradeTask.Entry, isUpsert);
        }
        else if (task is PersistenceTask<OpenOrderId> openOrderIdTask)
        {
            if (!openOrderIdTask.Entries.IsNullOrEmpty())
                foreach (var entry in openOrderIdTask.Entries)
                    await InsertOpenOrderId(entry);
            else if (openOrderIdTask.Entry != null)
                await InsertOpenOrderId(openOrderIdTask.Entry);
        }
        else if (task is PersistenceTask<Position> positionTask)
        {
            if (!positionTask.Entries.IsNullOrEmpty())
                foreach (var entry in positionTask.Entries)
                    await InsertPosition(entry, isUpsert);
            else if (positionTask.Entry != null)
                await InsertPosition(positionTask.Entry, isUpsert);
        }
        else if (task is PersistenceTask<Security> securityTask && !securityTask.Entries.IsNullOrEmpty())
        {
            if (securityTask.SecurityType == SecurityType.Equity)
                await UpsertStockDefinitions(securityTask.Entries);
            else if (securityTask.SecurityType == SecurityType.Fx)
                await UpsertFxDefinitions(securityTask.Entries);
        }
        else if (task is PersistenceTask<Balance> balanceTask)
        {
            if (balanceTask.Entry != null)
                await InsertBalance(balanceTask.Entry, true);
            else if (!balanceTask.Entries.IsNullOrEmpty())
                foreach (var entry in balanceTask.Entries)
                    await InsertBalance(entry, true);
        }
        else if (task is PersistenceTask<FinancialStat> financialStatsTask && !financialStatsTask.Entries.IsNullOrEmpty())
        {
            await UpsertSecurityFinancialStats(financialStatsTask.Entries);
        }
        else if (task is PersistenceTask<Account> accountTask && accountTask.Entry != null)
        {
            await InsertAccount(accountTask.Entry, true);
        }
        else
        {
            throw new InvalidOperationException($"Persistence task type {task.GetType().Name} is not supported.");
        }
    }

    public async Task<int> Insert<T>(IPersistenceTask task, bool isUpsert = true) where T : new()
    {
        if (task is PersistenceTask<T> pt)
        {
            if (pt.Entry != null)
            {
                var count = await InsertOne<T>(pt.Entry, isUpsert);
                _log.Info($"Persisted {count} entry into database: {typeof(T).Name}");
                return count;
            }
            else if (!pt.Entries.IsNullOrEmpty())
            {
                var count = await InsertMany<T>(pt.Entries, isUpsert);
                _log.Info($"Persisted {count} entries into database: {typeof(T).Name}");
                return count;
            }
        }
        throw new InvalidOperationException("Impossible case.");
    }

    public async Task<int> InsertOne<T>(T entry, bool isUpsert) where T : new()
    {
        var type = typeof(T);
        var (tableName, database) = DatabaseNames.GetTableAndDatabaseName<T>();
        if (!_writers.TryGetValue(type.Name, out var writer))
        {
            writer = new SqlWriter<T>(this, tableName, DatabaseFolder, database);
            _writers[type.Name] = writer;
        }
        return await writer.InsertOne(entry, isUpsert);
    }

    public async Task<int> InsertMany<T>(List<T> entry, bool isUpsert) where T : new()
    {
        var type = typeof(T);
        var (tableName, database) = DatabaseNames.GetTableAndDatabaseName<T>();
        if (!_writers.TryGetValue(type.Name, out var writer))
        {
            writer = new SqlWriter<T>(this, tableName, DatabaseFolder, database);
            _writers[type.Name] = writer;
        }
        return await writer.InsertMany(entry, isUpsert);
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

    public async Task<int> InsertUser(User user)
    {
        var tableName = DatabaseNames.UserTable;
        if (!_writers.TryGetValue(DataType.User.ToString(), out var writer))
        {
            writer = new SqlWriter<User>(this, tableName, DatabaseFolder, DatabaseNames.StaticData);
            _writers[DataType.User.ToString()] = writer;
        }
        return await writer.InsertOne(user, false);
    }

    public async Task<int> InsertAccount(Account account, bool isUpsert)
    {
        var tableName = DatabaseNames.AccountTable;
        if (!_writers.TryGetValue(DataType.Account.ToString(), out var writer))
        {
            writer = new SqlWriter<Account>(this, tableName, DatabaseFolder, DatabaseNames.StaticData);
            _writers[DataType.Account.ToString()] = writer;
        }
        return await writer.InsertOne(account, isUpsert);
    }

    public async Task<int> InsertBalance(Balance balance, bool isUpsert)
    {
        var tableName = DatabaseNames.BalanceTable;
        if (!_writers.TryGetValue(DataType.Balance.ToString(), out var writer))
        {
            writer = new SqlWriter<Balance>(this, tableName, DatabaseFolder, DatabaseNames.StaticData);
            _writers[DataType.Balance.ToString()] = writer;
        }
        return await writer.InsertOne(balance, isUpsert);
    }

    public async Task<int> InsertOrder(Order order, bool isUpsert = true)
    {
        var security = GetSecurity(order.SecurityId);
        if (security == null) return 0;
        var tableName = DatabaseNames.GetOrderTableName(security.Type);
        if (!_writers.TryGetValue(DataType.Order.ToString(), out var writer))
        {
            writer = new SqlWriter<Order>(this, tableName, DatabaseFolder, DatabaseNames.ExecutionData);
            _writers[DataType.Order.ToString()] = writer;
        }
        return await writer.InsertOne(order, isUpsert);
    }

    public async Task<int> InsertTrade(Trade trade, bool isUpsert = true)
    {
        var security = GetSecurity(trade.SecurityId);
        if (security == null) return 0;
        var tableName = DatabaseNames.GetTradeTableName(security.Type);
        if (!_writers.TryGetValue(DataType.Trade.ToString(), out var writer))
        {
            writer = new SqlWriter<Trade>(this, tableName, DatabaseFolder, DatabaseNames.ExecutionData);
            _writers[DataType.Trade.ToString()] = writer;
        }
        return await writer.InsertOne(trade, isUpsert);
    }

    public async Task<int> InsertPosition(Position position, bool isUpsert = true)
    {
        var security = GetSecurity(position.SecurityId);
        if (security == null) return 0;
        var tableName = DatabaseNames.GetPositionTableName(security.Type);
        if (!_writers.TryGetValue(DataType.Position.ToString(), out var writer))
        {
            writer = new SqlWriter<Position>(this, tableName, DatabaseFolder, DatabaseNames.ExecutionData);
            _writers[DataType.Position.ToString()] = writer;
        }
        return await writer.InsertOne(position, isUpsert);
    }

    public async Task InsertOpenOrderId(OpenOrderId openOrderId)
    {
        var tableName = DatabaseNames.OpenOrderIdTable;
        if (!_writers.TryGetValue(DataType.OpenOrderId.ToString(), out var writer))
        {
            writer = new SqlWriter<OpenOrderId>(this, tableName, DatabaseFolder, DatabaseNames.ExecutionData);
            _writers[DataType.OpenOrderId.ToString()] = writer;
        }
        await writer.InsertOne(openOrderId, false);
    }
}
