using Common;
using System.Text;
using TradeCommon.Utils;
using TradeConnectivity.Binance.Services;

namespace TradeConnectivity.Binance.Utils;
public class RequestBuilder
{
    private readonly KeyManager _keyManager;
    private readonly string _receiveWindowMsString;

    public RequestBuilder(KeyManager keyManager, int receiveWindowMs)
    {
        _keyManager = keyManager;
        _receiveWindowMsString = receiveWindowMs.ToString();
    }

    public string Build(HttpRequestMessage request,
                        HttpMethod method,
                        string url,
                        bool isSignedEndpoint,
                        List<(string key, string value)>? parameters = null)
    {
        request.Method = method;

        var result = "";
        if (isSignedEndpoint)
        {
            parameters ??= new List<(string, string)>();
            result = AppendSignedParameters(request, parameters);
        }

        if (method == HttpMethod.Get)
        {
            request.RequestUri = !result.IsBlank()
                ? new Uri($"{url}?{result}")
                : new Uri(url);
            return ""; // payload is empty
        }
        else
        {
            // if signed, result string is already constructed
            if (!parameters.IsNullOrEmpty() && result.IsBlank())
            {
                result = StringUtils.ToUrlParamString(parameters);
            }
            request.Content = new StringContent(result);
            request.RequestUri = new Uri(url);
            return result;
        }
    }

    public string AppendSignedParameters(HttpRequestMessage request, List<(string key, string value)>? parameters)
    {
        // add 'signature' to POST body (or as GET arguments): an HMAC-SHA256 signature
        // add 'timestamp' and 'receive window'
        request.Headers.Add("X-MBX-APIKEY", Keys.ApiKey);

        var timestamp = DateTime.UtcNow.ToUnixMs();
        parameters ??= new();
        parameters.Add(("recvWindow", _receiveWindowMsString));
        parameters.Add(("timestamp", timestamp.ToString()));

        var parameterString = StringUtils.ToUrlParamString(parameters);
        var valueBytes = Encoding.UTF8.GetBytes(parameterString);
        var hashedValueBytes = _keyManager.Hasher!.ComputeHash(valueBytes);
        var trueSecret = Convert.ToHexString(hashedValueBytes);

        return $"{parameterString}&signature={trueSecret}";
    }
}
