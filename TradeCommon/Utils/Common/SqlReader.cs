using Common;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;
using TradeCommon.Utils.Attributes;

namespace Common;

public class SqlReader<T> : IDisposable where T : new()
{
    private Dictionary<string, PropertyInfo> _properties;
    private ValueSetter<T> _valueSetter;

    private static Dictionary<Type, string> _selectClauses = new();

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
        foreach (var name in _valueSetter.GetFieldNames())
        {
            if (!Columns.Contains(name))
                continue; // skip when schema is available and property name doesn't exist in returned columns

            if (_properties.TryGetValue(name, out var pi))
            {
                var type = pi.PropertyType;
                _valueSetter.Set(entry, name, Get(type, name));
            }
        }
        return entry;
    }

    /// <summary>
    /// Get a value from the <see cref="Reader"/>.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private object? Get(Type type, string name)
    {
        if (type == typeof(string))
            return Reader.SafeGetString(name);
        else if (type == typeof(decimal))
            return Reader.GetDecimal(name);
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
        else if (type == typeof(double?))
            return Reader.SafeGetDouble(name);
        else if (type == typeof(DateTime?))
            return Reader.SafeGetDateTime(name);
        else if (type == typeof(int?))
            return Reader.SafeGetInt(name);
        else if (type == typeof(bool?))
            return Reader.SafeGetBool(name);
        throw new NotImplementedException("Unsupported type: " + type.Name);
    }

    public TV? GetOrDefault<TV>(string columnName, TV? defaultValue = default)
    {
        if (!Columns.Contains(columnName)) return defaultValue;
        return (TV?)Get(typeof(TV), columnName);
    }

    public static string GetSelectClause()
    {
        if (!_selectClauses.TryGetValue(typeof(T), out var clause))
        {
            var properties = ReflectionUtils.GetPropertyToName(typeof(T));
            var selectIgnoreFieldNames = properties.Where(pair => pair.Value.GetCustomAttribute<SelectIgnoreAttribute>() != null)
                .Select(pair => pair.Key).ToList();

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