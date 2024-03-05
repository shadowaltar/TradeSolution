using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Runtime;
using static TradeCommon.Utils.Delegates;

namespace TradeCommon.Externals;
public interface IExternalQuotationManagement
{
    string Name { get; }

    double LatencyOneSide { get; }
    double LatencyRoundTrip { get; }
    TimeSpan LatencyRoundTripInTimeSpan { get; }
    TimeSpan LatencyOneSideInTimeSpan { get; }

    public event OhlcPriceReceivedCallback? NextOhlc;

    public event Action<ExtendedTick>? NextTick;

    public event Action<ExtendedOrderBook>? NextOrderBook;

    Task<ExternalConnectionState> Initialize();

    Task<ExternalQueryState> GetPrices(params string[] symbols);
    
    Task<ExternalQueryState> GetPrice(string symbol);

    ExternalConnectionState SubscribeTick(Security security);

    ExternalConnectionState SubscribeOrderBook(Security security, int? level = null);

    ExternalConnectionState SubscribeOhlc(Security security, IntervalType intervalType);

    Task<ExternalConnectionState> UnsubscribeTick(Security security);

    Task<ExternalConnectionState> UnsubscribeOrderBook(Security security);

    Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType intervalType);

    Task<ExternalConnectionState> UnsubscribeAllOhlc();

    Task<OrderBook?> GetCurrentOrderBook(Security security);
}
