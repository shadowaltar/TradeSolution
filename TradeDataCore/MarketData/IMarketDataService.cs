using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using static TradeCommon.Utils.Delegates;

namespace TradeDataCore.MarketData;
public interface IMarketDataService
{
    /// <summary>
    /// Event when next OHLC price is fetched. Returns security Id and the price instance.
    /// Parameters: securityId, price, is the price at end of bar.
    /// </summary>
    event OhlcPriceReceivedCallback? NextOhlc;
    /// <summary>
    /// Event when next tick price is fetched. Returns security Id and the price instance.
    /// </summary>
    event TickPriceReceivedCallback? NextTick;
    /// <summary>
    /// Event when no more historical price would be output. Returns total price count.
    /// </summary>
    event Action<int>? HistoricalPriceEnd;

    IExternalQuotationManagement External { get; }

    Task Initialize();
    Task<Dictionary<string, decimal>?> GetPrices(List<Security> securities);
    Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType interval, DateTime? start = null, DateTime? end = null);
    Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType interval);
    Task<ExternalConnectionState> UnsubscribeAllOhlcs();

    Task<ExternalConnectionState> SubscribeTick(Security security);
    Task<ExternalConnectionState> UnsubscribeTick(Security security);
    Task<ExternalConnectionState> UnsubscribeAllTicks();
}