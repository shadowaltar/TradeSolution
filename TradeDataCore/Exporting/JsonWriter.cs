using System.Text.Json;
using TradeDataCore.Utils;

namespace TradeDataCore.Exporting;
public static class JsonWriter
{
    public static async Task<string> ToJsonFile(object obj, string fileName = "")
    {
        var c = JsonSerializer.Serialize(obj);
        var tempFileName = fileName.IsBlank() ? Path.GetTempFileName() + ".json" : fileName;
        await File.WriteAllTextAsync(tempFileName, c);
        return tempFileName;
    }
}
