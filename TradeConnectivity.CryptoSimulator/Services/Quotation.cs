using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeConnectivity.CryptoSimulator.Services;
public class Quotation : IExternalQuotationManagement
{
    public string Name => ExternalNames.CryptoSimulator;

    public event Action<int, OhlcPrice>? NextOhlc;
    public event Action<int, OrderBook>? NextOrderBook;

    public Task<OrderBook> GetCurrentOrderBook(Security security)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> Initialize()
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> SubscribeOrderBook(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeOrderBook(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        throw new NotImplementedException();
    }
}
