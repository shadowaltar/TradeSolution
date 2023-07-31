using log4net;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Reflection;
using System.Text;
using TradeCommon.Utils.Attributes;

namespace Common;

public class SqlWriter<T> : IDisposable where T : new()
{
    private static readonly ILog _log = Logger.New();

    private readonly List<string> _targetFieldNames;
    private readonly Dictionary<string, string> _targetFieldNamePlaceHolders;
    private Dictionary<string, PropertyInfo> _properties;
    private ValueGetter<T> _valueGetter;

    private readonly string _tableName;
    private readonly string _databasePath;
    private readonly string _databaseName;

    public string? Sql { get; private set; }

    public SqlWriter(string tableName, string databasePath, string databaseName,
        string? sql = null,
        char placeholderPrefix = '$')
    {
        tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));

        Sql = sql;

        _properties = ReflectionUtils.GetPropertyToName(typeof(T));
        _targetFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<UpsertIgnoreAttribute>() == null)
            .Select(pair => pair.Key).ToList();
        _targetFieldNamePlaceHolders = _targetFieldNames.ToDictionary(fn => placeholderPrefix + fn, fn => fn);
        _valueGetter = ReflectionUtils.GetValueGetter<T>();

        _tableName = tableName;
        _databasePath = databasePath;
        _databaseName = databaseName;
    }

    public void Dispose()
    {
        _properties = null;
        _valueGetter = null;
    }

    private string? GetConnectionString()
    {
        return $"Data Source={Path.Combine(_databasePath, _databaseName)}.db";
    }

    public string GetUpsertSql()
    {
        if (_properties.IsNullOrEmpty())
            return "";

        var upsertConflictKeyFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<UpsertConflictKeyAttribute>() != null)
            .Select(pair => pair.Key).ToList();
        var upsertConflictValueFieldNames = _properties.Where(pair => pair.Value.GetCustomAttribute<UpsertConflictKeyAttribute>() == null)
            .Select(pair => pair.Key).ToList();

        // INSERT INTO (...)
        var sb = new StringBuilder();
        sb.Append("INSERT INTO ").AppendLine(_tableName)
            .Append("(");
        foreach (var name in _targetFieldNames)
        {
            sb.Append(name).Append(", ");
        }
        sb.Remove(sb.Length - 1, 1);
        sb.AppendLine(")");

        // VALUES (...)
        sb.AppendLine("VALUES").AppendLine().Append("(");
        foreach (var name in _targetFieldNames)
        {
            sb.Append(_targetFieldNamePlaceHolders[name]).Append(", ");
        }
        sb.Remove(sb.Length - 1, 1);
        sb.AppendLine(")");

        // ON CONFLICT (...)
        sb.Append("ON CONFLICT (");
        foreach (var fn in upsertConflictKeyFieldNames)
        {
            sb.Append(fn).Append(", ");
        }
        sb.Remove(sb.Length - 1, 1);
        sb.AppendLine(")");

        // DO UPDATE SET ...
        sb.Append("DO UPDATE SET ");
        foreach (var fn in upsertConflictValueFieldNames)
        {
            sb.Append(fn).Append(" = excluded.").Append(fn).Append(',');
        }
        sb.Remove(sb.Length - 1, 1);

        var sql = sb.ToString();

        _log.Info("Sql for upsert: " + sql);
        return sql;
    }

    public async Task InsertMany(IList<T> entries, string? sql = null)
    {
        using var connection = await Connect(_databaseName);
        using var transaction = connection.BeginTransaction();

        var count = 0;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            Sql ??= sql ?? GetUpsertSql();
            command.CommandText = Sql;
            foreach (var entry in entries)
            {
                command.Parameters.Clear();
                foreach (var fn in _targetFieldNames)
                {
                    var placeholder = _targetFieldNamePlaceHolders[fn];
                    var value = _valueGetter.Get(entry, fn);
                    command.Parameters.AddWithValue(placeholder, value);
                }
                count++;
                await command.ExecuteNonQueryAsync();
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
    }

    public async Task InsertOne(T entry, string? sql = null)
    {
        using var connection = await Connect(_databaseName);

        var count = 0;
        SqliteCommand? command = null;
        try
        {
            command = connection.CreateCommand();
            Sql ??= sql ?? GetUpsertSql();
            command.CommandText = Sql;

            foreach (var fn in _targetFieldNames)
            {
                var placeholder = _targetFieldNamePlaceHolders[fn];
                var value = _valueGetter.Get(entry, fn);
                command.Parameters.AddWithValue(placeholder, value);
            }
            count++;
            await command.ExecuteNonQueryAsync();

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
    }

    private async Task<SqliteConnection> Connect(string database)
    {
        var conn = new SqliteConnection(GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }
}

