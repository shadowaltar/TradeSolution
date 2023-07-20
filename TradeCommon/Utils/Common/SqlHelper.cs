using System.Data;
using System.Data.Common;
using System.Reflection;
using Common;

namespace Common;

public class SqlHelper<T> : IDisposable where T : new()
{
    private Dictionary<string, PropertyInfo> _properties;
    private ValueSetter<T> _valueSetter;

    public SqlHelper(DbDataReader reader)
    {
        Reader = reader;
        Columns = reader.GetSchemaTable()!.GetDistinctValues<string>("ColumnName");
        _properties = ReflectionUtils.GetPropertyToName(typeof(T));
        _valueSetter = ReflectionUtils.GetValueSetter<T>();
    }

    public DbDataReader Reader { get; private set; }

    public HashSet<string> Columns { get; private set; }

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

    private object? Get(Type type, string name)
    {
        if (type == typeof(string))
            return Reader.SafeGetString(name);
        else if (type == typeof(decimal))
            return Reader.GetDecimal(name);
        else if (type == typeof(DateTime))
            return Reader.GetDateTime(name);
        else if (type == typeof(int))
            return Reader.GetInt32(name);
        else if (type == typeof(bool))
            return Reader.GetBoolean(name);
        else if (type == typeof(decimal?))
            return Reader.SafeGetDecimal(name);
        else if (type == typeof(DateTime?))
            return Reader.SafeGetDateTime(name);
        else if (type == typeof(int?))
            return Reader.SafeGetInt(name);
        else if (type == typeof(bool?))
            return Reader.SafeGetBool(name);
        throw new NotImplementedException("Unsupported type: " + type.Name);
    }

    public void Dispose()
    {
        Columns.Clear();
        Reader = null;
        _properties = null;
        _valueSetter = null;
    }

    public TV? GetOrDefault<TV>(string columnName, TV? defaultValue = default)
    {
        if (!Columns.Contains(columnName)) return defaultValue;
        return (TV?)Get(typeof(TV), columnName);
    }
}
