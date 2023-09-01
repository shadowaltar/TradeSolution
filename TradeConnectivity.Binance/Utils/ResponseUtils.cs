using Common;

namespace TradeConnectivity.Binance.Utils;
public class ResponseUtils
{
    /// <summary>
    /// Check the headers in <see cref="HttpResponseMessage"/>.
    /// Should be called selectively after an http request.
    /// </summary>
    /// <param name="response"></param>
    public static string CheckHeaders(HttpResponseMessage response)
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
                var values = ((string[])valueArray);
                return values.FirstOrDefault() ?? "";
            }
        }
        return "";
    }

    public static string GetUniqueConnectionId(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("x-mbx-uuid", out var valArray) ?
            valArray.FirstOrDefault() ?? "" : "";
    }
}
