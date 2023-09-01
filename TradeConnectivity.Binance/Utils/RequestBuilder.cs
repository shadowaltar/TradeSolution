using Common;
using Microsoft.Identity.Client;
using System;
using System.Text;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;
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
        request.Method = method;

        var result = "";
        if (method == HttpMethod.Get)
        {
            parameters ??= new();
            result = StringUtils.ToUrlParamString(parameters);
            request.RequestUri = !result.IsBlank()
                ? new Uri($"{url}?{result}")
                : new Uri(url);
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
        }
        return request;
    }

    public HttpRequestMessage BuildSigned(HttpMethod method,
                                          string url,
                                          List<(string key, string value)>? parameters = null)
    {
        var request = new HttpRequestMessage();
        request.Method = method;

        parameters ??= new List<(string, string)>();
        var result = AppendSignedParameters(request, parameters);

        if (method == HttpMethod.Get)
        {
            request.RequestUri = !result.IsBlank()
                ? new Uri($"{url}?{result}")
                : new Uri(url);
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
        }
        return request;
    }

    private string AppendSignedParameters(HttpRequestMessage request, List<(string key, string value)> parameters)
    {
        // add 'signature' to POST body (or as GET arguments): an HMAC-SHA256 signature
        // add 'timestamp' and 'receive window'
        request.Headers.Add("X-MBX-APIKEY", _keyManager.GetApiKey());

        var timestamp = DateTime.UtcNow.ToUnixMs();
        parameters.Add(("recvWindow", _receiveWindowMsString));
        parameters.Add(("timestamp", timestamp.ToString()));

        var parameterString = StringUtils.ToUrlParamString(parameters);
        var hashedValueBytes = _keyManager.ComputeHash(parameterString);
        var trueSecret = Convert.ToHexString(hashedValueBytes);

        return $"{parameterString}&signature={trueSecret}";
    }
}
