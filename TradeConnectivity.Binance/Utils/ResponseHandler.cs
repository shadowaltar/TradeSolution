using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeConnectivity.Binance.Utils;
public class ResponseHandler
{
    /// <summary>
    /// Check the headers in <see cref="HttpResponseMessage"/>.
    /// Should be called selectively after an http request.
    /// </summary>
    /// <param name="response"></param>
    public static void CheckHeaders(HttpResponseMessage response)
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
        }
    }

    public static string GetUniqueConnectionId(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("x-mbx-uuid", out var valArray) ?
            valArray.FirstOrDefault() ?? "" : "";
    }
}
