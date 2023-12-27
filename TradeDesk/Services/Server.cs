using Common;
using Common.Web;
using log4net;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeDesk.Utils;

namespace TradeDesk.Services;
public class Server
{
    private static readonly ILog _log = Logger.New();

    private string? _token;
    private string? _restUrl;
    private string? _webSocketUrl;
    private ClientWebSocket? _ohlcWebSocket;

    private static readonly CookieContainer _cookieContainer = new();
    private static readonly HttpClientHandler _clientHandler = new()
    {
        AllowAutoRedirect = true,
        UseCookies = true,
        CookieContainer = _cookieContainer,
        ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) =>
        {
            return true;
        }
    };
    private readonly HttpClient _client = new(_clientHandler);

    public event Action<OhlcPrice>? OhlcReceived;

    public void Initialize(string rootUrl, string token)
    {
        _token = token;
        _restUrl = $"https://{rootUrl.Trim('/')}";
        _webSocketUrl = $"ws://{rootUrl.Trim('/')}";
    }

    public async Task<List<Security>> GetSecurities()
    {
        try
        {
            var url = $"{_restUrl}/{RestApiConstants.Static}/{RestApiConstants.Securities}";
            var uri = new Uri(url).AddParameters(("exchange", ExternalNames.Binance), ("sec-type", SecurityType.Fx.ToString()), ("limit", 50000.ToString()));
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var response = await _client.SendAsync(request);
            var jsonNode = await response.ParseJsonNode(_log);
            if (jsonNode is not JsonArray jsonArray)
            {
                _log.Error("Failed to get orders.");
                return [];
            }
            return Json.Deserialize<List<Security>>(jsonArray) ?? [];
        }
        catch (Exception e)
        {
            _log.Error("Failed to get orders.", e);
            return [];
        }
    }

    public async Task<List<Order>> GetOrders(string securityCode, DateTime? startFrom = null)
    {
        try
        {
            var url = $"{_restUrl}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryOrders}";
            var uri = new Uri(url).AddParameters(("symbol", securityCode));
            if (startFrom == null)
                uri.AddParameters(("where", DataSourceType.MemoryCached.ToString()));
            else
                uri.AddParameters(("start", startFrom.Value.ToString("yyyyMMdd")), ("where", DataSourceType.InternalStorage.ToString()));
            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var response = await _client.SendAsync(request);
            var jsonNode = await response.ParseJsonNode(_log);
            if (jsonNode is not JsonArray jsonArray)
            {
                _log.Error("Failed to get orders.");
                return [];
            }
            return Json.Deserialize<List<Order>>(jsonArray) ?? [];
        }
        catch (Exception e)
        {
            _log.Error("Failed to get orders.", e);
            return [];
        }
    }

    public async Task<List<Trade>> GetTrades(string securityCode, DateTime? startFrom = null)
    {
        try
        {
            var url = $"{_restUrl}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryTrades}";
            var uri = new Uri(url).AddParameters(("symbol", securityCode));
            if (startFrom == null)
                uri.AddParameters(("where", DataSourceType.MemoryCached.ToString()));
            else
                uri.AddParameters(("start", startFrom.Value.ToString("yyyyMMdd")), ("where", DataSourceType.InternalStorage.ToString()));
            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var response = await _client.SendAsync(request);
            var jsonNode = await response.ParseJsonNode(_log);
            if (jsonNode is not JsonArray jsonArray)
            {
                _log.Error("Failed to get trades.");
                return [];
            }
            return Json.Deserialize<List<Trade>>(jsonArray) ?? [];
        }
        catch (Exception e)
        {
            _log.Error("Failed to get trades.", e);
            return [];
        }
    }

    public async Task<List<Asset>> GetAssets()
    {
        try
        {
            var url = $"{_restUrl}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryAssets}";
            var uri = new Uri(url).AddParameters(("where", DataSourceType.MemoryCached.ToString()));
            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var response = await _client.SendAsync(request);
            var jsonNode = await response.ParseJsonNode(_log);
            if (jsonNode is not JsonArray jsonArray)
            {
                _log.Error("Failed to get assets.");
                return [];
            }
            return Json.Deserialize<List<Asset>>(jsonArray) ?? [];
        }
        catch (Exception e)
        {
            _log.Error("Failed to get assets.", e);
            return [];
        }
    }

    public async Task<List<AssetState>> GetAssetStates(DateTime? startFrom = null)
    {
        try
        {
            var url = $"{_restUrl}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryAssetStates}";
            var uri = new Uri(url);
            if (startFrom == null)
                uri.AddParameters(("where", DataSourceType.MemoryCached.ToString()));
            else
                uri.AddParameters(("start", startFrom.Value.ToString("yyyyMMdd")), ("where", DataSourceType.InternalStorage.ToString()));
            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var response = await _client.SendAsync(request);
            var jsonNode = await response.ParseJsonNode(_log);
            if (jsonNode is not JsonArray jsonArray)
            {
                _log.Error("Failed to get assets.");
                return [];
            }
            return Json.Deserialize<List<AssetState>>(jsonArray) ?? [];
        }
        catch (Exception e)
        {
            _log.Error("Failed to get asset states.", e);
            return [];
        }
    }

    public async Task<List<Order>> GetOpenOrders()
    {
        try
        {
            var url = $"{_restUrl}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryOrders}";
            var json = await _client.GetStringAsync(url);
            return Json.Deserialize<List<Order>>(json) ?? [];
        }
        catch (Exception e)
        {
            return [];
        }
    }

    public void SubscribeOrderBook()
    {

    }

    public void SubscribeTick()
    {

    }

    public void SubscribeOhlc(Security security, IntervalType interval)
    {
        _ohlcWebSocket?.Dispose();
        _ohlcWebSocket = new ClientWebSocket();
        var wsName = $"{nameof(OhlcPrice)}_{security.Id}_{interval}";

        var url = $"{_webSocketUrl}/stream/ohlc/{wsName}";
        var ws = new ExtendedWebSocket(_log);
        var message = "";
        ws.Listen(new Uri(url), bytes =>
        {
            var json = Encoding.UTF8.GetString(bytes);
            var ohlc = Json.Deserialize<OhlcPrice>(json);
            if (ohlc != null)
                OhlcReceived?.Invoke(ohlc);
        }, OnWebSocketCreated);


        void OnWebSocketCreated()
        {
            message = $"Subscribed to OHLC price for {security.Code} on {security.Exchange} every {interval}";
            _log.Info(message);
        }
    }

    public void UnsubscribeOhlc()
    {
        _ohlcWebSocket?.Dispose();
    }

    public async Task<Order> CancelOrder(Order order)
    {
        return new();
    }

    public async Task<string> Login(string url, MultipartFormDataContent content)
    {
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(url)
        };
        request.Content = content;
        var header = new ContentDispositionHeaderValue("form-data");
        request.Content.Headers.ContentDisposition = header;

        var response = await _client.PostAsync(request.RequestUri.ToString(), request.Content);
        if (response.IsSuccessStatusCode)
        {
            var loginContent = await response.Content.ReadFromJsonAsync<JsonObject>();
            var result = loginContent.GetString("result");
            if (!Enum.TryParse<ResultCode>(result, out var rc))
            {
                if (rc is not ResultCode.LoginUserAndAccountOk or ResultCode.AlreadyLoggedIn)
                {
                    MessageBoxes.Info(null, "Result: " + rc, "Login Failed");
                    return null;
                }
            }
            return loginContent.GetString("token");

            //var resultCodeStr = loginContent.GetProperty("result").GetString();
            //if (!Enum.TryParse<ResultCode>(resultCodeStr, out var resultCode))
            //{
            //    MessageBoxes.Info(null, "Result: " + resultCode, "Login Failed");
            //}
            //var token = loginContent.GetProperty("Token").GetString();
            // must set the auth-token from now on
            //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        }
        else
        {
            MessageBoxes.Info(null, "Warn: " + response.StatusCode, "Login Failed");
            return null;
        }
    }
}
