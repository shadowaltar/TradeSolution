using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Runtime;

namespace TradeDataCore.MarketData;
public interface IRealTimeMarketDataService
{
    event Action<int, OhlcPrice> NewOhlc;
    Task Initialize();
    ExternalConnectionState SubscribeOhlc(Security security);
    Task<ExternalConnectionState> UnsubscribeOhlc(Security security);
    Task<ExternalConnectionState> UnsubscribeAllOhlcs();

    Task<ExternalConnectionState> SubscribeTick(Security security);
    Task<ExternalConnectionState> UnsubscribeTick(Security security);
    Task<ExternalConnectionState> UnsubscribeAllTicks();
}
