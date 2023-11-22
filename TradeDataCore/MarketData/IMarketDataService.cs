using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using static TradeCommon.Utils.Delegates;

namespace TradeDataCore.MarketData;
public interface IMarketDataService
{
    /// <summary>
    /// Event when next OHLC price is fetched. Returns security Id and the OHLC price instance.
    /// </summary>
    event OhlcPriceReceivedCallback? NextOhlc;
    /// <summary>
    /// Event when next tick price is fetched. Returns security Id and the tick price instance.
    /// </summary>
    event TickPriceReceivedCallback? NextTick;
    /// <summary>
    /// Event when next order book is fetched. Returns security Id and the order book instance.
    /// </summary>
    event OrderBookReceivedCallback? NextOrderBook;
    /// <summary>
    /// Event when no more historical price would be output. Returns total price count.
    /// </summary>
    event Action<int>? HistoricalPriceEnd;

    IExternalQuotationManagement External { get; }

    Task Initialize();
    
    Task Reset();

    Task<Dictionary<string, decimal>?> GetPrices(List<Security> securities);
    Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType interval, DateTime? start = null, DateTime? end = null);
    Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType interval);
    Task<ExternalConnectionState> UnsubscribeAllOhlcs();
    Task<ExternalConnectionState> SubscribeOrderBook(Security security, int? levels = null);
    Task<ExternalConnectionState> UnsubscribeOrderBook(Security security);
    Task<ExternalConnectionState> UnsubscribeAllOrderBooks();

    Task<ExternalConnectionState> SubscribeTick(Security security);
    Task<ExternalConnectionState> UnsubscribeTick(Security security);
    Task<ExternalConnectionState> UnsubscribeAllTicks();

    Task PrepareOrderBookTable(Security security, int orderBookLevels);
}