using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Runtime;

namespace TradeCommon.Externals;
public interface IExternalQuotationManagement
{
    string Name { get; }

    public event Action<int, OhlcPrice, bool>? NextOhlc;

    public event Action<ExtendedTick>? NextTick;

    public event Action<int, OrderBook>? NextOrderBook;

    Task<ExternalConnectionState> Initialize();

    Task<ExternalQueryState> GetPrices(params string[] symbols);

    Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType intervalType);

    Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType intervalType);
    
    Task<ExternalConnectionState> UnsubscribeAllOhlc();

    Task<ExternalConnectionState> SubscribeTick(Security security);

    Task<ExternalConnectionState> UnsubscribeTick(Security security);

    Task<ExternalConnectionState> SubscribeOrderBook(Security security, IntervalType intervalType);

    Task<ExternalConnectionState> UnsubscribeOrderBook(Security security, IntervalType intervalType);

    Task<OrderBook?> GetCurrentOrderBook(Security security);
}
