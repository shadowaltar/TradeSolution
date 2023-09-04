using Common;
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
        else if (task is PersistenceTask<Account> accountTask && accountTask.Entry != null)
        {
            await InsertAccount(accountTask.Entry, true);
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
