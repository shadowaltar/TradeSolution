using log4net;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Common;
public static class HttpHelper
{
    private static readonly ILog _log = Logger.New();

    public static async Task ReadIntoFile(this HttpClient client, string url, string saveFilePath, ILog? log = null)
    {
        log ??= _log;

        using var stream = await client.GetStreamAsync(url).ConfigureAwait(false);
        using var fileStream = new FileStream(saveFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

        byte[] buffer = new byte[1024];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            fileStream.Write(buffer, 0, bytesRead);

        log.Info($"Read and saved file {url}, length: {new FileInfo(saveFilePath).Length}bytes");
    }

    public static async Task ReadIntoFile(string url, string saveFilePath, ILog? log = null)
    {
        log ??= _log;

        using var client = new HttpClient();
        await ReadIntoFile(client, url, saveFilePath, log);
    }

    public static async Task<JsonObject?> ReadJson(string url, ILog? log = null)
    {
        log ??= _log;
        using var httpClient = new HttpClient();
        return await ReadJson(httpClient, url, log);
    }

    public static async Task<JsonObject?> ReadJson(this HttpClient client, string url, ILog? log = null)
    {
        log ??= _log;
        try
        {
            var json = await client.GetStringAsync(url).ConfigureAwait(false);
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

    public static async Task<JsonArray?> ReadJsonArray(this HttpClient client, string url, ILog? log = null)
    {
        log ??= _log;
        try
        {
            var json = await client.GetStringAsync(url).ConfigureAwait(false);
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

    public static async Task<(HttpResponseMessage response, long elapsedMs)> TimedSendAsync(this HttpClient client, HttpRequestMessage request)
    {
        var swInner = Stopwatch.StartNew();
        var response = await client.SendAsync(request);
        swInner.Stop();
        return (response, swInner.ElapsedMilliseconds);
    }


    private static async Task<string> CheckContentAndStatus(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            _log.Info(response);
        }
        else
        {
            _log.Error(response.StatusCode + ": " + content);
        }
        return content;
    }
}
