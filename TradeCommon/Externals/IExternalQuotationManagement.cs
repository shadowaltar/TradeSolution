using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Runtime;

namespace TradeCommon.Externals;
public interface IExternalQuotationManagement
{
    string Name { get; }

    public event Action<int, OhlcPrice, bool>? NextOhlc;

    public event Action<int, OrderBook>? NextOrderBook;

    Task<ExternalConnectionState> Initialize();

    Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType intervalType);

    Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType intervalType = IntervalType.Unknown);
    
    Task<ExternalConnectionState> UnsubscribeAllOhlc();

    Task<ExternalConnectionState> SubscribeOrderBook(Security security, IntervalType intervalType = IntervalType.Unknown);

    Task<ExternalConnectionState> UnsubscribeOrderBook(Security security, IntervalType intervalType = IntervalType.Unknown);

    Task<OrderBook?> GetCurrentOrderBook(Security security);
}
