using System.Text.Json;

namespace Common;
public static class Json
{
    private static JsonSerializerOptions _option = new JsonSerializerOptions()
    {
        WriteIndented = true
    };

    public static T? Clone<T>(T obj)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj));
    }

    public static string Serialize<T>(T obj, bool isPretty = false)
    {
        if (isPretty)
            return JsonSerializer.Serialize<T>(obj, _option);
        return JsonSerializer.Serialize<T>(obj);
    }

    public static T? Deserialize<T>(string content)
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
