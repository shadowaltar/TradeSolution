﻿using Common;
using log4net;
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
using static TradeCommon.Constants.Constants;

namespace TradeCommon.Database;

public partial class Storage
{
    private static readonly ILog _log = Logger.New();
    private static readonly Dictionary<DataType, ISqlWriter> _writers = new();

    public static async Task Insert(IPersistenceTask task, bool isUpsert = true)
    {
        if (task is PersistenceTask<OhlcPrice> priceTask)
        {
            await UpsertPrices(priceTask.SecurityId, priceTask.IntervalType, priceTask.SecurityType, priceTask.Entries);
        }
        else if (task is PersistenceTask<Order> orderTask)
        {
            if (orderTask.Entries.Count > 0)
                foreach (var entry in orderTask.Entries)
                    await InsertOrder(entry, orderTask.SecurityType, isUpsert);
            else
                await InsertOrder(orderTask.Entry, orderTask.SecurityType, isUpsert);
        }
        else if (task is PersistenceTask<Trade> tradeTask)
        {
            if (tradeTask.Entries.Count > 0)
                foreach (var entry in tradeTask.Entries)
                    await InsertTrade(entry, tradeTask.SecurityType, isUpsert);
            else
                await InsertTrade(tradeTask.Entry, tradeTask.SecurityType, isUpsert);
        }
        else if (task is PersistenceTask<Position> positionTask)
        {
            if (positionTask.Entries.Count > 0)
                foreach (var entry in positionTask.Entries)
                    await InsertPosition(entry, positionTask.SecurityType, isUpsert);
            else
                await InsertPosition(positionTask.Entry, positionTask.SecurityType, isUpsert);
        }
        else if (task is PersistenceTask<Security> securityTask)
        {
            if (securityTask.SecurityType == SecurityType.Equity)
                await UpsertStockDefinitions(securityTask.Entries);
            if (securityTask.SecurityType == SecurityType.Fx)
                await UpsertFxDefinitions(securityTask.Entries);
        }
        else if (task is PersistenceTask<FinancialStat> financialStatsTask)
        {
            await UpsertSecurityFinancialStats(financialStatsTask.Entries);
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
                command.Parameters.AddWithValue("$MaxLotSize", entry.FxInfo!.MaxLotSize);
                command.Parameters.AddWithValue("$MinNotional", entry.FxInfo!.MinNotional);
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
        if (!_writers.TryGetValue(DataType.Account, out var writer))
        {
            writer = new SqlWriter<User>(tableName, DatabaseFolder, DatabaseNames.StaticData);
            _writers[DataType.User] = writer;
        }
        return await writer.InsertOne(user, false);
    }

    public static async Task<int> InsertAccount(Account account)
    {
        var tableName = DatabaseNames.AccountTable;
        if (!_writers.TryGetValue(DataType.Account, out var writer))
        {
            writer = new SqlWriter<Account>(tableName, DatabaseFolder, DatabaseNames.StaticData);
            _writers[DataType.Account] = writer;
        }
        return await writer.InsertOne(account, true);
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

        //        string sql =
        //@$"
        //INSERT INTO {tableName}
        //    (Id, ExternalOrderId, SecurityId, AccountId, Type, Side, StopPrice, CreateTime, UpdateTime, ExternalCreateTime, ExternalUpdateTime, TimeInForce, StrategyId)
        //VALUES
        //    ($Id, $ExternalOrderId, $SecurityId, $AccountId, $Type, $Side, $StopPrice, $CreateTime, $UpdateTime, $ExternalCreateTime, $ExternalUpdateTime, $TimeInForce, $StrategyId)
        //ON CONFLICT (Id)
        //DO UPDATE SET
        //    ExternalOrderId = excluded.ExternalOrderId,
        //    SecurityId = excluded.SecurityId,
        //    AccountId = excluded.AccountId,
        //    Type = excluded.Type,
        //    Side = excluded.Side,
        //    StopPrice = excluded.StopPrice,
        //    CreateTime = excluded.CreateTime,
        //    UpdateTime = excluded.UpdateTime,
        //    ExternalCreateTime = excluded.ExternalCreateTime,
        //    ExternalUpdateTime = excluded.ExternalUpdateTime,
        //    TimeInForce = excluded.TimeInForce,
        //    StrategyId = excluded.StrategyId,
        //    BrokerId = excluded.BrokerId,
        //    ExchangeId = excluded.ExchangeId
        //";
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

    /// <summary>
    /// Execute a query and return a <see cref="DataTable"/>.
    /// Must specify all columns' type in <see cref="TypeCode"/>.
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="database"></param>
    /// <param name="typeCodes"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotImplementedException"></exception>
    public static async Task<DataTable> Query(string sql, string database, params TypeCode[] typeCodes)
    {
        var entries = new DataTable();

        using var connection = await Connect(database);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var r = await command.ExecuteReaderAsync();

        if (r.FieldCount != typeCodes.Length)
            throw new InvalidOperationException("Wrong type-code count vs SQL column count.");

        if (!r.HasRows)
            return entries;

        for (int i = 0; i < r.FieldCount; i++)
        {
            entries.Columns.Add(new DataColumn(r.GetName(i)));
        }

        int j = 0;
        while (r.Read())
        {
            DataRow row = entries.NewRow();
            entries.Rows.Add(row);

            for (int i = 0; i < r.FieldCount; i++)
            {
                entries.Rows[j][i] = typeCodes[i] switch
                {
                    TypeCode.Char or TypeCode.String => r.GetString(i),
                    TypeCode.Decimal => r.GetDecimal(i),
                    TypeCode.DateTime => r.GetDateTime(i),
                    TypeCode.Int32 => r.GetInt32(i),
                    TypeCode.Boolean => r.GetBoolean(i),
                    TypeCode.Double => r.GetDouble(i),
                    TypeCode.Int64 => r.GetInt64(i),
                    _ => throw new NotImplementedException(),
                };
            }

            j++;
        }

        _log.Info($"Read {entries.Rows.Count} entries in {database}. SQL: {sql}");
        return entries;
    }

    /// <summary>
    /// Execute a query and return a <see cref="DataTable"/>. All values are in strings.
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="database"></param>
    /// <returns></returns>
    public static async Task<DataTable> Query(string sql, string database)
    {
        var entries = new DataTable();

        using var connection = await Connect(database);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var r = await command.ExecuteReaderAsync();

        if (!r.HasRows)
            return entries;

        for (int i = 0; i < r.FieldCount; i++)
        {
            entries.Columns.Add(new DataColumn(r.GetName(i)));
        }

        int j = 0;
        while (r.Read())
        {
            DataRow row = entries.NewRow();
            entries.Rows.Add(row);

            for (int i = 0; i < r.FieldCount; i++)
            {
                entries.Rows[j][i] = r.GetValue(i);
            }

            j++;
        }

        _log.Info($"Read {entries.Rows.Count} entries in {database}. SQL: {sql}");
        return entries;
    }

    /// <summary>
    /// Check if a table exists.
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="database"></param>
    /// <returns></returns>
    public static async Task<bool> CheckTableExists(string tableName, string database)
    {
        if (tableName.IsBlank()) return false;
        if (database.IsBlank()) return false;
        const string sql = $"SELECT name FROM sqlite_master WHERE type='table' AND name=@Name;";
        using var conn = await Connect(database);
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Name", tableName);
        object? r = await command.ExecuteScalarAsync();
        return r != null;
    }

    /// <summary>
    /// Purge the databases.
    /// </summary>
    public static void PurgeDatabase()
    {
        var databaseNames = new[] { DatabaseNames.MarketData, DatabaseNames.StaticData, DatabaseNames.ExecutionData };
        try
        {
            foreach (var databaseName in databaseNames)
            {
                var filePath = Path.Combine(DatabaseFolder, databaseName + ".db");
                File.Delete(filePath);
                _log.Info($"Deleted database file: {filePath}");
            }
        }
        catch (Exception e)
        {
            _log.Error($"Failed to purge Sqlite database files.", e);
        }
    }

    private static string? GetConnectionString(string databaseName)
    {
        return $"Data Source={Path.Combine(DatabaseFolder, databaseName)}.db";
    }

    private static async Task<SqliteConnection> Connect(string database)
    {
        var conn = new SqliteConnection(GetConnectionString(database));
        await conn.OpenAsync();
        return conn;
    }
}
