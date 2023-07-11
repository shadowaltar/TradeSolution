using System.Data.Common;

namespace TradeDataCore.Utils;

public static class SqlExtensions
{
    public static string? SafeGetString(this DbDataReader reader, string fieldName, string defaultValue = "")
    {
        var i = reader.GetOrdinal(fieldName);
        if (reader.IsDBNull(i))
            return defaultValue;
        return reader.GetString(i);
    }

    public static decimal? SafeGetDecimal(this DbDataReader reader, string fieldName, decimal? defaultValue = null)
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
