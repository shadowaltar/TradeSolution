using System.Text.Json;
using System.Text.Json.Nodes;

namespace Common;
public static class Json
{
    private static readonly JsonSerializerOptions _option = new JsonSerializerOptions()
    {
        WriteIndented = true
    };

    public static T? Clone<T>(T obj)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj));
    }

    public static string Serialize<T>(T obj, bool isPretty = false)
    {
        return isPretty ? JsonSerializer.Serialize<T>(obj, _option) : JsonSerializer.Serialize<T>(obj);
    }

    public static T? Deserialize<T>(string content)
    {
        return JsonSerializer.Deserialize<T>(content);
    }

    public static T? Deserialize<T>(JsonArray content)
    {
        return JsonSerializer.Deserialize<T>(content);
    }

    public static T? Deserialize<T>(JsonElement content)
    {
        return JsonSerializer.Deserialize<T>(content);
    }

    public static object? Deserialize(Type type, string content)
    {
        return JsonSerializer.Deserialize(content, type);
    }

    public static string ToJson(object obj)
    {
        return JsonSerializer.Serialize(obj);
    }
}
