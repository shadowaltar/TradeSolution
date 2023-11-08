using Common;
using Common.Attributes;
using log4net;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using TradeCommon.Constants;

namespace Common.Database;

public class SqlReader<T> : IDisposable where T : new()
{
    private static readonly ILog _log = Logger.New();
    private static readonly Dictionary<Type, string> _selectClauses = new();
    private Dictionary<string, PropertyInfo> _properties;
    private ValueSetter<T> _valueSetter;

    public SqlReader(DbDataReader reader)
    {
        Reader = reader;
        Columns = reader.GetSchemaTable()!.GetDistinctValues<string>("ColumnName");
        _properties = ReflectionUtils.GetPropertyToName(typeof(T));
        _valueSetter = ReflectionUtils.GetValueSetter<T>();
    }

    public DbDataReader Reader { get; private set; }

    /// <summary>
    /// Gets the column names from the reader schema.
    /// </summary>
    public HashSet<string> Columns { get; private set; }

    /// <summary>
    /// Read an entry and store in <typeparamref name="T"/>.
    /// </summary>
    /// <returns></returns>
    public T Read()
    {
        var entry = new T();
        foreach (var name in _valueSetter.GetNames())
        {
            if (!Columns.Contains(name))
                continue; // skip when schema is available and property name doesn't exist in returned columns

            if (_properties.TryGetValue(name, out var pi))
            {
                var type = pi.PropertyType;
                var isAsJson = _valueSetter.AttributeInfo.IsAsJson(name);
                var value = isAsJson ? GetFromJson(type, name) : Get(type, name);

                _valueSetter.Set(entry, name, value);
            }
        }
        return entry;
    }

    private object? GetFromJson(Type type, string name)
    {
        try
        {
            var stringVal = Reader.SafeGetString(name);
            return Json.Deserialize(type, stringVal!);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to deserialize a field value for {name} into json.", e);
            return null;
        }
    }

    /// <summary>
    /// Get a value from the <see cref="Reader"/>.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    private object? Get(Type type, string name)
    {
        if (type == typeof(string))
            return Reader.SafeGetString(name);
        else if (type == typeof(decimal))
            return Reader.GetDecimal(name);
        else if (type == typeof(long))
            return Reader.GetInt64(name);
        else if (type == typeof(double))
            return Reader.GetDouble(name);
        else if (type == typeof(DateTime))
            return Reader.GetDateTime(name);
        else if (type == typeof(int))
            return Reader.GetInt32(name);
        else if (type == typeof(bool))
            return Reader.GetBoolean(name);
        else if (type == typeof(decimal?))
            return Reader.SafeGetDecimal(name);
        else if (type == typeof(long?))
            return Reader.SafeGetLong(name);
        else if (type == typeof(double?))
            return Reader.SafeGetDouble(name);
        else return type == typeof(DateTime?)
            ? Reader.SafeGetDateTime(name)
            : type == typeof(int?)
            ? Reader.SafeGetInt(name)
            : type == typeof(bool?)
            ? Reader.SafeGetBool(name)
            : type.IsEnum
            ? Enum.TryParse(type, Reader.SafeGetString(name), true, out var result) ? result : Activator.CreateInstance(type)
            : throw new NotImplementedException("Unsupported type: " + type.Name);
    }

    public TV? GetOrDefault<TV>(string columnName, TV? defaultValue = default)
    {
        return !Columns.Contains(columnName) ? defaultValue : (TV?)Get(typeof(TV), columnName);
    }

    public static string GetSelectClause()
    {
        if (!_selectClauses.TryGetValue(typeof(T), out var clause))
        {
            var properties = ReflectionUtils.GetPropertyToName(typeof(T));
            var selectIgnoreFieldNames = properties.Where(pair =>
            {
                var ignoreAttr = pair.Value.GetCustomAttribute<DatabaseIgnoreAttribute>();
                return ignoreAttr != null && ignoreAttr.IgnoreSelect;
            }).Select(pair => pair.Key).ToList();

            var sb = new StringBuilder("SELECT ");
            foreach (var propertyName in properties.Keys)
            {
                if (selectIgnoreFieldNames.Contains(propertyName))
                    continue;
                sb.Append(propertyName).Append(',');
            }
            sb.RemoveLast();
            clause = sb.ToString();
            _selectClauses[typeof(T)] = clause;
        }
        return clause;
    }

    public void Dispose()
    {
        Columns.Clear();
        Reader = null;
        _properties = null;
        _valueSetter = null;
    }
}


public static class SqlReader
{
    private static readonly ILog _log = Logger.New();

    /// <summary>
    /// Read a table and parse the results into a list by a given transformation function.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="tableName"></param>
    /// <param name="databaseName"></param>
    /// <param name="sql"></param>
    /// <param name="transformFunc"></param>
    /// <param name="parameterValues"></param>
    /// <returns></returns>
    public static async Task<List<T>> ReadMany<T>(string tableName, string databaseName, string environmentString, string sql, Func<SqliteDataReader, T> transformFunc, params (string key, object? value)[] parameterValues)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var connection = await Connect(databaseName, environmentString);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach ((string key, object? value) in parameterValues)
            {
                command.Parameters.AddWithValue(key, value);
            }
            var results = new List<T>();
            using var r = await command.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var entry = transformFunc(r);
                results.Add(entry);
            }
            sw.Stop();
            if (_log.IsDebugEnabled)
                _log.Debug($"[{sw.Elapsed.TotalSeconds:F4}ms] Read {results.Count} entries from {tableName} table in {databaseName}.");
            await connection.CloseAsync();
            return results;
        }
        catch (Exception e)
        {
            _log.Error("Failed to read one.", e);
            return new();
        }
    }

    /// <summary>
    /// Read a table and parse the results into a list automatically.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="tableName"></param>
    /// <param name="databaseName"></param>
    /// <param name="sql"></param>
    /// <param name="parameterValues"></param>
    /// <returns></returns>
    public static async Task<List<T>> ReadMany<T>(string tableName,
                                                  string databaseName,
                                                  string environmentString,
                                                  string sql,
                                                  params (string key, object? value)[] parameterValues) where T : new()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var connection = await Connect(databaseName, environmentString);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach ((string key, object? value) in parameterValues)
            {
                command.Parameters.AddWithValue(key, value);
            }
            var results = new List<T>();
            using var r = await command.ExecuteReaderAsync();
            using var sqlHelper = new SqlReader<T>(r);
            while (await r.ReadAsync())
            {
                var entry = sqlHelper.Read();
                results.Add(entry);
            }
            if (_log.IsDebugEnabled)
                _log.Debug($"[{sw.Elapsed.TotalSeconds:F4}ms] Read {results.Count} entries from {tableName} table in {databaseName}.");
            await connection.CloseAsync();
            return results;
        }
        catch (Exception e)
        {
            _log.Error("Failed to read one.", e);
            return new();
        }
    }

    /// <summary>
    /// Read a table and parse the results into a list by a given transformation function.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="tableName"></param>
    /// <param name="databaseName"></param>
    /// <param name="sql"></param>
    /// <param name="transformFunc"></param>
    /// <param name="parameterValues"></param>
    /// <returns></returns>
    public static async Task<T?> ReadOne<T>(string tableName, string databaseName, string environmentString, string sql, Func<SqliteDataReader, T> transformFunc, params (string key, object? value)[] parameterValues)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var connection = await Connect(databaseName, environmentString);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach ((string key, object? value) in parameterValues)
            {
                command.Parameters.AddWithValue(key, value);
            }
            var results = new List<T>();
            using var r = await command.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var entry = transformFunc(r);
                if (_log.IsDebugEnabled)
                    _log.Debug($"[{sw.Elapsed.TotalSeconds:F4}ms] Read 1 entry from {tableName} table in {databaseName}.");
                await connection.CloseAsync();
                return entry;
            }
            return default;
        }
        catch (Exception e)
        {
            _log.Error("Failed to read one.", e);
            return default;
        }
    }

    /// <summary>
    /// Read a table and parse the results into a list automatically.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="tableName"></param>
    /// <param name="databaseName"></param>
    /// <param name="sql"></param>
    /// <param name="parameterValues"></param>
    /// <returns></returns>
    public static async Task<T?> ReadOne<T>(string tableName, string databaseName, string environmentString, string sql, params (string key, object? value)[] parameterValues) where T : new()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var connection = await Connect(databaseName, environmentString);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach ((string key, object? value) in parameterValues)
            {
                command.Parameters.AddWithValue(key, value);
            }
            using var r = await command.ExecuteReaderAsync();
            using var sqlHelper = new SqlReader<T>(r);
            while (await r.ReadAsync())
            {
                var entry = sqlHelper.Read();
                if (_log.IsDebugEnabled)
                    _log.Debug($"[{sw.Elapsed.TotalSeconds:F4}ms] Read 1 entry from {tableName} table in {databaseName}.");
                await connection.CloseAsync();
                return entry;
            }
            return default;
        }
        catch (Exception e)
        {
            _log.Error("Failed to read one.", e);
            return default;
        }
    }

    private static string? GetConnectionString(string databaseName, string environmentString)
    {
        return $"Data Source={Path.Combine(Consts.DatabaseFolder, environmentString, databaseName)}.db";
    }

    private static async Task<SqliteConnection> Connect(string database, string environmentString)
    {
        var conn = new SqliteConnection(GetConnectionString(database, environmentString));
        await conn.OpenAsync();
        return conn;
    }
}