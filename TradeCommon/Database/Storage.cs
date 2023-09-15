using Autofac;
using Common;
using Common.Attributes;
using log4net;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Primitives;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Providers;
using TradeCommon.Runtime;
using TradeCommon.Utils.Common.Attributes;

namespace TradeCommon.Database;

public partial class Storage : IStorage
{
    private readonly ILog _log = Logger.New();
    private readonly Dictionary<DataType, ISqlWriter> _writers = new();
    private Func<int, Security>? _getSecurityFunction;

    public void Initialize(ISecurityDefinitionProvider securityService)
    {
        _getSecurityFunction = securityService.GetSecurity;
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

    /// <summary>
    /// Execute a query and return a <see cref="DataTable"/>. All values are in strings.
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="database"></param>
    /// <returns></returns>
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

    private string? GetConnectionString(string databaseName)
    {
        return $"Data Source={Path.Combine(DatabaseFolder, databaseName)}.db";
    }

    private async Task<SqliteConnection> Connect(string database)
    {
        var conn = new SqliteConnection(GetConnectionString(database));
        await conn.OpenAsync();
        return conn;
    }

    private Security? GetSecurity(int securityId)
    {
        return _getSecurityFunction?.Invoke(securityId);
    }

    public (string table, string? schema, string database) GetStorageNames<T>()
    {
        var attr = typeof(T).GetCustomAttribute<StorageAttribute>();
        if (attr == null)
            return default;
        return (attr.TableName, attr.SchemaName, attr.DatabaseName);
    }

    public string CreateInsertSql<T>(char placeholderPrefix, bool isUpsert)
    {
        var attr = typeof(T).GetCustomAttribute<StorageAttribute>();
        if (attr == null) throw new InvalidOperationException("Must provide table name.");

        var tableName = attr.TableName;
        var properties = ReflectionUtils.GetPropertyToName(typeof(T)).ShallowCopy();
        var uniqueKeyNames = typeof(T).GetCustomAttribute<UniqueAttribute>()!.FieldNames ?? Array.Empty<string>();
        var targetFieldNames = properties.Select(pair => pair.Key).ToList();
        var targetFieldNamePlaceHolders = targetFieldNames.ToDictionary(fn => fn, fn => placeholderPrefix + fn);

        var insertIgnoreFieldNames = properties.Where(pair =>
        {
            var ignoreAttr = pair.Value.GetCustomAttribute<DatabaseIgnoreAttribute>();
            if (ignoreAttr != null && ignoreAttr.IgnoreInsert) return true;
            return false;
        }).Select(pair => pair.Key).ToList();

        var upsertIgnoreFieldNames = properties.Where(pair =>
        {
            var ignoreAttr = pair.Value.GetCustomAttribute<DatabaseIgnoreAttribute>();
            if (ignoreAttr != null && ignoreAttr.IgnoreUpsert) return true;
            return false;
        }).Select(pair => pair.Key).ToList();

        // INSERT INTO (...)
        var sb = new StringBuilder()
            .Append("INSERT INTO ").AppendLine(tableName).Append('(');
        foreach (var name in targetFieldNames)
        {
            if (insertIgnoreFieldNames.Contains(name))
                continue;
            sb.Append(name).Append(",");
        }
        sb.RemoveLast();
        sb.Append(')').AppendLine();

        // VALUES (...)
        sb.AppendLine("VALUES").AppendLine().Append('(');
        foreach (var name in targetFieldNames)
        {
            if (insertIgnoreFieldNames.Contains(name))
                continue;
            sb.Append(targetFieldNamePlaceHolders[name]).Append(",");
        }
        sb.RemoveLast();
        sb.Append(')').AppendLine();

        if (isUpsert && !uniqueKeyNames.IsNullOrEmpty())
        {
            // ON CONFLICT (...)
            sb.Append("ON CONFLICT (");
            foreach (var fn in uniqueKeyNames)
            {
                sb.Append(fn).Append(',');
            }
            sb.RemoveLast();
            sb.Append(')').AppendLine();

            // DO UPDATE SET ...
            sb.Append("DO UPDATE SET ");
            foreach (var fn in targetFieldNames)
            {
                if (upsertIgnoreFieldNames.Contains(fn))
                    continue;
                if (uniqueKeyNames.Contains(fn))
                    continue;

                sb.Append(fn).Append(" = excluded.").Append(fn).Append(',');
            }
            sb.RemoveLast();
        }
        return sb.ToString();
    }

    public string CreateDropTableAndIndexSql<T>()
    {
        var type = typeof(T);
        var storageAttr = type.GetCustomAttribute<StorageAttribute>() ?? throw Exceptions.InvalidStorageDefinition();
        var table = storageAttr.TableName;
        var schema = storageAttr.SchemaName ?? "";
        var database = storageAttr.DatabaseName;
        if (table.IsBlank() || database.IsBlank()) throw Exceptions.InvalidStorageDefinition();
        if (!schema.IsBlank())
            table = $"{table}.{schema}";

        var sb = new StringBuilder();
        sb.Append($"DROP TABLE IF EXISTS ").AppendLine(table);

        var uniqueAttributes = type.GetCustomAttributes<UniqueAttribute>().ToList();
        var indexAttributes = type.GetCustomAttributes<IndexAttribute>().ToList();

        for (int i = 0; i < uniqueAttributes.Count; i++)
        {
            var attr = uniqueAttributes[i];
            sb.Append($"DROP UNIQUE INDEX IF EXISTS ")
                .Append("UX_").Append(table).Append('_')
                .AppendJoin('_', attr.FieldNames)
                .AppendLine();
        }
        for (int i = 0; i < indexAttributes.Count; i++)
        {
            var attr = indexAttributes[i];
            sb.Append($"DROP INDEX IF EXISTS ")
                .Append("IX_").Append(table).Append('_')
                .AppendJoin('_', attr.FieldNames)
                .AppendLine();
        }
        return sb.ToString();
    }

    public string CreateCreateTableAndIndexSql<T>()
    {
        var type = typeof(T);
        var properties = ReflectionUtils.GetPropertyToName(type);
        var storageAttr = type.GetCustomAttribute<StorageAttribute>() ?? throw Exceptions.InvalidStorageDefinition();
        var table = storageAttr.TableName;
        var schema = storageAttr.SchemaName ?? "";
        var database = storageAttr.DatabaseName;
        if (table.IsBlank() || database.IsBlank()) throw Exceptions.InvalidStorageDefinition();
        if (!schema.IsBlank())
            table = $"{table}.{schema}";

        var sb = new StringBuilder();
        sb.Append($"CREATE TABLE IF NOT EXISTS ")
            .Append(table).AppendLine(" (");

        foreach (var (name, property) in properties)
        {
            var typeString = TypeConverter.ToSqliteType(property.PropertyType);

            var isNotNull = false;
            var isPrimary = false;
            var attributes = property.GetCustomAttributes();
            foreach (var attr in attributes)
            {
                if (attr is AutoIncrementOnInsertAttribute)
                {
                    isPrimary = true;
                }
                if (attr is NotNullAttribute)
                {
                    isNotNull = true;
                }
            }

            sb.Append(name).Append(' ').Append(typeString);

            if (isPrimary)
                sb.Append(" PRIMARY KEY");
            else if (isNotNull)
                sb.Append(" NOT NULL");
        }

        return sb.ToString();
    }
}
