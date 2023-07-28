using Futu.OpenApi;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeConnectivity.Futu.Proxies;

namespace TradeConnectivity.Futu.Services;

/// <summary>
/// Futu's quotation engine.
/// According to https://openapi.futunn.com/futu-api-doc/en/intro/authority.html#9123:
/// * For HK users, the cheapest real-time data quota is 100 simultaneous subscription;
/// * For HK users, the cheapest historical data quota is 100, depleted whenever a new
/// symbol is subscribed in the last 30 days. 
/// * For mainland China users, the LV2 HK market quotes and A-share LV1 market quotes are free.
/// </summary>
public class Quotation : IExternalQuotationManagement
{
    public string Name => ExternalNames.Futu;

    private readonly ConnectionProxy _connectionProxy;
    private readonly QuotationProxy _quoterProxy;

    public event Action<int, OhlcPrice>? NextOhlc;
    public event Action<int, OrderBook>? NextOrderBook;

    public Quotation()
    {
        var quoter = new FTAPI_Qot();
        _connectionProxy = new ConnectionProxy(quoter);
        _quoterProxy = new QuotationProxy(quoter);
    }

    public async Task<ExternalConnectionState> Initialize()
    {
        return await _connectionProxy.ConnectAsync("127.0.0.1", 11111, false);
    }

    public async Task<ExternalConnectionState> Disconnect()
    {
        return await _connectionProxy.CloseAsync();
    }

    public async Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        return _quoterProxy.SubscribeSecurity(security, false);
    }

    public async Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        return _quoterProxy.SubscribeSecurity(security, false);
    }

    public Task<ExternalConnectionState> SubscribeOrderBook(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeOrderBook(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        throw new NotImplementedException();
    }

    public Task<OrderBook> GetCurrentOrderBook(Security security)
    {
        throw new NotImplementedException();
    }
}