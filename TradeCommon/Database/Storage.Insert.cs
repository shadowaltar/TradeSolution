using Common;
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
    private static readonly string DatabaseFolder = Constants.Constants.DatabaseFolder;

    public static async Task Insert(IPersistenceTask task, bool isUpsert = true)
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
                    await InsertOrder(entry, orderTask.SecurityType, isUpsert);
            else if (orderTask.Entry != null)
                await InsertOrder(orderTask.Entry, orderTask.SecurityType, isUpsert);
        }
        else if (task is PersistenceTask<Trade> tradeTask)
        {
            if (!tradeTask.Entries.IsNullOrEmpty())
                foreach (var entry in tradeTask.Entries)
                    await InsertTrade(entry, tradeTask.SecurityType, isUpsert);
            else if (tradeTask.Entry != null)
                await InsertTrade(tradeTask.Entry, tradeTask.SecurityType, isUpsert);
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
                    await InsertPosition(entry, positionTask.SecurityType, isUpsert);
            else if (positionTask.Entry != null)
                await InsertPosition(positionTask.Entry, positionTask.SecurityType, isUpsert);
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

    public static async Task UpsertStockDefinitions(List<Security> entries)
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
        finally
        {
            command?.Dispose();
        }

        await connection.CloseAsync();
    }

    public static async Task UpsertFxDefinitions(List<Security> entries)
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

    public static async Task<(int securityId, int count)> UpsertPrices(int securityId, IntervalType interval, SecurityType securityType, List<OhlcPrice> prices)
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

    public static async Task<int> UpsertSecurityFinancialStats(List<FinancialStat> stats)
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

    public static async Task<int> InsertUser(User user)
    {
        var tableName = DatabaseNames.UserTable;
        if (!_writers.TryGetValue(DataType.User, out var writer))
        {
            writer = new SqlWriter<User>(tableName, DatabaseFolder, DatabaseNames.StaticData);
            _writers[DataType.User] = writer;
        }
        return await writer.InsertOne(user, false);
    }

    public static async Task<int> InsertAccount(Account account, bool isUpsert)
    {
        var tableName = DatabaseNames.AccountTable;
        if (!_writers.TryGetValue(DataType.Account, out var writer))
        {
            writer = new SqlWriter<Account>(tableName, DatabaseFolder, DatabaseNames.StaticData);
            _writers[DataType.Account] = writer;
        }
        return await writer.InsertOne(account, isUpsert);
    }

    public static async Task<int> InsertBalance(Balance balance, bool isUpsert)
    {
        var tableName = DatabaseNames.BalanceTable;
        if (!_writers.TryGetValue(DataType.Balance, out var writer))
        {
            writer = new SqlWriter<Balance>(tableName, DatabaseFolder, DatabaseNames.StaticData);
            _writers[DataType.Balance] = writer;
        }
        return await writer.InsertOne(balance, isUpsert);
    }

    public static async Task InsertOrder(Order order, SecurityType securityType, bool isUpsert = true)
    {
        var tableName = DatabaseNames.GetOrderTableName(securityType);
        if (!_writers.TryGetValue(DataType.Order, out var writer))
        {
            writer = new SqlWriter<Order>(tableName, DatabaseFolder, DatabaseNames.ExecutionData);
            _writers[DataType.Order] = writer;
        }
        await writer.InsertOne(order, isUpsert);
    }

    public static async Task InsertTrade(Trade trade, SecurityType securityType, bool isUpsert = true)
    {
        var tableName = DatabaseNames.GetTradeTableName(securityType);
        if (!_writers.TryGetValue(DataType.Trade, out var writer))
        {
            writer = new SqlWriter<Trade>(tableName, DatabaseFolder, DatabaseNames.ExecutionData);
            _writers[DataType.Trade] = writer;
        }
        await writer.InsertOne(trade, isUpsert);
    }

    public static async Task InsertPosition(Position position, SecurityType securityType, bool isUpsert = true)
    {
        var tableName = DatabaseNames.GetPositionTableName(securityType);
        if (!_writers.TryGetValue(DataType.Position, out var writer))
        {
            writer = new SqlWriter<Position>(tableName, DatabaseFolder, DatabaseNames.ExecutionData);
            _writers[DataType.Position] = writer;
        }
        await writer.InsertOne(position, isUpsert);
    }

    public static async Task InsertOpenOrderId(OpenOrderId openOrderId)
    {
        var tableName = DatabaseNames.OpenOrderIdTable;
        if (!_writers.TryGetValue(DataType.OpenOrderId, out var writer))
        {
            writer = new SqlWriter<OpenOrderId>(tableName, DatabaseFolder, DatabaseNames.ExecutionData);
            _writers[DataType.OpenOrderId] = writer;
        }
        await writer.InsertOne(openOrderId, false);
    }
}
