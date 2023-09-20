using Common;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace Common.Database;
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

    public static string PrintActualSql(this DbCommand cmd)
    {
        string result = cmd.CommandText.ToString();
        foreach (DbParameter p in cmd.Parameters)
        {
            if (p?.Value == null) continue;
            string quotedValue = (p.Value is string) ? $"'{p.Value}'" : p.Value.ToString()!;
            result = result.Replace(p.ParameterName.ToString(), quotedValue);
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
        if (!reader.HasColumn(fieldName))
            return defaultValue;
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetString(i);
    }

    public static double SafeGetDouble(this DbDataReader reader, string fieldName, double defaultValue = default)
    {
        if (!reader.HasColumn(fieldName))
            return defaultValue;
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetDouble(i);
    }

    public static long SafeGetLong(this DbDataReader reader, string fieldName, long defaultValue = default)
    {
        if (!reader.HasColumn(fieldName))
            return defaultValue;
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetInt64(i);
    }

    public static decimal SafeGetDecimal(this DbDataReader reader, string fieldName, decimal defaultValue = default)
    {
        if (!reader.HasColumn(fieldName))
            return defaultValue;
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetDecimal(i);
    }

    public static int? SafeGetInt(this DbDataReader reader, string fieldName, int? defaultValue = null)
    {
        if (!reader.HasColumn(fieldName))
            return defaultValue;
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetInt32(i);
    }

    public static DateTime? SafeGetDateTime(this DbDataReader reader, string fieldName, DateTime? defaultValue = null)
    {
        if (!reader.HasColumn(fieldName))
            return defaultValue;
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetDateTime(i);
    }

    public static bool? SafeGetBool(this DbDataReader reader, string fieldName, bool? defaultValue = null)
    {
        if (!reader.HasColumn(fieldName))
            return defaultValue;
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetBoolean(i);
    }

    public static bool HasColumn(this IDataRecord record, string columnName)
    {
        for (int i = 0; i < record.FieldCount; i++)
        {
            if (record.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool IsSqlNativeType(this Type type)
    {
        if (type == typeof(int)
            || type == typeof(long)
            || type == typeof(int?)
            || type == typeof(long?))
            return true;

        if (type == typeof(decimal)
            || type == typeof(double)
            || type == typeof(decimal?)
            || type == typeof(double?))
            return true;
        if (type == typeof(string)
            || type == typeof(char)
            || type == typeof(char?))
            return true;

        if (type == typeof(DateTime)
            || type == typeof(TimeSpan)
            || type == typeof(DateTime?)
            || type == typeof(TimeSpan?))
            return true;

        if (type == typeof(bool) || type == typeof(bool?))
            return true;

        if (type.IsEnum)
            return true;

        return false;
    }
}
