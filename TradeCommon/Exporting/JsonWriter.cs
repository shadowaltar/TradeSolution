using Common;
using System.Text.Json;

namespace TradeCommon.Exporting;
public static class JsonWriter
{
    public static async Task<string> ToJsonFile(object obj, string filePath = "")
    {
        var c = JsonSerializer.Serialize(obj);
        var tempFileName = filePath.IsBlank() ? Path.GetTempFileName() + ".json" : filePath;
        await File.WriteAllTextAsync(tempFileName, c);
        return tempFileName;
    }
}
