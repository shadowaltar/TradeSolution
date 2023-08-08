using System.Text.Json;
using TradeCommon.Essentials;

namespace Common;
public static class Json
{
    public static T? Clone<T>(T obj)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj));
    }

    public static async Task<T?> Deserialize<T>(string content)
    {
        return JsonSerializer.Deserialize<T>(content);
    }

    public static string ToJson(object obj)
    {
        return JsonSerializer.Serialize(obj);
    }
}
