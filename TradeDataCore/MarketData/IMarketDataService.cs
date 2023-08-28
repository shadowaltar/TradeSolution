using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeDataCore.MarketData;
public interface IMarketDataService
{
    /// <summary>
    /// Event when next OHLC price is fetched. Returns security Id and the price instance.
    /// </summary>
    event Action<int, OhlcPrice> NextOhlc;
    /// <summary>
    /// Event when next tick price is fetched. Returns security Id and the price instance.
    /// </summary>
    event Action<int, Tick> NextTick;
    /// <summary>
    /// Event when no more historical price would be output. Returns total price count.
    /// </summary>
    event Action<int> HistoricalPriceEnd;

    IExternalQuotationManagement External { get; }

    Task Initialize();
    Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType interval, DateTime? start = null, DateTime? end = null);
    Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType interval);
    Task<ExternalConnectionState> UnsubscribeAllOhlcs();

    Task<ExternalConnectionState> SubscribeTick(Security security);
    Task<ExternalConnectionState> UnsubscribeTick(Security security);
    Task<ExternalConnectionState> UnsubscribeAllTicks();
}