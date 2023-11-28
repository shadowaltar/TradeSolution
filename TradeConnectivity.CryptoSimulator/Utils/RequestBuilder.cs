using TradeConnectivity.CryptoSimulator.Services;

namespace TradeConnectivity.CryptoSimulator.Utils;
public class RequestBuilder
{
    public RequestBuilder(KeyManager keyManager, int receiveWindowMs)
    {
    }

    /// <summary>
    /// Build a Binance REST request.
    /// To construct a request for a SIGNED API, provide a valid tuple of user, account and environment.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="url"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public HttpRequestMessage Build(HttpMethod method,
                                    string url,
                                    List<(string key, string value)>? parameters = null)
    {
        var request = new HttpRequestMessage();
        return request;
    }

    public HttpRequestMessage BuildSigned(HttpMethod method,
                                          string url,
                                          List<(string key, string value)>? parameters = null)
    {
        var request = new HttpRequestMessage();
        return request;
    }
}
