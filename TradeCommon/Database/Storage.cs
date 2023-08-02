using Common;
using log4net;
using Microsoft.Data.Sqlite;
using System.Data;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Database;

public partial class Storage
{
    private static readonly ILog _log = Logger.New();
    private static readonly Dictionary<DataType, string> _upsertSqls = new();

    public static readonly string DatabaseFolder = @"c:\temp";

    public static async Task Insert(IPersistenceTask task)
    {
        if (task is PersistenceTask<OhlcPrice> priceTask)
        {
            await InsertPrices(priceTask.SecurityId, priceTask.IntervalType, priceTask.SecurityType, priceTask.Entries);
        }
        else if (task is PersistenceTask<Order> orderTask)
        {
            var firstItem = orderTask.Entries[0];
            await InsertOrder(firstItem, orderTask.SecurityType);
        }
        else if (task is PersistenceTask<Trade> tradeTask)
        {
            var firstItem = tradeTask.Entries[0];
            await InsertTrade(firstItem, tradeTask.SecurityType);
        }
        else if (task is PersistenceTask<Position> positionTask)
        {
            var firstItem = positionTask.Entries[0];
            await InsertPosition(firstItem, positionTask.SecurityType);
        }
        else if (task is PersistenceTask<Security> securityTask)
        {
            if (securityTask.SecurityType == SecurityType.Equity)
                await InsertStockDefinitions(securityTask.Entries);
            if (securityTask.SecurityType == SecurityType.Fx)
                await InsertFxDefinitions(securityTask.Entries);
        }
        else if (task is PersistenceTask<FinancialStat> financialStatsTask)
        {
            await InsertSecurityFinancialStats(financialStatsTask.Entries);
        }
    }

    public static async Task InsertStockDefinitions(List<Security> entries)
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

    public static async Task InsertFxDefinitions(List<Security> entries)
    {
        const string sql =
@$"
INSERT INTO {DatabaseNames.FxDefinitionTable}
    (Code, Name, Exchange, Type, SubType, LotSize, Currency, BaseCurrency, QuoteCurrency, IsEnabled, LocalStartDate, LocalEndDate)
VALUES
    ($Code,$Name,$Exchange,$Type,$SubType,$LotSize,$Currency,$BaseCurrency,$QuoteCurrency,$IsEnabled,$LocalStartDate,$LocalEndDate)
ON CONFLICT (Code, BaseCurrency, QuoteCurrency, Exchange)
DO UPDATE SET
    Name = excluded.Name,
    Type = excluded.Type,
    SubType = excluded.SubType,
    LotSize = excluded.LotSize,
    Currency = excluded.Currency,
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
                command.Parameters.AddWithValue("$BaseCurrency", entry.FxInfo!.BaseCurrency.ToUpperInvariant());
                command.Parameters.AddWithValue("$QuoteCurrency", entry.FxInfo!.QuoteCurrency.ToUpperInvariant());
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

    public static async Task<(int securityId, int count)> InsertPrices(int securityId, IntervalType interval, SecurityType securityType, List<OhlcPrice> prices)
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

    public static async Task<int> InsertSecurityFinancialStats(List<FinancialStat> stats)
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

    public static async Task InsertOrder(Order order, SecurityType securityType)
    {
        var tableName = DatabaseNames.GetOrderTableName(securityType);
        var sql = _upsertSqls!.GetValueOrDefault(DataType.Order, null);
        var writer = new SqlWriter<Order>(tableName, DatabaseFolder, DatabaseNames.ExecutionData, sql);
        _upsertSqls[DataType.Order] = writer.Sql;
        await writer.InsertOne(order);

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

    public static async Task InsertTrade(Trade trade, SecurityType securityType)
    {
        var tableName = DatabaseNames.GetTradeTableName(securityType);
        var sql = _upsertSqls!.GetValueOrDefault(DataType.Trade, null);
        var writer = new SqlWriter<Trade>(tableName, DatabaseFolder, DatabaseNames.ExecutionData, sql);
        _upsertSqls[DataType.Trade] = writer.Sql;
        await writer.InsertOne(trade);
    }

    public static async Task InsertPosition(Position position, SecurityType securityType)
    {
        var tableName = DatabaseNames.GetPositionTableName(securityType);
        var sql = _upsertSqls!.GetValueOrDefault(DataType.Position, null);
        var writer = new SqlWriter<Position>(tableName, DatabaseFolder, DatabaseNames.ExecutionData, sql);
        _upsertSqls[DataType.Position] = writer.Sql;
        await writer.InsertOne(position);
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
