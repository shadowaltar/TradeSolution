using Common;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace Common;
public static class SqlExtensions
{
    public static T ReadFromSql<T>(this SqlDataReader reader, List<(string name, Type type, Action<T, object> setter)> setters) where T : new()
    {
        var result = new T();
        foreach (var (name, type, setter) in setters)
        {
            if (type == typeof(string))
            {
                var value = reader.SafeGetString(name);
                setter.Invoke(result, value);
            }
            else if (type == typeof(decimal))
            {
                var value = reader.SafeGetDecimal(name);
                if (value == null) continue;
                setter.Invoke(result, value);
            }
            else if (type == typeof(DateTime))
            {
                var value = reader.SafeGetDateTime(name);
                if (value == null) continue;
                setter.Invoke(result, value);
            }
            else if (type == typeof(int))
            {
                var value = reader.SafeGetInt(name);
                if (value == null) continue;
                setter.Invoke(result, value);
            }
            else if (type == typeof(bool))
            {
                var value = reader.SafeGetBool(name);
                if (value == null) continue;
                setter.Invoke(result, value);
            }
            else if (type == typeof(decimal?))
            {
                var value = reader.SafeGetDecimal(name);
                if (value == null) continue;
                setter.Invoke(result, value);
            }
            else
            {
                var value = reader.SafeGetString(name);
                try
                {
                    var v = value?.ConvertDescriptionToEnum(type, 0);
                    setter.Invoke(result, v);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        return result;
    }

    public static void PrepareSqlBulkCopy<T>(this SqlBulkCopy bulkCopy)
    {
        var names = ReflectionUtils.GetPropertyToName(typeof(T)).Keys;
        foreach (var name in names)
        {
            bulkCopy.ColumnMappings.Add(name, name);
        }
    }

    public static string? SafeGetString(this DbDataReader reader, string fieldName, string defaultValue = "")
    {
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetString(i);
    }

    public static decimal SafeGetDecimal(this DbDataReader reader, string fieldName, decimal defaultValue = default)
    {
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetDecimal(i);
    }

    public static int? SafeGetInt(this DbDataReader reader, string fieldName, int? defaultValue = null)
    {
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetInt32(i);
    }

    public static DateTime? SafeGetDateTime(this DbDataReader reader, string fieldName, DateTime? defaultValue = null)
    {
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetDateTime(i);
    }

    public static bool? SafeGetBool(this DbDataReader reader, string fieldName, bool? defaultValue = null)
    {
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetBoolean(i);
    }
}
