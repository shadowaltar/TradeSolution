using System.Text.Json;
using System.Text.Json.Nodes;

namespace Common;

public static class JsonExtensions
{
    public static long GetLong(this JsonNode? node, string? fieldName = null, long defaultValue = long.MinValue)
    {
        if (fieldName == null)
            return node?.AsValue().GetValue<long>() ?? default ;
        return node?[fieldName]?.AsValue().GetValue<long>() ?? defaultValue;
    }

    public static int GetInt(this JsonNode? node, string? fieldName = null, int defaultValue = int.MinValue)
    {
        if (fieldName == null)
            return node?.AsValue().ToString()?.ParseInt(defaultValue: defaultValue) ?? defaultValue;
        return node?[fieldName]?.AsValue().GetValue<int>() ?? defaultValue;
    }

    public static bool GetBoolean(this JsonNode node, string? fieldName = null)
    {
        if (fieldName == null)
            return node.AsValue().GetValue<bool>();
        return node[fieldName]!.AsValue().GetValue<bool>();
    }

    public static decimal GetDecimal(this JsonNode? node, string? fieldName = null, decimal defaultValue = decimal.MinValue)
    {
        if (fieldName == null)
            return node?.AsValue().ToString()?.ParseDecimal(defaultValue: defaultValue) ?? defaultValue;

        var jsonValue = node?[fieldName]?.AsValue();
        if (jsonValue == null)
            return defaultValue;

        var innerJsonValue = jsonValue.GetValue<object>();
        if (innerJsonValue is JsonElement element)
        {
            decimal result = element.ValueKind switch
            {
                JsonValueKind.Number => jsonValue.GetValue<decimal>(),
                JsonValueKind.String => decimal.Parse(jsonValue.GetValue<string>()),
                _ => defaultValue
            };
            return result;
        }
        return defaultValue;
    }

    public static DateTime GetLocalUnixDateTime(this JsonNode? node, string? fieldName = null, DateTime defaultValue = default)
    {
        if (fieldName == null)
            return node?.AsValue().ToString()?.ParseLocalUnixDate() ?? defaultValue;
        var seconds = node?[fieldName]?.AsValue().GetValue<int>() ?? 0;
        return DateUtils.FromLocalUnixSec(seconds);
    }

    public static DateTime GetUtcUnixDateTime(this JsonNode? node, string? fieldName = null, DateTime defaultValue = default)
    {
        if (fieldName == null)
            return node?.AsValue().ToString()?.ParseLocalUnixDate() ?? defaultValue;
        var seconds = node?[fieldName]?.AsValue().GetValue<int>() ?? 0;
        return DateUtils.FromLocalUnixSec(seconds);
    }
}