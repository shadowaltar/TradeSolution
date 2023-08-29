using log4net;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Reflection;
using System.Text;
using TradeCommon.Database;
using TradeCommon.Utils.Attributes;
using TradeCommon.Utils.Common;

namespace Common;

public class SqlWriter<T> : ISqlWriter, IDisposable where T : new()
{
    private static readonly ILog _log = Logger.New();

    private readonly List<string> _targetFieldNames;
    private readonly Dictionary<string, string> _targetFieldNamePlaceHolders;
    private readonly Dictionary<string, PropertyInfo> _properties;
    private readonly ValueGetter<T> _valueGetter;

    private readonly string _tableName;
    private readonly string _databasePath;
    private readonly string _databaseName;

    public string InsertSql { get; private set; } = "";

    public string UpsertSql { get; private set; } = "";

    public SqlWriter(string tableName,
                     string databasePath,
                     string databaseName,
                     char placeholderPrefix = '$')
    {
        tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));

        if (!Directory.Exists(databasePath))
            Directory.CreateDirectory(databasePath);

        _properties = ReflectionUtils.GetPropertyToName(typeof(T)).ShallowCopy();
        _targetFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<UpsertIgnoreAttribute>() == null)
            .Select(pair => pair.Key).ToList();
        _targetFieldNamePlaceHolders = _targetFieldNames.ToDictionary(fn => fn, fn => placeholderPrefix + fn);
        _valueGetter = ReflectionUtils.GetValueGetter<T>();

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
            sql ??= GetInsertSql(isUpsert);
            command.CommandText = sql;
            foreach (var entry in entries)
            {
                if (entry == null)
                    continue;

                command.Parameters.Clear();
                foreach (var fn in _targetFieldNames)
                {
                    var placeholder = _targetFieldNamePlaceHolders[fn];
                    var value = _valueGetter.Get(entry.Cast<T>(), fn);
                    value ??= DBNull.Value;
                    command.Parameters.AddWithValue(placeholder, value);
                }
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

    public async Task<int> InsertOne<T1>(T1 entry, bool isUpsert, string? sql = null)
    {
        var result = 0;
        if (typeof(T1) != typeof(T)) throw new InvalidOperationException();

        using var connection = await Connect();

        var count = 0;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            sql ??= GetInsertSql(isUpsert);
            command.CommandText = sql;

            foreach (var fn in _targetFieldNames)
            {
                if (entry == null)
                    continue;

                var placeholder = _targetFieldNamePlaceHolders[fn];
                var value = _valueGetter.Get(entry.Cast<T>(), fn);
                value ??= DBNull.Value;
                // by default treat enum value as upper string
                if (value.GetType().IsEnum)
                    command.Parameters.AddWithValue(placeholder, value.ToString().ToUpperInvariant());
                else
                    command.Parameters.AddWithValue(placeholder, value);
            }
            count++;
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

    public void Dispose()
    {
        _properties.Clear();
    }

    private string GetInsertSql(bool isUpsert)
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

        var insertIgnoreFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<InsertIgnoreAttribute>() != null)
            .Select(pair => pair.Key).ToList();
        var upsertConflictKeyFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<UpsertConflictKeyAttribute>() != null)
            .Select(pair => pair.Key).ToList();
        var upsertConflictValueFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<UpsertConflictKeyAttribute>() == null)
            .Select(pair => pair.Key).ToList();
        var upsertConflictExcludeValueFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<UpsertIgnoreAttribute>() != null)
            .Select(pair => pair.Key).ToList();

        // INSERT INTO (...)
        var sb = new StringBuilder()
            .Append("INSERT INTO ").AppendLine(_tableName).Append('(');
        foreach (var name in _targetFieldNames)
        {
            if (insertIgnoreFieldNames.Contains(name))
                continue;
            sb.Append(name).Append(",");
        }
        sb.RemoveLast();
        sb.Append(')').AppendLine();

        // VALUES (...)
        sb.AppendLine("VALUES").AppendLine().Append('(');
        foreach (var name in _targetFieldNames)
        {
            if (insertIgnoreFieldNames.Contains(name))
                continue;
            sb.Append(_targetFieldNamePlaceHolders[name]).Append(",");
        }
        sb.RemoveLast();
        sb.Append(')').AppendLine();

        if (isUpsert && !upsertConflictKeyFieldNames.IsNullOrEmpty())
        {
            // ON CONFLICT (...)
            sb.Append("ON CONFLICT (");
            foreach (var fn in upsertConflictKeyFieldNames)
            {
                sb.Append(fn).Append(",");
            }
            sb.RemoveLast();
            sb.Append(')').AppendLine();

            // DO UPDATE SET ...
            sb.Append("DO UPDATE SET ");
            foreach (var fn in upsertConflictValueFieldNames)
            {
                if (upsertConflictExcludeValueFieldNames.Contains(fn))
                    continue;

                sb.Append(fn).Append(" = excluded.").Append(fn).Append(',');
            }
            sb.RemoveLast();
            UpsertSql = sb.ToString();
            return UpsertSql;
        }
        else
        {
            InsertSql = sb.ToString();
            return InsertSql;
        }
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
}