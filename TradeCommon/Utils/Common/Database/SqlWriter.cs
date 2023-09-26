using Common.Attributes;
using log4net;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
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

    private readonly List<string> _targetFieldNames;
    private readonly Dictionary<string, string> _targetFieldNamePlaceHolders;
    private readonly Dictionary<string, PropertyInfo> _properties;
    private readonly string[] _uniqueKeyNames;
    private readonly ValueGetter<T> _valueGetter;

    public List<string> AutoIncrementOnInsertFieldNames { get; }

    private readonly IStorage _storage;
    private readonly string _defaultTableName;
    private readonly string _databasePath;
    private readonly string _databaseName;
    private readonly char _placeholderPrefix;

    public event Action<object, string> Success;
    public event Action<object, Exception, string> Failed;

    public Dictionary<string, string> InsertSqls { get; } = new();

    public Dictionary<string, string> UpsertSqls { get; } = new();

    public Dictionary<string, string> DeleteSqls { get; } = new();

    public Dictionary<string, string> DropTableAndIndexSqls { get; } = new();

    public Dictionary<string, string> CreateTableAndIndexSqls { get; } = new();

    public SqlWriter(IStorage storage, char placeholderPrefix = Consts.SqlCommandPlaceholderPrefix)
    {
        var (t, d) = DatabaseNames.GetTableAndDatabaseName<T>();
        _defaultTableName = t ?? throw new ArgumentNullException("Default table name");
        _databasePath = Consts.DatabaseFolder;
        _databaseName = d;

        if (!Directory.Exists(_databasePath))
            Directory.CreateDirectory(_databasePath);

        _placeholderPrefix = placeholderPrefix;
        _properties = ReflectionUtils.GetPropertyToName(typeof(T)).ShallowCopy();
        // only the 'primary' (the 1st) unique attribute will be used as the members
        // for UNIQUE() clause
        // the other unique attributes are only for indexes
        _uniqueKeyNames = ReflectionUtils.GetAttributeInfo<T>().PrimaryUniqueKey.ToArray();
        _targetFieldNames = _properties.Select(pair => pair.Key).ToList();
        _targetFieldNamePlaceHolders = _targetFieldNames.ToDictionary(fn => fn, fn => _placeholderPrefix + fn);

        AutoIncrementOnInsertFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<AutoIncrementOnInsertAttribute>() != null)
            .Select(pair => pair.Key).ToList();

        _valueGetter = ReflectionUtils.GetValueGetter<T>();

        _storage = storage;
    }

    public async Task<int> InsertMany<T1>(IList<T1> entries, bool isUpsert, string? tableNameOverride = null)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();

        using var connection = await Connect();
        using var transaction = connection.BeginTransaction();

        var tableName = tableNameOverride ?? _defaultTableName;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            var sql = GetInsertSql(isUpsert, tableName);
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
            _log.Info($"Upserted {result} entries into {tableName} table.");

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

    public async Task<int> UpsertOne<T1>(T1 entry, string? tableNameOverride = null)
    {
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();
        return await InsertOne(entry, true, tableNameOverride);
    }

    public async Task<int> InsertOne<T1>(T1 entry, bool isUpsert, string? tableNameOverride = null)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();
        if (entry == null)
            return 0;

        var tableName = tableNameOverride ?? _defaultTableName;
        using var connection = await Connect();
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            var sql = GetInsertSql(isUpsert, tableName);
            command.CommandText = sql;
            var e = (T)(object)entry;

            SetCommandParameters(command, e);
            result = await command.ExecuteNonQueryAsync();

            _log.Info($"Upserted 1 {typeof(T).Name} entry into {tableName} table.");

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

    private void RaiseSuccess(object entry, [CallerMemberName] string methodName = "")
    {
        Success?.Invoke(entry, methodName);
    }

    private void RaiseFailed(object entry, Exception e, [CallerMemberName] string methodName = "")
    {
        Failed?.Invoke(entry, e, methodName);
    }

    public async Task<int> DeleteOne<T1>(T1 entry, string? tableNameOverride = null)
    {
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();

        var result = 0;
        var tableName = tableNameOverride ?? _defaultTableName;
        using var connection = await Connect();
        using var transaction = connection.BeginTransaction();
        var count = 0;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            var sql = GetDeleteSql(tableName);
            command.CommandText = sql;

            foreach (var name in _uniqueKeyNames)
            {
                if (entry == null)
                    continue;

                var placeholder = _targetFieldNamePlaceHolders[name];
                var value = _valueGetter.Get((T)(object)entry, name);
                if (value == null) // unique key columns should never be null
                    return -1;
                // by default treat enum value as upper string
                if (value.GetType().IsEnum)
                    command.Parameters.AddWithValue(placeholder, value.ToString()!.ToUpperInvariant());
                else
                    command.Parameters.AddWithValue(placeholder, value);
            }
            count++;
            result = await command.ExecuteNonQueryAsync();

            _log.Info($"Deleted 1 entry from {tableName} table.");
            transaction.Commit();
            RaiseSuccess(entry);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to delete from {tableName} table.", e);
            transaction.Rollback();
            RaiseFailed(entry, e);
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
        var tableName = tableNameOverride ?? _defaultTableName;
        if (DropTableAndIndexSqls.TryGetValue(tableName, out var dropSql) && !dropSql.IsNullOrEmpty())
        {
            return dropSql;
        }
        dropSql = _storage.SchemaHelper.CreateDropTableAndIndexSql<T>(tableName);
        DropTableAndIndexSqls[tableName] = dropSql;
        return dropSql;
    }

    protected virtual async Task<SqliteConnection> Connect()
    {
        var conn = new SqliteConnection(GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    protected virtual string? GetConnectionString()
    {
        return $"Data Source={Path.Combine(_databasePath, _databaseName)}.db";
    }

    private string GetDeleteSql(string? tableNameOverride = null)
    {
        if (_uniqueKeyNames.IsNullOrEmpty())
            throw new InvalidOperationException("Auto SQL generation for DELETE is not supported if a type has no unique key columns.");

        var tableName = tableNameOverride ?? _defaultTableName;
        if (DeleteSqls.TryGetValue(tableName, out var deleteSql) && !deleteSql.IsNullOrEmpty())
        {
            return deleteSql;
        }
        deleteSql = _storage.SchemaHelper.CreateDeleteSql<T>(tableNameOverride: tableName);
        DeleteSqls[tableName] = deleteSql;
        return deleteSql;
    }

    private string GetInsertSql(bool isUpsert, string? tableName = null)
    {
        if (_properties.IsNullOrEmpty())
            return "";

        tableName ??= _defaultTableName;

        if (isUpsert && UpsertSqls.TryGetValue(tableName, out var upsertSql) && !upsertSql.IsNullOrEmpty())
        {
            return upsertSql;
        }

        if (!isUpsert && InsertSqls.TryGetValue(tableName, out var insertSql) && !insertSql.IsNullOrEmpty())
        {
            return insertSql;
        }

        if (isUpsert)
        {
            upsertSql = _storage.SchemaHelper.CreateInsertSql<T>(_placeholderPrefix, isUpsert, tableName);
            UpsertSqls[tableName] = upsertSql;
            return upsertSql;
        }
        else
        {
            insertSql = _storage.SchemaHelper.CreateInsertSql<T>(_placeholderPrefix, isUpsert, tableName);
            InsertSqls[tableName] = insertSql;
            return insertSql;
        }
    }

    private bool TryAutoIncrement(string name, out long newValue)
    {
        newValue = long.MinValue;
        if (AutoIncrementOnInsertFieldNames.Contains(name))
        {
            var maxId = AsyncHelper.RunSync(() => _storage.GetMax(name, _defaultTableName, _databaseName));
            newValue = maxId.IsValid() ? maxId + 1 : 1;
            return true;
        }
        return false;
    }

    private void SetCommandParameters(DbCommand command, T entry)
    {
        foreach (var name in _targetFieldNames)
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
}

public interface ISqlWriter
{
    Task<int> InsertMany<T>(IList<T> entries, bool isUpsert, string? tableNameOverride = null);

    Task<int> InsertOne<T>(T entry, bool isUpsert, string? tableNameOverride = null);

    Task<int> UpsertOne<T>(T entry, string? tableNameOverride = null);

    Task<int> DeleteOne<T>(T entry, string? tableNameOverride = null);

    event Action<object, string> Success;
    event Action<object, Exception, string> Failed;
}