using Common;
using Common.Web;
using log4net;
using ScottPlot.Renderable;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security;
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

namespace TradeDesk.Services;
public class Server
{
    private static readonly ILog _log = Logger.New();

    private string _token;
    private string _url;
    private readonly HttpClient _client = new HttpClient();
    private ClientWebSocket? _ohlcWebSocket;


    public event Action<OhlcPrice>? OhlcReceived;

    public void Setup(string rootUrl, string token)
    {
        _token = token;
        _url = rootUrl.Trim('/');
    }

    public async Task<List<Security>> GetSecurities()
    {
        try
        {
            var url = $"{_url}/{RestApiConstants.Static}/{RestApiConstants.Securities}";
            var uri = new Uri(url).AddParameters(("exchange", ExternalNames.Binance), ("sec-type", SecurityType.Fx.ToString()), ("limit", 50000.ToString()));
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var response = await _client.SendAsync(request);
            var jsonNode = await response.ParseJsonNode(_log);
            if (jsonNode is not JsonArray jsonArray)
            {
                _log.Error("Failed to get orders.");
                return new();
            }
            return Json.Deserialize<List<Security>>(jsonArray) ?? new();
        }
        catch (Exception e)
        {
            _log.Error("Failed to get orders.", e);
            return new();
        }
    }

    public async Task<List<Order>> GetOrders(string securityCode, DateTime? startFrom = null)
    {
        try
        {
            var url = $"{_url}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryOrders}";
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
                return new();
            }
            return Json.Deserialize<List<Order>>(jsonArray) ?? new();
        }
        catch (Exception e)
        {
            _log.Error("Failed to get orders.", e);
            return new();
        }
    }

    public async Task<List<Trade>> GetTrades(string securityCode, DateTime? startFrom = null)
    {
        try
        {
            var url = $"{_url}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryTrades}";
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
                return new();
            }
            return Json.Deserialize<List<Trade>>(jsonArray) ?? new();
        }
        catch (Exception e)
        {
            _log.Error("Failed to get trades.", e);
            return new();
        }
    }

    public async Task<List<Asset>> GetAssets()
    {
        try
        {
            var url = $"{_url}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryAssets}";
            var uri = new Uri(url).AddParameters(("where", DataSourceType.MemoryCached.ToString()));
            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var response = await _client.SendAsync(request);
            var jsonNode = await response.ParseJsonNode(_log);
            if (jsonNode is not JsonArray jsonArray)
            {
                _log.Error("Failed to get assets.");
                return new();
            }
            return Json.Deserialize<List<Asset>>(jsonArray) ?? new();
        }
        catch (Exception e)
        {
            _log.Error("Failed to get assets.", e);
            return new();
        }
    }

    public async Task<List<AssetState>> GetAssetStates(DateTime? startFrom = null)
    {
        try
        {
            var url = $"{_url}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryAssetStates}";
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
                return new();
            }
            return Json.Deserialize<List<AssetState>>(jsonArray) ?? new();
        }
        catch (Exception e)
        {
            _log.Error("Failed to get asset states.", e);
            return new();
        }
    }

    public async Task<List<Order>> GetOpenOrders()
    {
        try
        {
            var url = $"{_url}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryOrders}";
            var json = await _client.GetStringAsync(url);
            return Json.Deserialize<List<Order>>(json) ?? new();
        }
        catch (Exception e)
        {
            return new();
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

        var url = $"{_url}/ws/ohlc/{wsName}";
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
}
