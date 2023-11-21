using Common;
using log4net;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TradeCommon.Constants;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

namespace TradeDesk.Services;
public class Server
{
    private static readonly ILog _log = Logger.New();
    private string _token;
    private string _url;
    private readonly HttpClient _client = new HttpClient();

    public void Setup(string rootUrl, string token)
    {
        _token = token;
        _url = rootUrl.Trim('/');
    }


    public async Task<List<Order>> GetOrders(string securityCode, bool isHistorical, DateTime historicalStart)
    {
        try
        {
            var url = $"{_url}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryOrders}";
            var where = isHistorical ? DataSourceType.InternalStorage : DataSourceType.MemoryCached;
            var uri = new Uri(url).AddParameters(("start", historicalStart.ToString("yyyyMMdd")), ("symbol", securityCode), ("where", where.ToString()));
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
            return new();
        }
    }

    public async Task<List<Trade>> GetTrades(string securityCode, bool isHistorical, DateTime historicalStart)
    {
        try
        {
            var url = $"{_url}/{RestApiConstants.ExecutionRoot}/{RestApiConstants.QueryTrades}";
            var where = isHistorical ? DataSourceType.InternalStorage : DataSourceType.MemoryCached;
            var uri = new Uri(url).AddParameters(("start", historicalStart.ToString("yyyyMMdd")), ("symbol", securityCode), ("where", where.ToString()));
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
            return new();
        }
    }

    internal async Task<List<Order>> GetOpenOrders()
    {
        try
        {
            var url = $"{_url}/{RestApiConstants.QueryOrders}";
            var json = await _client.GetStringAsync(url);
            return Json.Deserialize<List<Order>>(json) ?? new();
        }
        catch (Exception e)
        {
            return new();
        }
    }

    internal async Task<Order> CancelOrder(Order order)
    {
        return new();
    }
}
