using log4net;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Reflection;
using System.Text;
using TradeCommon.Database;
using TradeCommon.Utils.Common;
using Common.Attributes;
using TradeCommon.Runtime;
using Microsoft.Identity.Client.Extensions.Msal;
using TradeCommon.Constants;
using System;
using Utilities;
using System.Windows.Input;
using System.Data.Common;

namespace Common;

public class SqlWriter<T> : ISqlWriter, IDisposable where T : new()
{
    private static readonly ILog _log = Logger.New();

    private readonly List<string> _targetFieldNames;
    private readonly Dictionary<string, string> _targetFieldNamePlaceHolders;
    private readonly Dictionary<string, PropertyInfo> _properties;
    private readonly string[] _uniqueKeyNames;
    private readonly ValueGetter<T> _valueGetter;

    public List<string> AutoIncrementOnInsertFieldNames { get; }

    private readonly IStorage _storage;
    private readonly string _tableName;
    private readonly string _databasePath;
    private readonly string _databaseName;
    private readonly char _placeholderPrefix;

    public string InsertSql { get; private set; } = "";

    public string UpsertSql { get; private set; } = "";

    public string DeleteSql { get; private set; } = "";

    public string DropTableAndIndexSql { get; private set; } = "";

    public string CreateTableAndIndexSql { get; private set; } = "";

    public SqlWriter(IStorage storage,
                     string tableName,
                     string databasePath,
                     string databaseName,
                     char placeholderPrefix = Consts.SqlCommandPlaceholderPrefix)
    {
        tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));

        if (!Directory.Exists(databasePath))
            Directory.CreateDirectory(databasePath);

        _placeholderPrefix = placeholderPrefix;
        _properties = ReflectionUtils.GetPropertyToName(typeof(T)).ShallowCopy();
        // only the 'primary' (the 1st) unique attribute will be used as the members
        // for UNIQUE() clause
        // the other unique attributes are only for indexes
        _uniqueKeyNames = typeof(T).GetCustomAttributes<UniqueAttribute>()
            .FirstOrDefault()?.FieldNames ?? Array.Empty<string>();
        _targetFieldNames = _properties.Select(pair => pair.Key).ToList();
        _targetFieldNamePlaceHolders = _targetFieldNames.ToDictionary(fn => fn, fn => _placeholderPrefix + fn);

        AutoIncrementOnInsertFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<AutoIncrementOnInsertAttribute>() != null)
            .Select(pair => pair.Key).ToList();

        _valueGetter = ReflectionUtils.GetValueGetter<T>();

        _storage = storage;
        _tableName = tableName;
        _databasePath = databasePath;
        _databaseName = databaseName;
    }

    public async Task<int> InsertMany<T1>(IList<T1> entries, bool isUpsert, string? sql = null)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();

        using var connection = await Connect();
        using var transaction = connection.BeginTransaction();

        var count = 0;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            sql ??= GetInsertSql(isUpsert, _tableName);
            command.CommandText = sql;
            foreach (object? entry in entries)
            {
                if (entry == null)
                    continue;

                command.Parameters.Clear();
                SetCommandParameters(command, (T)entry);

                count++;
                result += await command.ExecuteNonQueryAsync();
            }
            transaction.Commit();
            _log.Info($"Upserted {count} entries into {_tableName} table.");
        }
        catch (Exception e)
        {
            _log.Error($"Failed to upsert into {_tableName} table.", e);
            transaction.Rollback();
        }
        finally
        {
            command?.Dispose();
        }

        await connection.CloseAsync();
        return result;
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

    public async Task<int> UpsertOne<T1>(T1 entry, string? sql = null)
    {
        return await InsertOne<T1>(entry, true, sql);
    }

    public async Task<int> InsertOne<T1>(T1 entry, bool isUpsert, string? sql = null)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();
        if (entry == null)
            return 0;

        using var connection = await Connect();
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            sql ??= GetInsertSql(isUpsert, _tableName);
            command.CommandText = sql;
            var e = (T)(object)entry;

            SetCommandParameters(command, e);
            result = await command.ExecuteNonQueryAsync();

            _log.Info($"Upserted 1 entry into {_tableName} table.");
        }
        catch (Exception e)
        {
            _log.Error($"Failed to upsert into {_tableName} table.", e);
        }
        finally
        {
            command?.Dispose();
        }

        await connection.CloseAsync();
        return result;
    }

    private bool TryAutoIncrement(string name, out long newValue)
    {
        newValue = long.MinValue;
        if (AutoIncrementOnInsertFieldNames.Contains(name))
        {
            var maxId = AsyncHelper.RunSync(() => _storage.GetMax(name, _tableName, _databaseName));
            newValue = maxId.IsValid() ? maxId + 1 : 1;
            return true;
        }
        return false;
    }

    public async Task<int> DeleteOne<T1>(T1 entry, string? sql = null)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();

        using var connection = await Connect();
        using var transaction = connection.BeginTransaction();
        var count = 0;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            sql ??= GetDeleteSql();
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

            _log.Info($"Deleted 1 entry from {_tableName} table.");
            transaction.Commit();
        }
        catch (Exception e)
        {
            _log.Error($"Failed to delete from {_tableName} table.", e);
            transaction.Rollback();
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

    private string GetDeleteSql()
    {
        if (_uniqueKeyNames.IsNullOrEmpty())
            throw new InvalidOperationException("Auto SQL generation for DELETE is not supported if a type has no unique key columns.");
        if (!DeleteSql.IsNullOrEmpty())
        {
            return DeleteSql;
        }
        var sb = new StringBuilder("DELETE FROM ")
            .Append(_tableName)
            .Append(" WHERE ");
        for (int i = 0; i < _uniqueKeyNames.Length; i++)
        {
            string? name = _uniqueKeyNames[i];
            sb.Append(name).Append(" = ").Append(_targetFieldNamePlaceHolders[name]);
            if (i != _uniqueKeyNames.Length - 1)
                sb.Append(" AND ");
        }
        DeleteSql = sb.ToString();
        return DeleteSql;
    }

    private string GetInsertSql(bool isUpsert, string? tableNameOverride = null)
    {
        if (_properties.IsNullOrEmpty())
            return "";

        if (isUpsert && !UpsertSql.IsNullOrEmpty())
        {
            return UpsertSql;
        }

        if (!isUpsert && !InsertSql.IsNullOrEmpty())
        {
            return InsertSql;
        }

        if (isUpsert)
        {
            UpsertSql = _storage.CreateInsertSql<T>(_placeholderPrefix, isUpsert, tableNameOverride);
            return UpsertSql;
        }
        else
        {
            InsertSql = _storage.CreateInsertSql<T>(_placeholderPrefix, isUpsert, tableNameOverride);
            return InsertSql;
        }
    }

    public string GetDropTableAndIndexSql()
    {
        if (!DropTableAndIndexSql.IsNullOrEmpty())
        {
            return DropTableAndIndexSql;
        }
        DropTableAndIndexSql = _storage.CreateDropTableAndIndexSql<T>();
        return DropTableAndIndexSql;
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
}

public interface ISqlWriter
{
    Task<int> InsertMany<T>(IList<T> entries, bool isUpsert, string? sql = null);

    Task<int> InsertOne<T>(T entry, bool isUpsert, string? sql = null);

    Task<int> DeleteOne<T>(T entry, string? sql = null);
}