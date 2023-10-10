using Common;
using Common.Database;
using log4net;
using Microsoft.Data.Sqlite;
using System.Data;
using TradeCommon.Constants;

namespace TradeCommon.Database;

public partial class Storage : IStorage
{
    private readonly ILog _log = Logger.New();
    private readonly SqlWriters _writers;

    public event Action<object, string>? Success;
    public event Action<object, Exception, string>? Failed;

    public IDatabaseSchemaHelper SchemaHelper { get; } = new SqliteSchemaHelper();

    public Storage()
    {
        _writers = new SqlWriters(this);
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
    public async Task<DataTable> Query(string sql, string database, params TypeCode[] typeCodes)
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

    public async Task<DataTable> Query(string sql, string database)
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
    /// Execute a query and return a <see cref="DataTable"/>. All values are in strings.
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="database"></param>
    /// <returns></returns>
    public async Task<int> RunOne(string sql, string database)
    {
        using var connection = await Connect(database);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var i = await command.ExecuteNonQueryAsync();

        _log.Info($"Executed command in {database}. SQL: {sql}");
        return i;
    }

    public async Task<int> RunMany(List<string> sqls, string database)
    {
        var count = 0;
        using var connection = await Connect(database);
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var sql in sqls)
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                var i = await command.ExecuteNonQueryAsync();
                count += i;
            }
            transaction.Commit();
        }
        catch (Exception e)
        {
            _log.Error("Failed to run many sqls. ", e);
            transaction.Rollback();
        }

        _log.Info($"Executed multiple commands in {database}. SQLs:{Environment.NewLine}{string.Join(Environment.NewLine, sqls)}");
        return count;
    }

    /// <summary>
    /// Check if a table exists.
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="database"></param>
    /// <returns></returns>
    public async Task<bool> CheckTableExists(string tableName, string database)
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
    public void PurgeDatabase()
    {
        var databaseNames = new[] { DatabaseNames.MarketData, DatabaseNames.StaticData, DatabaseNames.ExecutionData };
        try
        {
            foreach (var databaseName in databaseNames)
            {
                var filePath = Path.Combine(Consts.DatabaseFolder, databaseName + ".db");
                File.Delete(filePath);
                _log.Info($"Deleted database file: {filePath}");
            }
        }
        catch (Exception e)
        {
            _log.Error($"Failed to purge Sqlite database files.", e);
        }
    }

    private string? GetConnectionString(string databaseName)
    {
        return $"Data Source={Path.Combine(Consts.DatabaseFolder, databaseName)}.db";
    }

    private async Task<SqliteConnection> Connect(string database)
    {
        var conn = new SqliteConnection(GetConnectionString(database));
        await conn.OpenAsync();
        return conn;
    }

    private void Register(ISqlWriter writer)
    {
        writer.Success -= RaiseSuccess;
        writer.Success += RaiseSuccess;
        writer.Failed -= RaiseFailed;
        writer.Failed += RaiseFailed;
    }

    public void RaiseSuccess(object entry, string methodName = "")
    {
        Success?.Invoke(entry, methodName);
    }

    public void RaiseFailed(object entry, Exception e, string methodName = "")
    {
        Failed?.Invoke(entry, e, methodName);
    }

    protected class SqlWriters
    {
        private readonly Dictionary<string, ISqlWriter> _writers = new();
        private readonly IStorage _storage;

        public SqlWriters(IStorage storage)
        {
            _storage = storage;
        }

        public ISqlWriter Get<T>() where T : class, new()
        {
            lock (_writers)
            {
                var type = typeof(T);
                var name = type.Name;
                if (!_writers.TryGetValue(name, out var writer))
                {
                    writer = new SqlWriter<T>(_storage);
                    writer.Success += RaiseSuccess;
                    writer.Failed += RaiseFailed;
                    _writers[name] = writer;
                }
                return writer;
            }
        }

        protected void RaiseSuccess(object entry, string methodName = "")
        {
            _storage.RaiseSuccess(entry, methodName);
        }

        protected void RaiseFailed(object entry, Exception e, string methodName = "")
        {
            _storage.RaiseFailed(entry, e, methodName);
        }
    }
}
