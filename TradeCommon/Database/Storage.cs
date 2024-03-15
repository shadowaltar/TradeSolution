using Autofac;
using Common;
using Common.Database;
using log4net;
using Microsoft.Data.Sqlite;
using System;
using System.Data;
using TradeCommon.Constants;
using TradeCommon.Runtime;

namespace TradeCommon.Database;

public partial class Storage : IStorage
{
    private static readonly ILog _log = Logger.New();
    private readonly IComponentContext _container;
    private readonly SqlWriters _writers;
    private readonly Lazy<ApplicationContext> _lazyContext;
    private EnvironmentType _environment;
    private string _environmentString = Environments.ToString(EnvironmentType.Unknown);
    private SqliteConnection? _globalConnection;
    private SqliteTransaction? _globalTransaction;

    private int AccountId => _lazyContext.Value.AccountId;

    public event Action<object, string>? Success;
    public event Action<object, Exception, string>? Failed;

    public IDatabaseSqlBuilder SqlHelper { get; } = new SqliteSqlBuilder();

    public Storage(IComponentContext container)
    {
        _writers = new SqlWriters(this);
        _container = container;
        _lazyContext = new Lazy<ApplicationContext>(_container.Resolve<ApplicationContext>);
    }

    public EnvironmentType Environment
    {
        get => _environment;
        set
        {
            _environment = value;
            _environmentString = Environments.ToString(_environment);
            var dir = Path.Combine(Consts.DatabaseFolder, _environmentString);
            Directory.CreateDirectory(dir);

            _writers.SetEnvironmentString(_environmentString);
        }
    }

    public async Task BeginGlobalTransaction(params string[] databaseNames)
    {
        if (databaseNames.Length == 0)
            throw Exceptions.Invalid("At least one database must be specified");
        var connection = await ConnectAsync(databaseNames[0]);
        if (databaseNames.Length > 1)
        {
            using var cmd = connection.CreateCommand();
            for (int i = 1; i < databaseNames.Length; i++)
            {
                cmd.CommandText = $"ATTACH DATABASE \'{databaseNames[i]}\'";
                cmd.ExecuteNonQuery();
            }
        }
        _globalConnection = connection;
        _globalTransaction = (SqliteTransaction?)await connection.BeginTransactionAsync();
    }

    public async Task<bool> CommitGlobalTransaction()
    {
        if (_globalConnection == null || _globalTransaction == null) return false;
        await _globalTransaction.CommitAsync();
        await _globalConnection.CloseAsync();
        await _globalTransaction.DisposeAsync();
        await _globalConnection.DisposeAsync();
        _globalTransaction = null;
        _globalConnection = null;
        return true;
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

        using var connection = await ConnectAsync(database);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var r = await command.ExecuteReaderAsync();

        if (r.FieldCount != typeCodes.Length)
            throw new InvalidOperationException("Wrong type-code count vs SQL column count.");

        if (!r.HasRows)
            return entries;

        for (int i = 0; i < r.FieldCount; i++)
        {
            var type = TypeConverter.FromTypeCode(typeCodes[i]);
            entries.Columns.Add(new DataColumn(r.GetName(i), type));
        }

        int j = 0;
        while (r.Read())
        {
            DataRow row = entries.NewRow();
            entries.Rows.Add(row);

            for (int i = 0; i < r.FieldCount; i++)
            {
                if (r.IsDBNull(i))
                {
                    continue;
                }
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
        if (_log.IsDebugEnabled)
            _log.Debug($"Read {entries.Rows.Count} entries in {database}. SQL: {sql}");
        return entries;
    }

    public async Task<DataTable> Query(string sql, string database)
    {
        var entries = new DataTable();

        using var connection = await ConnectAsync(database);
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

        if (_log.IsDebugEnabled)
            _log.Debug($"Read {entries.Rows.Count} entries in {database}. SQL: {sql}");
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
        using var connection = await ConnectAsync(database);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var i = await command.ExecuteNonQueryAsync();

        if (_log.IsDebugEnabled)
            _log.Debug($"Executed command in {database}. SQL: {sql}");
        return i;
    }

    public async Task<int> RunMany(List<string> sqls, string database)
    {
        var count = 0;
        using var connection = await ConnectAsync(database);
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
        if (_log.IsDebugEnabled)
            _log.Debug($"Executed multiple commands in {database}. SQLs:{System.Environment.NewLine}{string.Join(System.Environment.NewLine, sqls)}");
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
        const string sql = $"SELECT name FROM sqlite_master WHERE type='table' AND name=$Name;";
        using var conn = await ConnectAsync(database);
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Name", tableName);
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
        return $"Data Source={Path.Combine(Consts.DatabaseFolder, _environmentString, databaseName)}.db";
    }

    private async Task<SqliteConnection> ConnectAsync(string databaseName)
    {
        var conn = new SqliteConnection(GetConnectionString(databaseName));
        await conn.OpenAsync();
        return conn;
    }

    private SqliteConnection Connect(string database)
    {
        var conn = new SqliteConnection(GetConnectionString(database));
        conn.Open();
        return conn;
    }

    private SqliteConnection ConnectToDatabaseFile(string databaseFilePath)
    {
        var conn = new SqliteConnection($"Data Source={databaseFilePath}");
        conn.Open();
        return conn;
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
        private readonly Dictionary<string, ISqlWriter> _writers = [];
        private readonly IStorage _storage;
        private string? _environmentString;

        public SqlWriters(IStorage storage)
        {
            _storage = storage;
        }

        public void SetEnvironmentString(string environmentString)
        {
            _environmentString = environmentString;
        }

        public ISqlWriter Get<T>() where T : class, new()
        {
            if (_environmentString.IsBlank())
                throw Exceptions.MustLogin();

            lock (_writers)
            {
                var type = typeof(T);
                var name = type.Name;
                if (!_writers.TryGetValue(name, out var writer))
                {
                    writer = new SqlWriter<T>(_storage, _environmentString);
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
