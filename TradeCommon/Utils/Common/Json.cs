using System.Text.Json;

namespace Common;
public static class Json
{
    public static T? Clone<T>(T obj)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj));
    }
}
