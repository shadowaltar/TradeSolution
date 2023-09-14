using System.Text.Json;
using System.Text.Json.Nodes;

namespace Common;

public static class JsonExtensions
{
    public static long GetLong(this JsonNode? node, string? fieldName = null, long defaultValue = long.MinValue)
    {
        var kind = TryGetJsonValueAndKind(node, fieldName, out var jsonValue);
        if (kind == JsonValueKind.Undefined || jsonValue == null)
        {
            return defaultValue;
        }
        return kind switch
        {
            JsonValueKind.Number => jsonValue.GetValue<long>(),
            JsonValueKind.String => long.TryParse(jsonValue.GetValue<string>(), out var result) ? result : defaultValue,
            _ => defaultValue
        };
    }

    public static int GetInt(this JsonNode? node, string? fieldName = null, int defaultValue = int.MinValue)
    {
        if (fieldName == null)
            return node?.AsValue().ToString()?.ParseInt(defaultValue: defaultValue) ?? defaultValue;
        return node?[fieldName]?.AsValue().GetValue<int>() ?? defaultValue;
    }

    public static bool GetBoolean(this JsonNode node, string? fieldName = null, bool defaultValue = false)
    {
        try
        {
            if (fieldName == null)
                return node.AsValue().GetValue<bool>();
            var sub = node[fieldName];
            if (sub == null) return defaultValue;
            return sub.AsValue().GetValue<bool>();
        }
        catch
        {
            return defaultValue;
        }
    }

    public static string GetString(this JsonNode node, string? fieldName = null, string defaultValue = "")
    {
        try
        {
            if (fieldName == null)
                return node?.AsValue().GetValue<string>() ?? defaultValue;
            var sub = node[fieldName];
            if (sub == null) return defaultValue;
            return sub.AsValue().GetValue<string>() ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public static decimal GetDecimal(this JsonNode? node, string? fieldName = null, decimal defaultValue = decimal.MinValue)
    {
        var kind = TryGetJsonValueAndKind(node, fieldName, out var jsonValue);
        if (kind == JsonValueKind.Undefined || jsonValue == null)
        {
            return defaultValue;
        }
        return kind switch
        {
            JsonValueKind.Number => jsonValue.GetValue<decimal>(),
            JsonValueKind.String => decimal.TryParse(jsonValue.GetValue<string>(), out var result) ? result : defaultValue,
            _ => defaultValue
        };
    }

    public static double GetDouble(this JsonNode? node, string? fieldName = null, double defaultValue = double.NaN)
    {
        var kind = TryGetJsonValueAndKind(node, fieldName, out var jsonValue);
        if (kind == JsonValueKind.Undefined || jsonValue == null)
        {
            return defaultValue;
        }
        return kind switch
        {
            JsonValueKind.Number => jsonValue.GetValue<double>(),
            JsonValueKind.String => double.TryParse(jsonValue.GetValue<string>(), out var result) ? result : defaultValue,
            _ => defaultValue
        };
    }

    public static DateTime GetLocalFromUnixSec(this JsonNode? node, string? fieldName = null, DateTime defaultValue = default)
    {
        var kind = TryGetJsonValueAndKind(node, fieldName, out var jsonValue);
        if (kind == JsonValueKind.Undefined || jsonValue == null)
        {
            return defaultValue;
        }

        return kind switch
        {
            JsonValueKind.Number => jsonValue.GetValue<long>().FromLocalUnixSec(),
            JsonValueKind.String => long.TryParse(jsonValue.GetValue<string>(), out var result) ? result.FromLocalUnixSec() : default,
            _ => defaultValue,
        };
    }

    public static DateTime GetLocalFromUnixMs(this JsonNode? node, string? fieldName = null, DateTime defaultValue = default)
    {
        var kind = TryGetJsonValueAndKind(node, fieldName, out var jsonValue);
        if (kind == JsonValueKind.Undefined || jsonValue == null)
        {
            return defaultValue;
        }

        return kind switch
        {
            JsonValueKind.Number => jsonValue.GetValue<long>().FromLocalUnixMs(),
            JsonValueKind.String => long.TryParse(jsonValue.GetValue<string>(), out var result) ? result.FromLocalUnixMs() : default,
            _ => defaultValue,
        };
    }

    public static DateTime GetUtcFromUnixMs(this JsonNode? node, string? fieldName = null, DateTime defaultValue = default)
    {
        var kind = TryGetJsonValueAndKind(node, fieldName, out var jsonValue);
        if (kind == JsonValueKind.Undefined || jsonValue == null)
        {
            return defaultValue;
        }

        return kind switch
        {
            JsonValueKind.Number => jsonValue.GetValue<long>().FromUnixMs(),
            JsonValueKind.String => long.TryParse(jsonValue.GetValue<string>(), out var result) ? result.FromUnixMs() : default,
            _ => defaultValue,
        };
    }

    private static JsonValueKind TryGetJsonValueAndKind(JsonNode? node, string? fieldName, out JsonValue? jsonValue)
    {
        jsonValue = fieldName == null ? (node?.AsValue()) : (node?[fieldName]?.AsValue());
        if (jsonValue == null)
            return JsonValueKind.Undefined;

        var innerJsonValue = jsonValue.GetValue<object>();
        if (innerJsonValue is not JsonElement element)
        {
            return JsonValueKind.Undefined;
        }
        return element.ValueKind;
    }
}