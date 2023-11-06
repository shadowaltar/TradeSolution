using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials.Trading;

namespace TradeDesk.Services;
public class Server
{
    private string _url;

    public void SetUrl(string url)
    {
        _url = url;
    }

    public List<Order> QueryOpenOrders()
    {
        var sub = "orders/query-all";
    }
}
