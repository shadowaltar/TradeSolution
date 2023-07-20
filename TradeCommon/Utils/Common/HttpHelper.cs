using log4net;
using System.Text.Json.Nodes;

namespace Common;
public class HttpHelper
{
    private static readonly ILog _log = Logger.New();

    public static async Task ReadFile(string url, string saveFilePath, ILog? log = null)
    {
        log ??= _log;

        var client = new HttpClient();
        using var stream = await client.GetStreamAsync(url).ConfigureAwait(false);
        using var fileStream = new FileStream(saveFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

        byte[] buffer = new byte[1024];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            fileStream.Write(buffer, 0, bytesRead);

        log.Info($"Read and saved file {url}, length: {new FileInfo(saveFilePath).Length}bytes");
    }

    public static async Task<JsonObject?> ReadJson(string url, ILog? log = null)
    {
        log ??= _log;
        using var httpClient = new HttpClient();
        return await ReadJson(url, httpClient, log);
    }

    public static async Task<JsonObject?> ReadJson(string url, HttpClient httpClient, ILog? log = null)
    {
        log ??= _log;
        try
        {
            var json = await httpClient.GetStringAsync(url).ConfigureAwait(false);
            if (json != null)
            {
                log.Info($"Read json object from {url}, length: {json.Length}");
                return JsonNode.Parse(json)?.AsObject();
            }
            log.Info($"Read empty string from {url}. No json object returned.");
            return null;
        }
        catch (HttpRequestException e)
        {
            _log.Error($"[{e.StatusCode}] Failed to read json object from {url}. Message: {e.Message}.", e);
            return null;
        }
        catch (Exception e)
        {
            log.Info($"Failed to read json object from {url}.", e);
            return null;
        }
    }

    public static async Task<JsonArray?> ReadJsonArray(string url, HttpClient httpClient, ILog? log = null)
    {
        log ??= _log;
        try
        {
            var json = await httpClient.GetStringAsync(url).ConfigureAwait(false);
            if (json != null)
            {
                log.Info($"Read json array from {url}, length: {json.Length}");
                return JsonNode.Parse(json)?.AsArray();
            }
            log.Info($"Read empty string from {url}. No json object returned.");
            return null;
        }
        catch (HttpRequestException e)
        {
            _log.Error($"[{e.StatusCode}] Failed to read json object from {url}. Message: {e.Message}.", e);
            return null;
        }
        catch (Exception e)
        {
            log.Info($"Failed to read json array from {url}.", e);
            return null;
        }
    }
}
