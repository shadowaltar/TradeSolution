using Common;
using log4net;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace TradeConnectivity.Binance.Utils;
public static class ResponseUtils
{
    private static readonly ILog _log = Logger.New();

    /// <summary>
    /// Check the headers in <see cref="HttpResponseMessage"/>.
    /// Should be called selectively after an http request.
    /// </summary>
    /// <param name="response"></param>
    public static string CheckHeaders(this HttpResponseMessage response)
    {
        foreach (var (key, valueArray) in response.Headers)
        {
            if (key.StartsWithIgnoreCase("X-MBX-ORDER-COUNT-"))
            {
                // TODO
            }
            else if (key.StartsWithIgnoreCase("X-MBX-USED-WEIGHT-"))
            {
                if (key.EndsWithIgnoreCase("1s"))
                {
                    var value = ((string[])valueArray)[0];
                }
                else if (key.EndsWithIgnoreCase("1m"))
                {

                }
                // TODO
            }
            else if (key.StartsWithIgnoreCase("Retry-After"))
            {
                // TODO
            }
            else if (key == "x-mbx-uuid")
            {
                var values = (string[])valueArray;
                return values.FirstOrDefault() ?? "";
            }
        }
        return "";
    }

    public static bool ParseJsonObject(this HttpResponseMessage response,
                                       [NotNullWhen(true)] out string? content,
                                       [NotNullWhen(true)] out JsonObject? jsonObject,
                                       [NotNullWhen(false)] out string? errorMessage,
                                       ILog? log = null)
    {
        log ??= _log;
        jsonObject = null;
        if (ParseJsonNode(response, out content, out var jsonNode, out errorMessage, log))
        {
            try
            {
                jsonObject = jsonNode.AsObject();
                return true;
            }
            catch (InvalidOperationException)
            {
                errorMessage += $" | Also failed to cast to JsonObject.";
                log.Error($"Received Error [{response.StatusCode}]: {errorMessage}");
                return false;
            }
        }
        return false;
    }


    public static bool ParseJsonArray(this HttpResponseMessage response,
                                      [NotNullWhen(true)] out string? content,
                                      [NotNullWhen(true)] out JsonArray? jsonArray,
                                      [NotNullWhen(false)] out string? errorMessage,
                                      ILog? log = null)
    {
        log ??= _log;
        jsonArray = null;
        if (ParseJsonNode(response, out content, out var jsonNode, out errorMessage, log))
        {
            try
            {
                jsonArray = jsonNode.AsArray();
                return true;
            }
            catch (InvalidOperationException)
            {
                errorMessage += $" | Also failed to cast to JsonArray.";
                log.Error($"Received Error [{response.StatusCode}]: {errorMessage}");
                return false;
            }
        }
        return false;
    }

    public static bool ParseJsonNode(this HttpResponseMessage response,
                                     [NotNullWhen(true)] out string? content,
                                     [NotNullWhen(true)] out JsonNode? jsonNode,
                                     [NotNullWhen(false)] out string? errorMessage,
                                     ILog? log = null)
    {
        log ??= _log;
        jsonNode = null;
        errorMessage = null;
        content = AsyncHelper.RunSync(response.Content.ReadAsStringAsync);
        try
        {
            jsonNode = JsonNode.Parse(content);
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
            log.Error($"Received Error [{response.StatusCode}]: {errorMessage}");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            jsonNode = JsonNode.Parse(content);
            var errorCode = jsonNode.GetInt("code");
            if (errorCode.IsValid())
            {
                errorMessage = $"[{errorCode}] {jsonNode.GetString("msg")}";
                log.Error($"Received Error [{response.StatusCode}]: {errorMessage}");
                return false;
            }
            else
            {
                errorMessage = response.ReasonPhrase ?? "Unknown issue.";
                log.Error($"Received Error [{response.StatusCode}]: {response.ReasonPhrase}");
                return false;
            }
        }
        else if (jsonNode == null || content == "") // notice that "[]" and "{}" are both valid cases
        {
            errorMessage = "Missing data.";
            log.Error($"Received no data [{response.StatusCode}]: json node is null or parsed string has no valid content.");
            return false;
        }
        return true;
    }

    public static string GetUniqueConnectionId(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("x-mbx-uuid", out var valArray) ?
            valArray.FirstOrDefault() ?? "" : "";
    }
}
