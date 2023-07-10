using System.Text.Json.Nodes;

namespace TradeDataCore.Utils;
public static class JsonExtensions
{
    public static int GetInt(this JsonNode? node, string? fieldName = null, int defaultValue = default)
    {
        if (fieldName == null)
            return node?.AsValue().ToString()?.ParseInt(defaultValue: defaultValue) ?? defaultValue;
        return node?[fieldName]?.AsValue().GetValue<int>() ?? defaultValue;
    }

    public static decimal GetDecimal(this JsonNode? node, string? fieldName = null, decimal defaultValue = default)
    {
        if (fieldName == null)
            return node?.AsValue().ToString()?.ParseDecimal(defaultValue: defaultValue) ?? defaultValue;
        return node?[fieldName]?.AsValue().GetValue<decimal>() ?? defaultValue;
    }

    public static DateTime GetLocalUnixDateTime(this JsonNode? node, string? fieldName = null, DateTime defaultValue = default)
    {
        if (fieldName == null)
            return node?.AsValue().ToString()?.ParseLocalUnixDate() ?? defaultValue;
        var seconds = node?[fieldName]?.AsValue().GetValue<int>() ?? 0;
        return DateUtils.FromLocalUnixSec(seconds);
    }
}
