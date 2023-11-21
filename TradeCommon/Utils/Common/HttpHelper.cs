using log4net;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Web;
using TradeCommon.Constants;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Externals;
using TradeCommon.Runtime;

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

    public static async Task<(HttpResponseMessage response, long elapsedMs)> TimedSendAsync(this HttpClient client, HttpRequestMessage request, ILog? log = null)
    {
        Stopwatch? swInner = null;
        log ??= _log;
        try
        {
            // route to the fake client
            if (client is FakeHttpClient fakeClient)
            {
                return await fakeClient.TimedSendAsync(request);
            }
            swInner = Stopwatch.StartNew();
            var response = await client.SendAsync(request);
            swInner.Stop();
            if (_log.IsDebugEnabled)
                _log.Debug($"[{swInner.Elapsed.Seconds:F4}s] Called REST API ({request.Method}): {request.RequestUri}");
            return (response, swInner.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            log.Error($"Failed to send request to {request.RequestUri}. Error: " + e.Message, e);
            return (new HttpResponseMessage(HttpStatusCode.NotFound), swInner?.ElapsedMilliseconds ?? 0);
        }
    }

    public static async Task<JsonNode?> ParseJsonNode(this HttpResponseMessage response, ILog? log = null)
    {
        log ??= _log;
        var content = await response.Content.ReadAsStringAsync();
        try
        {
            return JsonNode.Parse(content) ?? new JsonObject();
        }
        catch (Exception e)
        {
            log.Error($"Received Error [{response.StatusCode}]", e);
            return false;
        }
    }

    public static Uri AddParameters(this Uri url, params (string Name, string Value)[] @params)
    {
        if (!@params.Any())
        {
            return url;
        }

        UriBuilder uriBuilder = new(url);
        NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach (var param in @params)
        {
            query[param.Name] = param.Value.Trim();
        }

        uriBuilder.Query = query.ToString();

        return uriBuilder.Uri;
    }

    public static HttpClient HttpClientWithoutCert()
    {
        return new HttpClient(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) =>
            {
                return true;
            }
        });
    }
}
