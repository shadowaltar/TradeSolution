using System.Text.Json;
using TradeCommon.Essentials;

namespace Common;
public static class Json
{
    public static T? Clone<T>(T obj)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj));
    }

    public static string ToJson(object obj)
    {
        return JsonSerializer.Serialize(obj);
    }
}
