using Common.Attributes;
using log4net;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using TradeCommon.Constants;
using TradeCommon.Database;

namespace Common.Database;

public class SqlWriter<T> : ISqlWriter, IDisposable where T : class, new()
{
    private static readonly ILog _log = Logger.New();

    private readonly string[] _targetFieldNames;
    private readonly IDictionary<string, string> _targetFieldNamePlaceHolders;
    private readonly IDictionary<string, PropertyInfo> _properties;
    private readonly string[] _uniqueKeyNames;
    private readonly ValueGetter<T> _valueGetter;

    public List<string> AutoIncrementOnInsertFieldNames { get; }

    private readonly IDatabase _db;
    private readonly string _environmentString;
    private readonly string _databasePath;
    private readonly string _databaseName;
    private readonly char _placeholderPrefix;

    public event Action<object, string> Success;
    public event Action<object, Exception, string> Failed;

    public virtual string ConnectionString { get; protected set; }
    public virtual string DefaultTableName { get; protected set; }

    public Dictionary<string, string> InsertSqls { get; } = new();

    public Dictionary<string, string> UpsertSqls { get; } = new();

    public Dictionary<string, string> DeleteSqls { get; } = new();

    public Dictionary<string, string> DropTableAndIndexSqls { get; } = new();

    public Dictionary<string, string> CreateTableAndIndexSqls { get; } = new();

    public SqlWriter(IDatabase db, string environmentString, char placeholderPrefix = Consts.SqlCommandPlaceholderPrefix)
    {
        var (t, d) = DatabaseNames.GetTableAndDatabaseName<T>();
        DefaultTableName = t ?? throw new ArgumentNullException("Default table name");
        _databasePath = Consts.DatabaseFolder;
        _databaseName = d;

        if (!Directory.Exists(_databasePath))
            Directory.CreateDirectory(_databasePath);

        _db = db;
        _environmentString = environmentString;

        ConnectionString = $"Data Source={Path.Combine(_databasePath, _environmentString, _databaseName)}.db";
        _placeholderPrefix = placeholderPrefix;
        _properties = ReflectionUtils.GetPropertyToName(typeof(T)).ShallowCopy();
        _uniqueKeyNames = ReflectionUtils.GetAttributeInfo<T>().PrimaryUniqueKey.ToArray();
        _targetFieldNames = _properties.Select(pair => pair.Key).ToArray();
        _targetFieldNamePlaceHolders = _targetFieldNames.ToDictionary(fn => fn, fn => _placeholderPrefix + fn);

        AutoIncrementOnInsertFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<AutoIncrementOnInsertAttribute>() != null)
            .Select(pair => pair.Key).ToList();

        _valueGetter = ReflectionUtils.GetValueGetter<T>();
    }

    public async Task<int> InsertOne<T1>(T1 entry, string? tableNameOverride = null)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();
        if (entry == null)
            return 0;

        var tableName = tableNameOverride ?? DefaultTableName;
        using var connection = await Connect();
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            var sql = GetInsertSql(tableName);
            command.CommandText = sql;
            var e = (T)(object)entry;

            SetCommandParameters(command, e);
            result = await command.ExecuteNonQueryAsync();

            if (_log.IsDebugEnabled)
                _log.Debug($"Upserted 1 {typeof(T).Name} entry into {tableName} table.");

            RaiseSuccess(entry);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to upsert into {tableName} table.", e);
            RaiseFailed(entry, e);
        }
        finally
        {
            command?.Dispose();
        }

        await connection.CloseAsync();
        return result;
    }

    public async Task<int> UpsertOne<T1>(T1 entry, string? tableNameOverride = null)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();
        if (entry == null)
            return 0;

        var tableName = tableNameOverride ?? DefaultTableName;
        using var connection = await Connect();
        using var transaction = connection.BeginTransaction();
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            var sql = GetUpsertSql(tableName);
            command.CommandText = sql;
            var e = (T)(object)entry;

            SetCommandParameters(command, e);
            result = await command.ExecuteNonQueryAsync();

            if (_log.IsDebugEnabled)
                _log.Debug($"Upserted 1 {typeof(T).Name} entry into {tableName} table.");

            transaction.Commit();
            RaiseSuccess(entry);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to upsert into {tableName} table.", e);
            transaction.Rollback();
            RaiseFailed(entry, e);
        }
        finally
        {
            command?.Dispose();
        }

        await connection.CloseAsync();
        return result;
    }

    public async Task<int> InsertMany<T1>(IList<T1> entries, bool isUpsert, string? tableNameOverride = null)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();

        using var connection = await Connect();
        using var transaction = connection.BeginTransaction();

        var tableName = tableNameOverride ?? DefaultTableName;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            var sql = isUpsert ? GetUpsertSql(tableName) : GetInsertSql(tableName);
            command.CommandText = sql;
            foreach (object? entry in entries)
            {
                if (entry == null)
                    continue;

                command.Parameters.Clear();
                SetCommandParameters(command, (T)entry);
                result += await command.ExecuteNonQueryAsync();
            }
            transaction.Commit();
            if (_log.IsDebugEnabled)
                _log.Debug($"Upserted {result} entries into {tableName} table.");

            RaiseSuccess(entries);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to upsert into {tableName} table.", e);
            transaction.Rollback();
            RaiseFailed(entries, e);
        }
        finally
        {
            command?.Dispose();
        }

        await connection.CloseAsync();
        return result;
    }

    public async Task<int> MoveOne<T1>(T1 entry, bool isUpsert, string fromTableName, string toTableName)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();
        if (entry == null)
            return 0;

        using var connection = await Connect();
        using SqliteTransaction transaction = connection.BeginTransaction();
        SqliteCommand? insertCommand = null;
        SqliteCommand? deleteCommand = null;
        try
        {
            var e = (T)(object)entry;

            insertCommand = connection.CreateCommand();
            var insertSql = GetUpsertSql(toTableName);
            insertCommand.CommandText = insertSql;
            SetCommandParameters(insertCommand, e);
            result = await insertCommand.ExecuteNonQueryAsync();
            if (result != 1)
            {
                throw new Exception("Failed to insert during move.");
            }
            deleteCommand = connection.CreateCommand();
            var deleteSql = GetDeleteSql(fromTableName);
            deleteCommand.CommandText = deleteSql;
            SetCommandParameters(deleteCommand, e, _uniqueKeyNames);
            result = await deleteCommand.ExecuteNonQueryAsync();
            if (result != 1)
            {
                throw new Exception("Failed to delete after move.");
            }

            transaction.Commit();

            if (_log.IsDebugEnabled)
                _log.Debug($"Moved 1 {typeof(T).Name} entry from {fromTableName} to {toTableName}.");

            RaiseSuccess(entry);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to move from {fromTableName} to {toTableName}.", e);
            transaction.Rollback();
            RaiseFailed(entry, e);
        }
        finally
        {
            insertCommand?.Dispose();
            deleteCommand?.Dispose();
        }

        await connection.CloseAsync();
        return result;
    }

    public async Task<int> DeleteOne<T1>(T1 entry, string? tableNameOverride = null)
    {
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();
        if (entry == null)
            return 0;

        var result = 0;
        var tableName = tableNameOverride ?? DefaultTableName;
        using var connection = await Connect();
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            var sql = GetDeleteSql(tableName);
            command.CommandText = sql;
            var e = (T)(object)entry;
            SetCommandParameters(command, e, _uniqueKeyNames);
            result = await command.ExecuteNonQueryAsync();
            if (_log.IsDebugEnabled)
                _log.Debug($"Deleted 1 entry from {tableName} table.");
            RaiseSuccess(entry);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to delete from {tableName} table.", e);
            RaiseFailed(entry, e);
        }
        finally
        {
            command?.Dispose();
        }

        await connection.CloseAsync();
        return result;
    }

    public async Task<int> DeleteMany<T1>(IList<T1> entries, string? tableNameOverride = null)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();

        using var connection = await Connect();
        using var transaction = connection.BeginTransaction();

        var tableName = tableNameOverride ?? DefaultTableName;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            var sql = GetDeleteSql(tableName);
            command.CommandText = sql;
            foreach (object? entry in entries)
            {
                if (entry == null)
                    continue;

                command.Parameters.Clear();
                SetCommandParameters(command, (T)entry);
                result += await command.ExecuteNonQueryAsync();
            }
            transaction.Commit();
            if (_log.IsDebugEnabled)
                _log.Debug($"Deleted {result} entries from {tableName} table.");

            RaiseSuccess(entries);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to delete from {tableName} table.", e);
            transaction.Rollback();
            RaiseFailed(entries, e);
        }
        finally
        {
            command?.Dispose();
            transaction.Dispose();
        }

        await connection.CloseAsync();
        return result;
    }

    public void Dispose()
    {
        _properties.Clear();
    }

    public string GetDropTableAndIndexSql(string? tableNameOverride = null)
    {
        var tableName = tableNameOverride ?? DefaultTableName;
        if (DropTableAndIndexSqls.TryGetValue(tableName, out var dropSql) && !dropSql.IsBlank())
        {
            return dropSql;
        }
        dropSql = _db.SqlHelper.CreateDropTableAndIndexSql<T>(tableName);
        DropTableAndIndexSqls[tableName] = dropSql;
        return dropSql;
    }

    protected virtual async Task<SqliteConnection> Connect()
    {
        var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    private string GetDeleteSql(string? tableNameOverride = null)
    {
        if (_uniqueKeyNames.IsNullOrEmpty())
            throw new InvalidOperationException("Auto SQL generation for DELETE is not supported if a type has no unique key columns.");

        var tableName = tableNameOverride ?? DefaultTableName;
        if (DeleteSqls.TryGetValue(tableName, out var deleteSql) && !deleteSql.IsBlank())
        {
            return deleteSql;
        }
        deleteSql = _db.SqlHelper.CreateDeleteSql<T>(tableNameOverride: tableName);
        DeleteSqls[tableName] = deleteSql;
        return deleteSql;
    }

    private string GetInsertSql(string? tableName = null)
    {
        if (_properties.IsNullOrEmpty())
            return "";

        tableName ??= DefaultTableName;

        if (InsertSqls.TryGetValue(tableName, out var insertSql) && !insertSql.IsBlank())
        {
            return insertSql;
        }

        insertSql = _db.SqlHelper.CreateInsertSql<T>(_placeholderPrefix, tableName);
        InsertSqls[tableName] = insertSql;
        return insertSql;
    }

    private string GetUpsertSql(string? tableName = null)
    {
        if (_properties.IsNullOrEmpty())
            return "";

        tableName ??= DefaultTableName;

        if (UpsertSqls.TryGetValue(tableName, out var upsertSql) && !upsertSql.IsBlank())
            return upsertSql;

        upsertSql = _db.SqlHelper.CreateUpsertSql<T>(_placeholderPrefix, tableName);
        UpsertSqls[tableName] = upsertSql;
        return upsertSql;
    }

    private bool TryAutoIncrement(string name, out long newValue)
    {
        newValue = long.MinValue;
        if (AutoIncrementOnInsertFieldNames.Contains(name))
        {
            var maxId = _db.GetMax(name, DefaultTableName, _databaseName);
            newValue = maxId.IsValid() ? maxId + 1 : 1;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Set db command parameters using reflection with field names and values in the <paramref name="entry"/>.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="entry"></param>
    /// <param name="targetFieldNames"></param>
    private void SetCommandParameters(DbCommand command, T entry, params string[] targetFieldNames)
    {
        targetFieldNames = targetFieldNames.IsNullOrEmpty() ? _targetFieldNames : targetFieldNames;

        foreach (var name in targetFieldNames)
        {
            var placeholder = _targetFieldNamePlaceHolders[name];

            var (value, valueType) = _valueGetter.GetTypeAndValue(entry, name);
            value = TryAutoIncrement(name, out var a) ? a : value;
            value ??= DBNull.Value;
            // always convert enum value as upper string
            if (valueType.IsEnum)
            {
                command.Parameters.Add(new SqliteParameter(placeholder, value.ToString()!.ToUpperInvariant()));
            }
            else if (!valueType.IsSqlNativeType() && !ReflectionUtils.GetAttributeInfo<T>().IsDatabaseIgnored(name))
            {
                var text = Json.Serialize(value);
                command.Parameters.Add(new SqliteParameter(placeholder, text));
            }
            else
            {
                command.Parameters.Add(new SqliteParameter(placeholder, value));
            }
        }
    }

    private void RaiseSuccess(object entry, [CallerMemberName] string methodName = "")
    {
        Success?.Invoke(entry, methodName);
    }

    private void RaiseFailed(object entry, Exception e, [CallerMemberName] string methodName = "")
    {
        Failed?.Invoke(entry, e, methodName);
    }
}

public interface ISqlWriter
{
    Task<int> UpsertOne<T>(T entry, string? tableNameOverride = null);
    Task<int> InsertOne<T>(T entry, string? tableNameOverride = null);
    Task<int> InsertMany<T>(IList<T> entries, bool isUpsert, string? tableNameOverride = null);
    Task<int> DeleteOne<T>(T entry, string? tableNameOverride = null);
    Task<int> DeleteMany<T>(IList<T> entries, string? tableNameOverride = null);
    Task<int> MoveOne<T1>(T1 entry, bool isUpsert, string fromTableName, string toTableName);

    event Action<object, string> Success;

    event Action<object, Exception, string> Failed;
}