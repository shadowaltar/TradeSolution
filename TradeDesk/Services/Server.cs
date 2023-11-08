using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Constants;
using TradeCommon.Essentials.Trading;

namespace TradeDesk.Services;
public class Server
{
    private string _url;
    private HttpClient _client = new HttpClient();
    public void SetUrl(string url)
    {
        _url = url.Trim('/');
    }
    public async Task Login(string userName, string userPassword, string adminPassword)
    {

    }

    internal async Task<List<Order>> GetOrders()
    {
        var url = $"{_url}/{RestApiConstants.QueryOrders}";
        var json = await _client.GetStringAsync(url);
        return Json.Deserialize<List<Order>>(json) ?? new();
    }

    internal async Task<List<Order>> GetOpenOrders()
    {
        var url = $"{_url}/{RestApiConstants.QueryOrders}";
        var json = await _client.GetStringAsync(url);
        return Json.Deserialize<List<Order>>(json) ?? new();
    }

    internal async Task<Order> CancelOrder(Order order)
    {
        throw new NotImplementedException();
    }
}
